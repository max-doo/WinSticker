# Markdown 编辑会话统一（快捷窗口逻辑下沉）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 QuickCaptureSurfaceContent 的即时编辑保存状态机抽成共享组件 `MarkdownEditingSession`，桌面待办备注（TodoWidgetContent.DetailNotesAndSteps）改为消费同一组件，消除两套编辑保存逻辑。

**Architecture:** 新建纯逻辑的 `MarkdownEditingSession`（revision 脏跟踪、防抖自动保存、保存互斥、commit 重试循环、冲刷），定时器通过 `IMarkdownSessionTimer` 接口注入以保证可测试性。两个 surface 删除各自的定时器/信号量/revision 字段，把持久化动作放进 `SaveCore` 委托。桌面按决策保留显式进入编辑、Escape 不再回滚原文。

**Tech Stack:** WinUI 3 (net10.0-windows10.0.22621.0), xunit。

**Spec:** 本对话中的用户决策（2026-10-24）：① 保存状态机完全采用快捷窗口语义；② 桌面保留双击/编辑按钮进入编辑；③ Escape 取消回滚，改为冲刷保存后退出编辑；④ 失败反馈呈现各留（桌面内联重试条、快捷窗口 toast），逻辑统一在会话层。

## Global Constraints

- 测试命令（禁止默认 AnyCPU）：`dotnet test .\tests\WinSticker.Tests\WinSticker.Tests.csproj --no-restore --verbosity:minimal -p:Platform=x64`；绿的标准 = passed 数等于 total 数。
- 改应用代码后按 AGENTS.md 工作流：停掉仓库路径下的 WinSticker.exe → 构建 → 用 `C:\Users\simon\.agents\skills\winsticker-dev\scripts\dev-restart.ps1` 重启并核对运行路径。
- 工作树含用户未提交的无关改动（Todo 等 12 个文件）：**commit 时只 `git add` 本计划明确列出的文件**，绝不 `git add -A`。
- 提交信息禁止任何 assistant/Co-Authored-By 署名。
- 注释密度与语言跟随现有代码（英文注释为主）。

## Review Focus

- 保存 `await` 期间用户继续输入：commit 后必须再补一轮保存直到干净（revision 循环）——Task 1 测试钉住。
- 切换条目/关闭面板时有挂起的防抖保存：必须先冲刷且等待在途保存完成再切换——Task 1 钉 FlushAsync 行为，Task 2/3 接线。
- SaveCore 抛异常：不得死锁信号量、不得丢失 dirty 标记、需通知失败——Task 1 钉异常路径。
- 空白正文反复保存失败：自动保存不得无限重试循环（快捷窗口现有防循环语义）——Task 3 靠 SaveCompleted/SaveFailed 事件保留 re-arm 条件。
- 程序化写入编辑器文本（SetEditorText）不得被当作用户输入记脏——Task 2/3 各自保留 suppress 标志接线。

---

### Task 1: MarkdownEditingSession 纯逻辑组件（TDD）

**Files:**
- Create: `src/WinSticker/Services/MarkdownEditingSession.cs`
- Test: `tests/WinSticker.Tests/MarkdownEditingSessionTests.cs`

**Interfaces:**
- Consumes: 无（纯新代码）。
- Produces（Task 2/3 依赖，签名固定）:

```csharp
public interface IMarkdownSessionTimer : IDisposable
{
    event EventHandler? Tick;
    bool IsEnabled { get; }
    void Start();
    void Stop();
}

public sealed class DispatcherQueueSessionTimer : IMarkdownSessionTimer
// ctor(DispatcherQueue dispatcherQueue, TimeSpan interval)

public sealed class MarkdownEditingSession
{
    public MarkdownEditingSession(IMarkdownSessionTimer autosaveTimer);
    public bool IsSaving { get; }
    public bool HasUnsavedChanges { get; }
    public bool IsAutoSaveScheduled { get; }
    public Func<Task<bool>>? SaveCore { get; set; }   // 持久化委托；true=已落盘（含"无需保存"）
    public event EventHandler? SaveCompleted;         // SaveCore 返回 true
    public event EventHandler? SaveFailed;            // SaveCore 返回 false 或抛异常
    public void NotifyChanged();                      // MarkDirty + 重启防抖定时器
    public void MarkDirty();                          // 只标脏不排定时器（外观/格式变更用）
    public void ScheduleAutoSave();                   // 重启定时器（失败后 re-arm 用）
    public void Reset();                              // revision 清零、清脏、停定时器
    public void AbandonDirtyDraft();                  // editRevision = savedRevision（空白草稿放弃）
    public Task<bool> FlushAsync();                   // 脏或在途时 SaveAsync(false)
    public Task<bool> SaveAsync(bool completeEditing);
}
```

