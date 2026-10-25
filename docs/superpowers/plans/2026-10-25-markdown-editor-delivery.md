# Markdown 编辑器交付进度（2026-10-25）

目标是交付可用、兼顾性能的类 Obsidian Markdown 编辑器。用户已选择原生 WinUI 路径，不新增依赖。完整交付目标保留，本文区分实现、运行证据与未通过的验收。

## 当前实现

编辑表面改为单一 RichEditBox，完整 Markdown 字符保留在一个连续缓冲区中。原生控件负责中文输入、连续选区、软折行与滚动；活动行展开源码，其他行使用 Hidden 和字体格式呈现标题、强调、链接、代码与任务。普通击键仅标记变化并重启 150ms 空闲计时器，不获取全文、不解析、不改写对外 Text。

空闲或保存时同步源码，保留原始 LF、CRLF、混合换行、硬换行空格和尾部空行。Markdig 解析在后台执行，过期结果按文档代数丢弃；UI 只更新视口、旧活动行和新活动行。当前空闲同步与解析仍与文档长度有关，并未实现增量 Markdown 解析器。

真实控件实验证明，修改字体及 Hidden 会进入原生撤销历史。因此关闭原生格式撤销，使用有界源码差量历史，保留修改前后选区两端与方向。已接入 Ctrl+Z/Y、Markdown 格式快捷键、源码复制/剪切、纯文本粘贴，以及列表、任务、引用的 Enter 续行。组字期间暂停同步和格式更新，组字结束后再同步。

任务框通过跟踪原生字符范围定位，点击前验证文档身份，避免前缀输入导致勾错任务。待办自动保存继续使用现有 MarkdownEditingSession；保存入口调用 FlushAndGetText，立即保存不依赖空闲计时器先触发。

表格和独立图片复用现有 MarkdownDocumentView，源码仍在同一个原生缓冲中。可见复杂块创建预览控件，离开视口后移除；最多保留 8 个已分离控件用于短距离往返和编辑状态切换。原生范围保存测量高度，不插入替代字符。待办附件解析器与远程图片开关沿用原有策略。

## WorkCat 参考结果

只读检查了 D:/Project/WorkCat 的当前源码。沿用其活动源码保留排版、逻辑选区、组字结束后同步、任务点击事务及列表续行规则；没有迁移逐键提取全文、全局重建 Inlines、WPF TextPointer 映射及单步全文撤销。未修改 WorkCat。

## 已取得的验证

- 修改前全量基线为 5805 / 5810 通过，存在 5 个既有失败：FeatureWidgetEntryFactory、AotStage5B4B2B2A、AotStage5B4B2B2B1、AotStage5B4B2B2B2、WidgetGroupCloseConfirmation。
- 最近一次定向回归为 189 / 189 通过，包含 Markdown 与 JSON 序列化清单契约。
- 最近一次全量回归为 5853 / 5858 通过，仍为同一组 5 个失败，无新增失败。最后的光标可见性调整另由实际控件探针覆盖。
- 实际原生控件探针已验证源码往返、标题格式、无同步全文提取、撤销重做、跨段删除后恢复源码与选区、前缀插入后的任务勾选、自动保存及立即保存。
- 用户已确认基础显示、光标和中文输入正常。
- 262144 字符、300 段样例中，连续 1000 次原生范围插入的同步全文提取次数为 0；最新一次源码同步约 3.1ms，加载及后台解析约 254ms。1000 次输入回调合计约 4.7ms。原生 API 插入总耗时包含原生布局，不等同于回调耗时，也不能用作真实键入延迟或滚动帧率证据。
- 表格/图片探针确认创建了两个预览，保留全部源码，进入表格后字符可见且恢复 Single 行距，跨表格删除后撤销完整恢复源码，预览可重新出现且编辑切换复用原对象。

## 复杂预览修复与截图验证

用户观察到的图片与文字重叠已通过滚动截图复现，根因是把 RichEdit 返回的未滚动内容坐标当作视口坐标。覆盖层现使用内容坐标，并通过合成器表达式与原生 ScrollViewer 同步滚动；视口范围查询也转换为内容坐标。格式事务保留滚动位置，不再把视口拉回屏幕外的光标；滚动时只格式化新进入视口的范围。滚动 160px 后，图片底部为 365、下方文字顶部为 393，截图已直接检查无重叠。离开图片源码后恢复预览时，也会将新活动光标保持在视口中，实际探针和截图通过。

Windows.Graphics.Capture 多次出现 FrameArrived timed out 或 window capture timed out。用户要求后续自行截图验证，不再请求人工验收。已通过应用自身的 RenderTargetBitmap 获得自产测试窗口内容的 PNG，逐张查看了表格预览、源码展开、滚动后图片与正文交界，以及图片切换后的光标位置。截图位于 .debug-data/md-editor/native-markdown-complex-preview.png、native-markdown-table-source.png、native-markdown-complex-scrolled.png 和 native-markdown-image-caret.png。只捕获自产测试内容。表格采用已处理事件监听，在鼠标按下时直接展开原生源码，其他预览刷新排队执行；相同激活方法的实际测量约 6.8ms，字符与行距立即恢复。

