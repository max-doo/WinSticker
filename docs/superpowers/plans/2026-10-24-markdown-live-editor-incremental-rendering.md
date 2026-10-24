# 便签实时 Markdown 编辑器——块级增量渲染 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 `MarkdownLiveEditor` 从"块级点击编辑 + 全量重建"升级为 Obsidian 式实时编辑体验：活动块即源码、其余块实时渲染，且任何一次编辑的开销只与**变化的内容**成正比，与文档总长无关（击键零序列化、块级 diff 复用、视口虚拟化）。

**Architecture:** 保持"单一真源是字符串"的模型（`MarkdownLiveDocumentModel`），在 UI 层引入四个机制移植 CodeMirror 6 的性能支柱：①击键后序列化防抖（150ms 空闲）；②`MarkdownBlockReconciler` 纯逻辑块协调器（前后缀匹配 diff，产出 复用/新增/删除 计划）；③渲染块视图缓存 + 协调式更新取代 `RebuildBlocks()` 全量重建；④`ItemsRepeater` 视口虚拟化。配套 `MarkdownDocumentView` 渲染零分配化与跨块键盘导航。

**Tech Stack:** WinUI 3 (net10.0-windows10.0.22621.0), Markdig, xunit。

**Spec:** 本对话中的用户决策（2026-10-24 调研报告）：①采用**原生块级增量渲染**路线，不引入 WebView2/CodeMirror（Native AOT 绑定风险、离线安装器约束、多便签窗口内存代价）；②效果对标 Obsidian 的核心交互（活动行源码、其余渲染、勾选直改源码），块内行级语法淡入列为后续增强；③性能优先——四条硬预算见 §8。

## Global Constraints

- 测试命令（禁止默认 AnyCPU）：`dotnet test .\tests\WinSticker.Tests\WinSticker.Tests.csproj --no-restore --verbosity:minimal -p:Platform=x64`；绿的标准 = passed 数等于 total 数。
- **当前基线有 5 个已知失败**（日历贴片在途工作所致，2026-10-24 记录）。每个 Task 的验收标准是：新增/修改的测试全绿，且不引入基线之外的失败。跑全量套件前先跑一次基线留档对照。
- 改应用代码后按 AGENTS.md 工作流：停掉可执行路径在本仓库下的 `WinSticker.exe` → 构建受影响项目 → 从当前 Debug 构建启动新实例并核对运行路径。规范本地开发可执行文件为 `src/WinSticker/bin/Debug/net10.0-windows10.0.22621.0/WinSticker.exe`。构建成功的唯一凭证是 `WinSticker.dll` 的写入时间晚于构建开始时间。
- 工作树含用户未提交的无关改动：**commit 时只 `git add` 本计划明确列出的文件**，绝不 `git add -A`。
- 提交信息禁止任何 assistant/Co-Authored-By 署名。
- 注释密度与语言跟随现有代码（英文注释为主）；所有新公开类型写 XML doc 注释。
- `MarkdownLiveEditor` 唯一消费方是 `TodoWidgetContent.xaml:941`（`DetailNotesEditor`，编辑态可见、`MarkdownEditingSession` 600ms 自动保存）。**不得破坏** `DetailNotesEditor_EditorTextChanged`、`FocusEditor`、`Text` DP、`PlaceholderText`、`IsReadOnly` 这五个对外契约。

## Review Focus

- 击键路径上**不得**出现：`Serialize()`、`Text` DP 赋值、`MarkdownDocumentView.Markdown` 赋值、`SplitIntoBlocks`（活动块自身的 split 除外，它是 O(活动块)）。Task 2 用注入 FakeTimer 的测试钉住。
- 协调式更新后，源码未变化的块的 `MarkdownDocumentView` **实例必须被复用**（引用相等），Task 1 的 planner 纯逻辑测试 + Task 3 的内部计数器钉住。
- 防抖序列化与 `MarkdownEditingSession` 600ms 自动保存的时序：SaveCore 读 `Text` 时必须已是新值（150ms < 600ms 且 FlushAsync 前有强制序列化点）。Task 2 测试钉住"定时器到点前 Flush 可拿到最新文本"。
- 虚拟化不得破坏勾选直改与点击激活：视口外回收的块滚回时若源码未变必须免重渲染（命中视图缓存）。Task 4 手验清单覆盖。
- IME：活动块是原生 `TextBox`，**不得**为任何编辑器功能添加 TextChanged 之外的文本拦截；IME 组字期间禁止触发协调/序列化以外的 UI 重建。