- [ ] **Step 1: 写失败测试**（`MarkdownEditingSessionTests.cs`，用 FakeTimer 手动触发 Tick）覆盖：
  1. `NotifyChanged` 置脏并启动定时器；Tick 触发 `SaveAsync(false)`，成功后清脏并停表。
  2. `SaveAsync(true)` 重试循环：SaveCore 第一次调用后、第二次调用前用回调让测试再 `NotifyChanged()`，断言 SaveCore 被调 2 次且最终 `HasUnsavedChanges == false`。
  3. `SaveAsync(true)` 时 SaveCore 返回 false：返回 false、脏保持、不再循环、`SaveFailed` 触发一次。
  4. SaveCore 抛异常：返回 false、信号量释放（后续 SaveAsync 可再进入）、`SaveFailed` 触发。
  5. `FlushAsync` 在干净且无在途时直接 true 不调 SaveCore；脏时等待在途保存完成（先启动一个挂起的 SaveCore，FlushAsync 在其完成后才返回）。
  6. `Reset`/`AbandonDirtyDraft` 语义。
  7. `IsAutoSaveScheduled` 跟随 FakeTimer.IsEnabled。
- [ ] **Step 2: 跑测试确认编译失败/红**
- [ ] **Step 3: 实现 `MarkdownEditingSession.cs`**：revision 用 long；`SaveAsync` 结构照搬 QuickCaptureSurfaceContent.SaveDetailAsync 的 gate + do..while + finally 语义（revisionAtStart 在调用 SaveCore 前捕获，成功后 `savedRevision = Math.Max(savedRevision, revisionAtStart)`、`HasUnsavedChanges = editRevision > revisionAtStart`；这部分数学从 surface 移入会话，SaveCore 内不再碰 revision）。`DispatcherQueueSessionTimer` 包一层 `DispatcherQueueTimer`（IsRepeating=false）。
- [ ] **Step 4: 跑测试全绿**
- [ ] **Step 5: Commit**（只 add 这两个文件）`feat: 抽出 MarkdownEditingSession 共享编辑保存状态机`

### Task 2: 桌面备注迁移到会话

**Files:**
- Modify: `src/WinSticker/Controls/WidgetContents/TodoWidgetContent.DetailNotesAndSteps.cs`（全文重写会话部分）
- Modify: `src/WinSticker/Controls/WidgetContents/TodoWidgetContent.AotPersistenceSmoke.cs`（改用会话 API 等待自动保存）
- Modify: `tests/WinSticker.Tests/AotStage5B4B2B2AContractTests.cs`、`tests/WinSticker.Tests/AotStage5B4B2B2B1ContractTests.cs`、`tests/WinSticker.Tests/MarkdownAndSplitterContractTests.cs`（同步源码扫描断言）

**Interfaces:**
- Consumes: Task 1 的 `MarkdownEditingSession` / `DispatcherQueueSessionTimer`。
- Produces: 行为不变的桌面备注编辑入口 `BeginNotesEditingAsync` / `SaveActiveNotesAsync(keepEditing)` / `PrepareForDetailSelectionChangeAsync`（AotPersistenceSmoke 依赖后两者名字，保留签名）。