滚动 60fps 尚未证明。在同一个测试窗口、相同 256KiB 文档和更密集的程序滚动输入下，CompositionTarget.Rendering 事件频率为编辑器约 32.2 次/秒、裸 RichEditBox 约 31.6 次/秒；裸控件确实滚动到 1428px。此对照未显示 Markdown 装饰降低频率，但 UI 线程事件计数不能单独证明屏幕呈现为 60fps。因此完整目标尚未标为完成。Computer Use 的实际鼠标注入仍报 coordinate input geometry is unavailable，尚无最终修复后的真实鼠标点击证据；没有再次请求用户验收。

## 实际键盘交互验收

表格和图片预览已提供键盘入口，焦点顺序按源码位置排列。Ctrl+Tab 从原生编辑区进入预览，Enter 展开源码并将焦点交回 RichEditBox；只读状态下 Tab 正常离开编辑区。菜单全选在确定范围前先同步尚未提交的输入。

Computer Use 已实际发送 Ctrl+Tab、Enter、普通字符输入、Ctrl+Z、Ctrl+Y 和 Ctrl+Home，逐步读取可访问性树。探针记录的焦点从 MarkdownDocumentView 变为 Keyboard 状态的 RichEditBox，光标进入表格源码位置 11，预览数从 2 变为 1；实际输入、撤销和重做均改变了真实文档，移到文首后预览数恢复为 2，源码完整恢复。

已逐张检查实际按键后的 PNG。证据为 .debug-data/md-editor/native-markdown-keyboard-source.png、native-markdown-keyboard-source.json 和 native-markdown-keyboard-restored.png。本轮修改后重新编译的 Markdown/JSON 定向测试为 189/189 通过，git diff --check 通过。

重置 Computer Use JavaScript 会话后再次尝试窗口捕获，仍返回 FrameArrived timed out。应用内截图与键盘输入可用，真实鼠标注入和 60fps 屏幕呈现证据仍待补齐，没有将目标标为完成。
## 开发环境与产物

使用独立开发数据目录 .debug-data/md-editor，不修改真实待办数据。原生控件探针只编译进 Debug，源文件位于 tests/WinSticker.NativeMarkdownProbe/，通过环境变量显式启用。未新增应用依赖，未执行零售发布或删除发布产物。

每轮应用修改按仓库流程停止仓库可执行文件、构建规范 Debug、校验 WinSticker.dll 写入时间晚于构建开始，再启动新实例并核对路径。规范程序为 src/WinSticker/bin/Debug/net10.0-windows10.0.22621.0/WinSticker.exe。




## Goal 状态

完整目标保留，当前为 blocked。Windows.Graphics.Capture 在连续多轮验收中持续超时，鼠标注入因 coordinate input geometry is unavailable 失败，重置 JavaScript 会话后仍未恢复。实际鼠标路径和 60fps 屏幕呈现的最终证据受此环境阻塞，未标记完成，也未要求用户再次验收。代码、应用内截图、实际键盘事件、189/189 定向回归以及既有全量回归证据均已保留。

## 2026-10-25：补齐快捷窗口与桌面便签的真实入口

此前接入范围只覆盖待办备注，遗漏了快捷窗口便签和桌面随记的便签详情。现已补齐：QuickPanelNotesView 正文直接使用 MarkdownLiveEditor；MarkdownSourceEditor 保留原有名称、格式工具栏、查找与保存快捷键接口，内部改用同一原生内核。纯文本格式关闭实时 Markdown 装饰，保留原有语义。

快捷窗口加入同步原生缓冲后再切换/关闭/保存的边界；只保存有改动的便签，避免刷新时把旧缓存覆盖桌面更新。带 Markdown 正文的附件便签纳入同一列表，并带出内容格式与附件解析信息。桌面保存读取缓冲后再记录修改版本，保存等待期间的新输入也在结束编辑前再次同步，防止异步保存后用旧正文重绑。

最新针对 Markdown、快捷便签、设置切片及序列化契约的定向测试 221/221 通过。最近全量复跑 5856/5861，通过之外仍为相同 5 项已知失败；首次全量曾出现新增设置切片访问违规，已修正，剪贴板筛选用例曾一次失败，定向及最终全量复跑通过。最后新增的附件便签覆盖由定向回归验证。

实际控件探针确认以下六项为 true：快捷窗口使用原生编辑器、切换前保存旧便签、新便签不混入旧内容、桌面便签使用原生编辑器、立即保存捕获待同步输入、保存过程中继续输入不丢失。使用独立数据目录导入测试图片，已检查两个真实控件入口截图 native-markdown-quick-panel.png 与 native-markdown-desktop-note.png。规范 Debug DLL 写入时间晚于构建开始，当前仅运行规范仓库实例。

编辑内核接入不等于整个业务与 UI 统一。用户随后提出的命名、颜色、附件、工具栏、关闭/删除、收藏和跨入口同步一致性，已分析为独立的统一体验建议，见 2026-10-25-notes-unified-experience.md；尚未将该整套建议实现为新的共享便签面板。