---

## 1. 背景与依据（为什么这样设计）

2026-10-24 的调研结论（完整报告见对话记录，结论已存项目记忆）：

1. **Obsidian 的实时预览是单一编辑器缓冲 + 装饰**，不是两个视图同步。四个性能支柱：单一字符串真源、增量解析（开销正比编辑量）、视口渲染（DOM 恒定）、事务化更新。
2. **WorkCat 的性能教训**（`D:\Project\WorkCat\src\WorkCat.App\Views\MarkdownEditor.cs`，RichTextBox/FlowDocument 基座）：
   - 每击键 `GetDocumentText` 遍历全部 Block 用 `TextRange.Text` 提取并重拼整篇字符串（O(N) + 重分配）；
   - `RefreshRenderedMarkdown` 对所有非活动段无差别 `Inlines.Clear()` 重渲染，每行每刷新 new Brush/CheckBox/Border；
   - FlowDocument 零 UI 虚拟化；
   - 为对抗基座用了 9 个重入标志（IME 一个场景约 6 个）。
   - **结构性结论：文本真源不能存在 UI 里，渲染必须块级 diff，列表必须虚拟化。**
3. WinSticker 现有资产已经避开了大部分陷阱（Markdig→对象树→原生 XAML 不走 HTML；块级模型；渲染状态门控），本计划是补齐剩下的四块：击键零序列化、块 diff、虚拟化、渲染零分配。

## 2. 现状盘点

| 文件 | 角色 | 现状与差距 |
|---|---|---|
| `src/WinSticker/Services/MarkdownLiveDocumentModel.cs` | 块模型（单一真源） | 已正确：块列表 + 活动块，空白行分裂/合并，fence 保护。差距：`UpdateActiveSource` 每击键触发 `DocumentChanged` → 上层立即 `Serialize()` |
| `src/WinSticker/Controls/MarkdownLiveEditor.cs` | 块级混合编辑器 | 交互模型正确。差距：`RebuildBlocks()` 激活/失焦全量重建所有块视图；`StackPanel` 无虚拟化；每击键 `Text = Serialize()` |
| `src/WinSticker/Controls/MarkdownDocumentView.cs` | 只读渲染器 | 已有 `MarkdownRenderState` 相等门、160ms 宽度防抖、渲染代数。差距：每次渲染约 25 处 `new SolidColorBrush/ColorConverter/FontFamily/Thickness` |
| `src/WinSticker/Services/MarkdownDocumentService.cs` | 解析 + 源操作 | 无需改动（`TryToggleTask` 直改 span 的设计保留） |
| `src/WinSticker/Services/MarkdownEditingSession.cs` | 600ms 防抖自动保存状态机 | 无需改动；依赖 `Text` DP 读值，本计划保证 SaveCore 执行时 Text 已新鲜 |
| `src/WinSticker/Services/MarkdownEditCommandEngine.cs` | 工具栏格式命令（纯文本变换） | Phase 2 接入活动块 TextBox |
| 消费方 | `TodoWidgetContent.xaml:941` `DetailNotesEditor` | 只读态 `MarkdownDocumentView` + 编辑态 `MarkdownLiveEditor` 切换，契约不变 |

## 3. 目标行为规范（UX Spec）

保留现有交互（用户已习惯，来自 WorkCat 移植模型），只增强：

1. **激活**：点击渲染块 → 该块原位变为源码 `TextBox` 获得焦点；其余块不变。`FocusEditor()` 激活第一块（打开即编辑入口）。
2. **提交**：失焦或 Escape → 活动块源码写回模型，该块重渲染（只重渲染受影响块）。Escape 不回滚（沿用 `MarkdownEditingSession` 决策）。
3. **块内分裂/合并**：活动块内输入空行 → 按模型规则分裂（fence 内空行不分裂）；删除分隔空行 → 合并。分裂/合并只影响相邻块。
4. **勾选直改**：渲染块上的任务勾选 → `TryToggleTask` 直改源码 3 字符 → 该块局部重渲染 → 冒泡 `EditorTextChanged`。只读态下 `AreTaskListsInteractive` 维持现有语义。
5. **跨块导航（新）**：活动块内 `↑` 在首行 → 提交并激活上一块，光标落其末行末尾；`↓` 在末行 → 提交并激活下一块，光标落其首行开头。文档边界处维持现状（不跳块）。
6. **IME**：完全交给 `TextBox` 原生处理，无自定义拦截。
7. **占位符**：现有 `ApplyPlaceholder` 逻辑不变。