- [ ] **Step 1: 先改契约测试**（红）：`AotStage5B4B2B2A` 中 `ScheduleNotesAutoSave`/`_notesAutosaveTimer`/`_notesOriginalText`/`_notesSaveGate.CurrentCount` 断言替换为会话等价物（如 `_notesSession.IsAutoSaveScheduled`、`_notesSession.HasUnsavedChanges`、无原文回滚）；`Interval = TimeSpan.FromMilliseconds(600)` 断言保留（600ms 常量仍在 DetailNotesAndSteps.cs 构造会话处）。
- [ ] **Step 2: 跑该测试文件确认红**
- [ ] **Step 3: 重写 DetailNotesAndSteps.cs**：删 `_notesAutosaveTimer`/`_notesSaveGate`/`_notesOriginalText`；新建 `_notesSession`（构造在 `InitializeDetailNotesAndSteps`，`DispatcherQueueSessionTimer(DispatcherQueue, 600ms)`）。`SaveCore` 委托 = 现有 `SaveActiveNotesAsync` 的落库核心：`EnsureDetailItemPersistedAsync` hook → `UpdateNotesAsync(itemId, DetailNotesEditor.Text)`，成功 true。`EditorTextChanged` → `_notesSession.NotifyChanged()`。`CancelRequested` → `_notesSession.FlushAsync()` 后 `EndNotesEditing()`（**不回滚原文**，删 `_notesOriginalText`）。`SaveFailed` → 显示 `DetailNotesSaveFailure` 并保持编辑态（替代原内联失败逻辑）。`SynchronizeDetailNotes`/`PrepareForDetailSelectionChangeAsync` 改用 `FlushAsync`。AotPersistenceSmoke 的等待点改用 `_notesSession.IsAutoSaveScheduled`/`IsSaving`。
- [ ] **Step 4: 跑三个契约测试文件 + TodoWidgetViewModelTests 全绿**
- [ ] **Step 5: Commit**（只 add 上述文件）`refactor: 桌面待办备注迁移到 MarkdownEditingSession——即时保存语义，Escape 不再回滚`

### Task 3: 快捷窗口迁移到会话

**Files:**
- Modify: `src/WinSticker/Controls/WidgetContents/QuickCaptureSurfaceContent.xaml.cs`（删除本地状态机字段与方法，接会话）

**Interfaces:**
- Consumes: Task 1 的 `MarkdownEditingSession`。
- Produces: 无对外新接口。

- [ ] **Step 1: 迁移**：删 `_detailSaveGate`/`_detailAutoSaveTimer`/`_isSavingDetail`/`_detailEditRevision`/`_detailSavedRevision`/`_detailHasUnsavedChanges`；`_detailSaveGate`→session.IsSaving、`MarkDetailDirty`→`MarkDirty`、`ScheduleDetailAutoSave`→`NotifyChanged`（注意保留 `_isCreatingDetail` 时不排定时器的条件：创建态 MarkDirty 即可，Commit 时统一落盘）、`SaveDetailAsync` 外壳→`SaveAsync(completeEditing)`、`SaveDetailCoreAsync` 保留全部业务分支（IsRecent/创建/附件/外观格式比对）但删掉其中所有 revision 赋值（会话负责）、`FlushPendingDetailSaveAsync` 用 `AbandonDirtyDraft`+`FlushAsync`、空白正文 re-arm 条件挪进 `SaveCompleted`/`SaveFailed` 处理器。`SetDetailEditorText` 的 `_suppressDetailEditorChanges` 保留（会话不接管编辑器文本）。
- [ ] **Step 2: 全量测试套件绿**（命令见 Global Constraints）
- [ ] **Step 3: 按 AGENTS.md 工作流重启验证**：dev-restart.ps1 门 + 报告运行 exe 路径；手验清单——桌面备注双击进入→打字→600ms 落盘→Escape 退出不回滚；快捷窗口新建/编辑/切换条目不丢字。
- [ ] **Step 4: Commit** `refactor: 快捷窗口详情编辑迁移到 MarkdownEditingSession——两处 surface 共用一套保存状态机`

### Task 4: 收尾验证

- [ ] **Step 1: 全量测试 + `simplify` 式自查**：确认两个文件中不再有各自实现的防抖/信号量/revision 残留（grep `_notesAutosaveTimer|_detailSaveGate|SemaphoreSlim.*notes|_detailEditRevision` 应为 0 命中，会话文件除外）。
- [ ] **Step 2: 汇报**：行为变化点（Escape 语义）+ 测试数 + 运行路径。