## 4. 总体架构

```
用户击键(活动块 TextBox)
  └─ UpdateActiveSource(source)            纯字符串, O(活动块)
       └─ Model 内部标脏, 不再立即 DocumentChanged→Serialize
       └─ 击键路径终止(零序列化/零渲染/零协调)

150ms 输入空闲 (DispatcherQueueSessionTimer)
  └─ Serialize() → Text DP 赋值 → EditorTextChanged → MarkdownEditingSession.NotifyChanged
                                                    └─ 600ms 后 SaveCore(读 Text, 已新鲜)

失焦提交 / Escape / 勾选 / 外部 Load
  └─ 立即序列化(同步点)
  └─ MarkdownBlockReconciler.Plan(oldBlocks, newBlocks)
       └─ 复用: 源码未变的块 → 保留现有 MarkdownDocumentView 实例(引用相等)
       └─ 替换/新增/删除: 仅受影响块 → 建/换 MarkdownDocumentView
  └─ ItemsRepeater 视口内实例化, 视口外回收 → 缓存池(源码未变直接还原)
```

四条设计红线（实现与 review 时逐条对照）：

- **R1 单一真源**：任何时刻文档内容 = `MarkdownLiveDocumentModel`；UI 控件永不反推文本（不得出现从 XAML 提取文本再写回模型的路径）。
- **R2 击键零开销**：击键路径只做模型字符串更新；序列化/DP/渲染/协调一律推迟到空闲或提交。
- **R3 增量更新**：块列表变化经 Reconciler 协调；源码未变的块视图实例必须复用。
- **R4 恒定资源**：视口内块视图数量有上界（虚拟化 + overscan）；渲染用画刷/字体资源静态缓存。

## 5. 详细设计

### 5.1 MarkdownBlockReconciler（新纯逻辑类，本计划核心）

`src/WinSticker/Services/MarkdownBlockReconciler.cs`：

```csharp
public readonly record struct MarkdownBlockReuse(int OldIndex, int NewIndex);

public sealed record MarkdownBlockPlan(
    IReadOnlyList<MarkdownBlockReuse> Reuses,   // 升序, OldIndex 与 NewIndex 均单调递增
    int FirstChangedNewIndex,                    // 第一个未命中复用的新块下标; 无变化 = -1
    int LastChangedNewIndex,                     // 最后一个未命中复用的新块下标; 无变化 = -1
    bool Unchanged);                             // Reuses.Count == 新旧块数

public static class MarkdownBlockReconciler
{
    // blockKey: 块源码的稳定键(Ordinal 字符串相等即视为同块)。
    // 算法: 前缀公共 run + 后缀公共 run, 中段视为整体替换。
    // 退化为全量重建的条件与行为见 §5.1.1。
    public static MarkdownBlockPlan Plan(
        IReadOnlyList<string> oldBlocks,
        IReadOnlyList<string> newBlocks);
}
```

**算法（前缀/后缀匹配，非 LCS）**：

1. 从头找最长公共前缀（Ordinal 相等）；从尾找最长公共后缀（不与前缀重叠）。
2. 前缀与后缀之间的中段 = 变化区，全部按"替换"处理（`FirstChangedNewIndex`..`LastChangedNewIndex`）。
3. 空间 O(1)、时间 O(N)（每块一次 Ordinal 比较，块数通常 < 200）。

选择前缀/后缀而非 LCS 的理由：便签的真实编辑模式是"改一块 / 一块分裂成多块 / 多块合并成一块 / 勾一个框"，全部落在中段替换模式内；LCS 在块级粒度上收益趋零却引入 O(N·M) 与键冲突（两个内容相同的块）语义问题。

**§5.1.1 退化与边界**：

- 前缀 + 后缀覆盖全部新块（纯删除中段）→ `Reuses` 覆盖剩余，Unchanged=false。
- 新旧完全相等 → `Unchanged=true`，调用方跳过一切 UI 操作（含 `Text` 已同步时的重复赋值）。
- 块数超过阈值（`MaxBlocksForReconcile = 2000`，防御性）→ 返回空 Reuses 的全替换计划（等价全量重建，不炸）。
- 该类不做任何 UI 操作、不持有状态，100% 可单元测试。

### 5.2 击键序列化防抖

改造 `MarkdownLiveEditor`：

- 复用 `DispatcherQueueSessionTimer`（构造注入 `IMarkdownSessionTimer`，interval 150ms，`IsRepeating=false`；测试注入 FakeTimer——`MarkdownEditingSessionTests` 已有现成模式）。
- `_model.DocumentChanged` 不再立即 `Text = Serialize()`，改为：**停表再启表**（重启 150ms 防抖），并置 `_hasPendingSerialization = true`。
- 定时器到点 → 执行 `FlushPendingSerialization()`：`Text = _model.Serialize()` + `EditorTextChanged` + `ApplyPlaceholder`。
- **三个强制同步点**（绕过防抖立即冲刷，先停表）：
  1. `Deactivate()`/`ActivateBlock()` 提交时（结构已变，需要协调）；
  2. `OnTextPropertyChanged` 外部赋值路径（`_isSynchronizing` 保护已有）；
  3. 新增 `public string FlushAndGetText()`：停表、若 `_hasPendingSerialization` 则立即序列化并返回 `Text`。**`MarkdownEditingSession.SaveCore` 的读取点从 `DetailNotesEditor.Text` 改为先调它**（TodoWidgetContent.DetailNotesAndSteps.cs 一处改动），保证 600ms 自动保存和 FlushAsync 永远拿到最新文本，不依赖 150ms 先到的时间假设。
- 防抖期间 `EditorTextChanged` 尚未触发 → `MarkdownEditingSession` 的 600ms 表也尚未启动。时序链：击键 → 150ms（渲染/序列化）→ 600ms（保存）。总延迟 ≤750ms，远低于人的保存预期，且与现状（600ms）体感一致。

### 5.3 协调式块更新（取代 RebuildBlocks）

- 新增私有字段：`private readonly List<MarkdownDocumentView> _renderedViews = [];`（索引与新块列表对齐；活动块位置为 `null` 槽位）。
- `_renderedViews` 与 `_model.Blocks` 的对齐由 Reconciler 计划驱动：
  1. `Plan(_lastCommittedBlocks, _model.Blocks)`（`_lastCommittedBlocks` 为上次协调完成时的块快照，`List<string>` 拷贝）；
  2. `Unchanged` → 直接返回；
  3. 按计划重建 `_renderedViews`（复用项搬运现有实例引用；变化区索引置 `null` 待实现化）；
  4. 同步增删 `ItemsRepeater.ItemsSource`（见 §5.4），让视口内的变化块实例化新视图、其余复用。
- `RefreshRenderedBlock(index)`（勾选路径）保留，但前提改为"该块视图已存在且源码变化仅此一块"——`TryToggleTask` 后走与提交相同的协调入口统一处理。
- 活动块视图（`TextBox`）不进缓存：激活即建、提交即弃。它的 `TextChanged` 只喂模型（现状）。
- **视图缓存与虚拟化的关系**：`_renderedViews` 持有的是"逻辑视图槽位"。为满足 R4，槽位对象分两态——`Materialized`（在视口内，真实 `MarkdownDocumentView` 挂在可视树）与 `Detached`（滚出视口被回收，仅保留 `{ string Source; }` 轻量记录，滚回时若 `Source` 与当前块源码 Ordinal 相等则重建视图一次即可命中 `MarkdownDocumentView` 内部的 `MarkdownRenderState` 门？——**不**：控件实例没了就一定重建。因此回收策略是"只回收视口外超过 `DetachedViewBudget`(默认 12) 个的视图"**：视口 ± overscan 之外多缓存 12 个最近使用视图，超预算的丢实例保源码。便签文档块数有限（≤2000），256KB 上限下这是内存与重建次数的平衡点；`DetachedViewBudget` 暴露为 `internal const` 便于测试改写。

### 5.4 ItemsRepeater 虚拟化

- `_blockList`（StackPanel）→ `ItemsRepeater` + `StackLayout`，外层保留现有 `_scroller`（ScrollViewer）。ItemsSource = 块视图模型列表 `IReadOnlyList<MarkdownBlockItem>`：

```csharp
// 嵌套私有类型: 协调器输出到 repeater 的条目
private sealed class MarkdownBlockItem
{
    public required string Source { get; init; }     // R1: 真源在模型, 此处仅展示快照
    public required bool IsActive { get; init; }
    public int BlockIndex { get; init; }
}
```

- `ItemTemplate` 用 `DataTemplateSelector` 两个模板：`RenderedBlockTemplate`（`MarkdownDocumentView`，`Markdown={Binding Source}`，`AreTaskListsInteractive`/`FontSize`/`AllowRemoteImages` 绑定转发）与 `ActiveEditorTemplate`（`TextBox`）。XAML 资源放进 `MarkdownLiveEditor`（当前为纯代码控件，需新增 `MarkdownLiveEditor.xaml` 或改用代码构造 `DataTemplate`——**选后者**：该控件历史上纯代码建 UI，避免引入 xaml 编译面；用 `XamlBindingHelper`/代码 `DataTemplate` 均可，实现时取最小路径并在 PR 说明）。
- `ItemsRepeater` 的元素回收回调（`ElementClearing`/`ElementPrepared`）里维护 §5.3 的 Materialized/Detached 记账：`ElementPrepared` 时若该 BlockIndex 已有 Detached 前的实例仍在缓存 → 直接复用该实例作为 element 内容（或最小代价：让 repeater 自己回收模板控件，我们的缓存只保证 `MarkdownDocumentView.Markdown` 相等门命中——**注意**：`MarkdownRenderState.Markdown` 相等即跳过重渲染，所以 repeater 回收模板控件后重设 `Markdown` 为相同值时解析已被门挡住，真正的开销只剩控件树本身。**最终策略以此为准**：repeater 原生回收 + `Markdown` 相等门，自定义 `DetachedViewBudget` 缓存仅用于 `ItemsRepeater` 之外仍被 `_renderedViews` 引用的协调记账，二者在 Task 4 实现时统一，文档以"`ElementPrepared` 重设相同 `Markdown` 必须命中 `MarkdownRenderState` 门零重渲染"为验收线）。
- **高度测量**：`DetailNotesEditor` 有 `MinHeight=116 MaxHeight=360`，`MarkdownLiveEditor` 自带 `_scroller`。ItemsRepeater + ScrollViewer + StackLayout 的虚拟化要求 scrollview 内容高度自然增长，`MaxHeight` 约束在外层——现有结构满足（`_scroller` 已是内容宿主）。验证点写入手验清单。
- **焦点**：回收活动块 `TextBox` 会丢焦点——活动块**不参与回收**（`IsActive` 条目所在 element 上调 `ItemsRepeater`… 实现上以"活动块期间锁 `VirtualizationMode` 无关，改为活动期将 repeater 的 `MaximumItemsPerLine`… WinUI ItemsRepeater 无该开关；正确做法：接受回收可能性，`ElementPrepared` 时若 `IsActive` 且焦点丢失则重新 `Focus` + 恢复光标到 `MarkdownLiveEditor` 记录的 `_pendingCaretOffset`"。活动块通常在视口内（用户正在打字），回收它属于罕见路径，恢复逻辑兜底即可。

### 5.5 MarkdownDocumentView 渲染零分配化

- 把渲染路径中的 `new SolidColorBrush(...)`、`ColorConverter.ConvertFromString`、`new FontFamily(...)`、重复 `new Thickness` 提为 `static readonly` 字段（WinUI 无 `Freeze`，共享不可变实例即可；`SolidColorBrush` 可跨控件共享）。语义色（代码块背景/边框、引用条、标题强调色等约 25 处）按 (Light/Dark) 两套静态化。
- **主题失效已有**：`ActualThemeChanged → InvalidateRenderForAppearanceChange("theme")` 会触发重渲染，静态画刷后该路径依然正确（重渲染时取当前主题的静态实例）。`UpdateForegrounds` 里跟随宿主前景的共享画刷逻辑**保持不动**（注释明确说明它支撑自定义颜色热更新，这是刻意设计）。
- 验收线：渲染一个 100 块文档两次（第二次 `Markdown` 不同值），第二次渲染的 GC 分配显著低于第一次（手验 + 计数器观察，不写进自动化）。

### 5.6 跨块键盘导航

- `CreateActiveEditor` 的 `KeyDown` 追加：`↑` 且光标在首行（`TextBox.SelectionStart` 之前无 `\n`，用 `Text.LastIndexOf('\n', SelectionStart - 1)` 判断）→ `args.Handled = true`，记 `_pendingCaretOffset = 尾`，`CommitAndActivate(BlockIndex - 1)`；`↓` 且光标在末行 → 对称。
- `CommitAndActivate`：失焦提交当前块 → 协调 → 激活目标块 → `Focus` + `Select(offset, 0)`。块边界（第一块按 `↑` / 最后一块按 `↓`）不处理，事件穿透维持原生行为。
- 光标落点启发式：上一块末尾 / 下一块开头（不做渲染偏移↔源码偏移映射——WorkCat 的 `MapRenderedOffsetToRaw` 复杂度大、收益小，明确不做）。

### 5.7 性能计数器

```csharp
internal readonly record struct MarkdownLiveEditorPerformanceSnapshot(
    int KeystrokeModelUpdateCount,
    TimeSpan SerializeDuration,       // 累计
    int SerializeCount,               // 应 ≈ 每 150ms 窗口 ≤1 次
    int ReconcileCount,
    TimeSpan ReconcileDuration,
    int BlocksRenderedTotal,          // 协调后实际 (重)渲染的块数, 应 ≈ 变化块数
    int ViewReuseHitCount);           // MarkdownRenderState 门命中次数(MarkdownDocumentView 侧已有渲染代数, 加只读计数)
```

`internal` + `InternalsVisibleTo`（测试项目已有该约定则沿用，无则在 csproj 加）。Debug 下不自动上报，仅供手验与测试断言（测试只断言**次数**类计数，绝不断言耗时——避免抖动）。

## 6. 测试策略

| 层 | 文件 | 内容 |
|---|---|---|
| 纯逻辑（主力） | `tests/WinSticker.Tests/MarkdownBlockReconcilerTests.cs`（新） | 前缀/后缀匹配、单块编辑、分裂（1→N）、合并（N→1）、纯删除、全等、空文档、超阈值退化、Reuses 单调性 |
| 纯逻辑 | `MarkdownLiveDocumentModelTests.cs`（扩） | 补 fence 内空行、`\r\n` 归一、连续空行、首尾空行的 split 边界（现有用例外补漏） |
| 防抖（注入 FakeTimer） | `MarkdownLiveEditorSerializationTests.cs`（新） | ①击键 N 次 → SerializeCount==0；②FakeTimer 触发 → 1 次；③`FlushAndGetText` 在定时器到点前返回最新文本；④同步点（Deactivate/外部 Text 赋值）立即冲刷。控件不可无 UI 实例化 → **把防抖逻辑抽成纯类 `MarkdownSerializationScheduler`**（构造 `IMarkdownSessionTimer` + `Func<string> serialize` 回调，暴露 `NotifyChanged/Flush`），测试打纯类，`MarkdownLiveEditor` 只做接线 |
| 契约 | `MarkdownAndSplitterContractTests.cs`（扩） | 源码扫描断言：`MarkdownLiveEditor` 内不得出现击键路径直连 `Serialize`（`Model_DocumentChanged` 方法体中无 `Text =` 赋值）；`RebuildBlocks` 方法已删除 |
| 集成 | `TodoWidgetViewModelTests` + 现有 3 个 markdown 测试文件 | 全量回归，防消费方契约破坏 |

## 7. 任务分解

> 每个 Task 独立可交付、独立 commit。Task 1/2 无 UI 依赖先行；Task 3 是枢纽；Task 4 依赖 3；5/6 可并行；7 收尾。

### Task 1: MarkdownBlockReconciler 纯逻辑（TDD）

**Files:** Create `src/WinSticker/Services/MarkdownBlockReconciler.cs`；Create `tests/WinSticker.Tests/MarkdownBlockReconcilerTests.cs`

- [ ] Step 1: 写失败测试覆盖 §5.1 全部边界（含 `Reuses` 双下标单调递增的属性断言）
- [ ] Step 2: 跑测试确认红（编译失败即红）
- [ ] Step 3: 实现前缀/后缀匹配算法（Ordinal 比较，`MaxBlocksForReconcile=2000` 退化）
- [ ] Step 4: 跑测试全绿
- [ ] Step 5: Commit `feat: 新增 MarkdownBlockReconciler 块级协调器——前后缀匹配产出块复用计划`

### Task 2: 序列化防抖调度器（TDD）

**Files:** Create `src/WinSticker/Services/MarkdownSerializationScheduler.cs`；Create `tests/WinSticker.Tests/MarkdownSerializationSchedulerTests.cs`（复用 `MarkdownEditingSessionTests` 的 FakeTimer 模式）

接口：

```csharp
public sealed class MarkdownSerializationScheduler : IDisposable
{
    public MarkdownSerializationScheduler(
        IMarkdownSessionTimer idleTimer,     // 150ms, one-shot
        Func<string> serialize,              // () => _model.Serialize()
        Action<string> publish);             // text => { Text = text; EditorTextChanged?.Invoke(); ... }
    public void NotifyChanged();             // 停表→启表, 置 pending
    public void Flush();                     // 停表, pending 则 serialize+publish 一次, 清 pending
    public bool HasPending { get; }
}
```

- [ ] Step 1: 失败测试：①Notify N 次不 publish；②Tick 到点 publish 恰一次且清 pending；③Flush 在到点前拿到最新 `serialize()` 返回值；④Flush 干净态为 no-op；⑤Dispose 后 Tick 不 publish
- [ ] Step 2: 红 → 实现 → 绿
- [ ] Step 3: Commit `feat: 新增 MarkdownSerializationScheduler——击键序列化 150ms 空闲防抖`

### Task 3: MarkdownLiveEditor 接入防抖与协调式更新

**Files:** Modify `src/WinSticker/Services/MarkdownLiveDocumentModel.cs`（`UpdateActiveSource`/`TryToggleTask` 的 `DocumentChanged` 语义不变，由上层决定节流）；Modify `src/WinSticker/Controls/MarkdownLiveEditor.cs`；Create `tests/WinSticker.Tests/MarkdownLiveEditorContractTests.cs`；Modify `tests/WinSticker.Tests/MarkdownAndSplitterContractTests.cs`（如扫描断言重叠则合并）

- [ ] Step 1: 契约测试先行（红）：①源码扫描 `MarkdownLiveEditor.cs` 无 `RebuildBlocks` 方法；②`Model_DocumentChanged` 方法体无 `Text =`/`Serialize()` 直呼；③存在 `FlushAndGetText` 公有方法
- [ ] Step 2: 实现：接 `MarkdownSerializationScheduler`（默认 `DispatcherQueueSessionTimer(DispatcherQueue.GetForCurrentThread(), 150ms)`，构造函数可选注入保测试）；`RebuildBlocks` → `ReconcileBlocks`（§5.3：`_lastCommittedBlocks` 快照 + Reconciler 计划 + `_renderedViews` 搬运；此 Task 仍用 `StackPanel` 平铺——**虚拟化留 Task 4**，先锁定协调正确性）；三个同步点接 `Flush`
- [ ] Step 3: `TodoWidgetContent`（`DetailNotesAndSteps.cs`）SaveCore 读取点改 `FlushAndGetText()`；跑 `TodoWidgetViewModelTests` + 三个 markdown 测试文件
- [ ] Step 4: 手验：备注打字→停→150ms 后勾选联动正常；双击进入/Escape 退出；切换条目不丢字（FlushAsync 路径）
- [ ] Step 5: Commit `refactor: MarkdownLiveEditor 改用防抖序列化与块级协调更新——击键零序列化, 块视图按计划复用`

### Task 4: ItemsRepeater 视口虚拟化

**Files:** Modify `src/WinSticker/Controls/MarkdownLiveEditor.cs`；Modify `tests/WinSticker.Tests/MarkdownLiveEditorContractTests.cs`

- [ ] Step 1: 契约测试（红）：源码扫描存在 `ItemsRepeater` 且 `_blockList` 不再是 `StackPanel` 类型字段
- [ ] Step 2: 实现 §5.4：`MarkdownBlockItem` + `DataTemplateSelector`（代码构造模板）+ `ElementPrepared/Clearing` 记账；`Markdown` 相等门验收线（`ElementPrepared` 重设相同 Markdown 零重渲染——通过 `MarkdownDocumentView` 暴露的 `internal` 渲染计数断言，若 UI 层无法单测则以代码评审 + 手验替代并在此注明）
- [ ] Step 3: 手验：256KB 上限文档（构造 300+ 块）滚动 60fps；滚动中勾选不误触；活动块打字期间快速滚动再滚回，光标与文本完好
- [ ] Step 4: Commit `feat: MarkdownLiveEditor 块列表改 ItemsRepeater 视口虚拟化`

### Task 5: MarkdownDocumentView 渲染零分配化

**Files:** Modify `src/WinSticker/Controls/MarkdownDocumentView.cs`；Modify `tests/WinSticker.Tests/MarkdownAndSplitterContractTests.cs`（如该文件对此有扫描断言）

- [ ] Step 1: 语义色/字体/Thickness 静态化（Light/Dark 两套），`UpdateForegrounds` 共享画刷逻辑保持不动并加注释说明为何例外
- [ ] Step 2: 全量测试 + 手验明暗主题切换、便签自定义文字颜色仍热更新
- [ ] Step 3: Commit `perf: MarkdownDocumentView 渲染资源静态缓存——消除每渲染 Brush/FontFamily 分配`

### Task 6: 跨块键盘导航

**Files:** Modify `src/WinSticker/Controls/MarkdownLiveEditor.cs`；Modify `tests/WinSticker.Tests/MarkdownLiveEditorContractTests.cs`（扫描 `CommitAndActivate` 存在性与 `VirtualKey.Up/Down` 处理）

- [ ] Step 1: 实现 §5.6（含 `_pendingCaretOffset` 兜底恢复）
- [ ] Step 2: 手验：三块文档，块 2 首行 `↑` → 块 1 末尾；末行 `↓` → 块 3 开头；边界块不跳；fence 块内 `↑↓` 仍是文本内移动
- [ ] Step 3: Commit `feat: MarkdownLiveEditor 活动块支持上下键跨块导航`

### Task 7: 收尾验证与性能预算复核

- [ ] Step 1: 全量测试（对照基线：新增全绿 + 失败数不多于基线 5 个）
- [ ] Step 2: 按 AGENTS.md 重启工作流验证并报告运行 exe 路径
- [ ] Step 3: 性能预算复核（§8）——用性能计数器在 256KB 样例上手测记录数字，写入本文件 §8 表格下方
- [ ] Step 4: Commit `docs: 便签实时 Markdown 编辑器块级增量渲染实测数据与收尾`（如仅文档更新）

### Phase 2（明确不在本计划内，另立计划）

- 快捷窗口 `MarkdownSourceEditor` → `MarkdownLiveEditor` 迁移 + `MarkdownEditCommandEngine` 工具栏命令接入活动块 TextBox。
- 活动块内部行级语法淡入（`RichEditBox` 自定义呈现路线）。
- 大文档（>32KB）线程池后台解析 + 版本号护栏。

## 8. 性能预算（验收线）

| 指标 | 预算 | 测法 |
|---|---|---|
| 活动块击键（模型更新） | < 1ms，零 Serialize/零渲染/零协调 | 计数器：击键 1000 次窗口内 SerializeCount==0 |
| 空闲序列化（150ms 窗口） | ≤1 次 Serialize | 计数器 |
| 提交/勾选的协调 | 只重渲染变化块：勾选 1 块、编辑 1 块 → BlocksRenderedTotal 增量 == 1 | 计数器 |
| 激活/失焦（无结构变化） | 视图实例全部复用（ViewReuse 命中，新建 0） | 计数器 + 评审 |
| 256KB / 300 块文档滚动 | 60fps，视口外块不实例化 | 手验 |
| IME 组字 | 无可感知延迟、无光标跳动 | 手验（中文/日文输入） |

实测记录（Task 7 填写）：____________

## 9. 风险与缓解

| 风险 | 缓解 |
|---|---|
| ItemsRepeater 在外层 MaxHeight=360 + 内层 ScrollViewer 组合下的测量异常（高度坍缩/双滚动条） | Task 4 手验第一项；备选退路：保留 ScrollViewer+StackLayout 但 `VirtualizingStackPanel`（WinUI 无此类型，实际退路=回退 StackPanel 平铺，协调器收益仍在） |
| 活动块 TextBox 被回收导致焦点/光标丢失 | §5.4 `_pendingCaretOffset` 兜底；活动块几乎总在视口内，属罕见路径 |
| 代码构造 DataTemplate 的绑定开销/兼容问题 | 模板内控件少、绑定 3 个属性，量级可忽略；不行就补 .xaml（计划允许，PR 说明） |
| 防抖 150ms 与外部 `Text` 绑定竞态（外部在 pending 期间赋同名值） | 现有 `_isSynchronizing` + Ordinal 相等门已覆盖；`FlushAndGetText` 强制同步点兜底 SaveCore |
| 协调器把"内容相同的两个块"误判复用导致视图错位 | 前缀/后缀算法按 Ordinal 全串比较（非哈希），不存在哈希冲突；相同内容块本来就渲染等价，复用语义正确 |
| 契约测试源码扫描脆（重构改名即碎） | 这是仓库既有约定（`MarkdownAndSplitterContractTests` 先例），改名时同步更新断言即可 |
