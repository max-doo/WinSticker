# Todo 日历视图与独立日历贴片 — 设计文档

- 日期：2026-10-24
- 状态：**定稿，待实现**
- 关联：`TodoWidgetContent`（已统一的 Todo 组件）、`TodoRepository`（单写路径）、`GlanceWidgetContent`（贴片形态参考）
- 本文档已经过一轮独立构建者审查，审查提出的阻塞问题已全部裁决并写入正文（见 §13 决策记录）

## 1. 意图与原因

用户希望：

1. **在 Todo 里加日历功能**，日历视图里显示"日程"；
2. **日历可以作为独立桌面贴片**常驻，并且**可交互**。

关键语义澄清（用户已确认）：

- **"日程"就是待办的到期时间**（`TodoItem.DueDate`），不引入独立的日历事件实体。日历不是新数据，而是现有待办的**另一种按日期分组的视图**。
- **贴片完全可交互**：不只是看，要能勾选完成、点格子新建、拖任务改日期。
- **循环任务按方案 A**：日历只显示 Todo 列表里真实存在的条目，不做虚实例展开。
- **日历是独立组件**：新增 `WidgetKind.Calendar`，与 Todo 贴片平级。

为什么这么做：待办的本质是"在某个时间点要做的事"。列表视图回答"我还有什么没做完"，日历视图回答"我这几天忙不忙、什么时候该做"。两者是同一份数据的两个透镜——这是 TickTick / Todoist 的共识做法。

## 2. 目标

| # | 目标 | 验收标准 |
|---|------|---------|
| G1 | Todo 组件内提供月历视图 | 列表/月历一键切换；点日期筛选当天任务；格子内可见任务标题与完成状态 |
| G2 | 独立日历贴片（`WidgetKind.Calendar`） | 设置中可添加；可拖拽移动/缩放；常驻桌面；与 Todo 数据实时同步 |
| G3 | 贴片可交互 | 勾选完成、空格双击新建（到期日=该日）、拖任务到日期改到期、右键菜单、滚轮切月 |
| G4 | 日历 UI 只有一套代码 | `TodoCalendarContent` 被 Todo 视图和日历贴片共同复用，无第二套月视图渲染 |
| G5 | 所有写操作的行为与列表视图一致 | 完成/改期/新建的语义（循环生成、提醒重置、锚点平移）**只有一份实现**，日历与列表共用 |

## 3. 非目标

- **不引入日历事件实体**（无 `CalendarEvent` 模型、无起止时间字段）。只投影 `TodoItem.DueDate`。
- **不实现循环任务在日历上的虚实例展开**（方案 A）：日历只显示 Todo 列表里真实存在的条目。
- **不集成外部日历**（Google Calendar / Outlook / ICS 订阅）。
- **不实现周视图/日视图/时间轴（time blocking）**。首版只做月视图 + 当日列表。
- **不改变现有 Todo 列表页的功能与布局**（只增加一个视图切换入口）。

## 4. 反目标（即使做出来也算失败）

- 日历维护自己的一份任务副本 → 贴片改了但 Todo 列表没变（正是上次消除的 bug）。
- 日历自己重写一遍完成/循环/提醒逻辑 → 与列表视图行为分叉。
- 月格子里塞整段任务文本导致格子撑爆、月份高度跳动。
- 贴片在任务多时滚动卡顿或出现闪烁。
- 新增 `WidgetKind` 后，删除一个日历贴片把 Calendar 总开关关掉、或让启动恢复抛异常。

## 5. 调研依据

| 发现 | 来源 | 对本设计的影响 |
|------|------|-------------|
| Todoist 日历视图被评为"弱"：只显示有截止日的任务、不能拖动、与列表割裂 | techwiseinsider, zapier | 日历必须可编辑可拖动，不能是只读装饰 → G3 |
| TickTick 的可拖拽日历是显著优势；支持任务与日历同屏 | zapier, ellieplanner | 拖任务落到日期作为核心交互 |
| 月视图密度：显示 1~3 条 + "+N 更多"溢出；分层（月度总览做导航，详情做规划） | Muzli | 格子内容上限 + 溢出计数 |
| Rainlendar 经典形态 = 月历网格 + "Today" 当日列表；关键特性含窗口穿透、锁定位置 | Rainlendar 文档 | 贴片布局；"今日"高亮与当日列表 |
| 桌面贴片价值 = 开机即得当天上下文 | How-To Geek | 贴片默认显示当月 + 选中今天 |
| WinUI `CalendarView` 的日期格**不可** re-template、不可放任意内容 | Microsoft Learn；CommunityToolkit/Graph-Controls#48 | **放弃 `CalendarView`，自绘月网格** |

## 6. 架构

三层，自下而上：

```
┌─────────────────────────────────────────────────────────┐
│ UI 出口（复用同一个 Content 组件）                         │
│  ├─ Todo 组件内：列表 ⇄ 月历 切换                          │
│  └─ 独立日历贴片：WidgetKind.Calendar                      │
├─────────────────────────────────────────────────────────┤
│ TodoCalendarContent (XAML UserControl)                    │
│  └─ TodoCalendarViewModel                                 │
│       ├─ MonthAnchor / SelectedDate                       │
│       ├─ ObservableCollection<TodoCalendarDayCell>         │
│       ├─ 持有 TodoWidgetViewModel _todo（唯一写入口）       │
│       └─ TodoTargetResolver → TodoWidgetStore              │
├─────────────────────────────────────────────────────────┤
│ TodoCalendarProjection (纯静态逻辑，无 UI 依赖，可单测)      │
│  ├─ BuildMonthGrid(month, weekStart) → DayCell[42]         │
│  ├─ BucketByLocalDate(items, tz)                          │
│  └─ AssignSlots(items, maxVisible) → (visible, overflow)   │
└─────────────────────────────────────────────────────────┘
```

**数据源决策：日历不拥有自己的 store。** 它读取"当前启用的 Todo widget"的 store，经 `TodoTargetResolver.Resolve(settingsService)` 统一解析（规则：第一个 `WidgetKind == Todo && !IsDisabled && 未删除` 的配置；缺省 `"todo"`）。三处共用同一解析器：快捷面板（替换现有 `GetActiveTodoWidgetId`）、日历贴片的 provider、Todo 组件内日历视图。

**解析失败即抛异常，绝不静默回退。** `TodoWidgetContentProvider` 现有的 `(context.TodoStoreFactory ?? (w => new TodoWidgetStore(w.Id)))(config)` 对 Calendar 是**错误示范**——那会用 Calendar 自己的 `config.Id` 建一个空 store，正好命中反目标 1。日历 provider 必须显式走 target resolver。

**生命周期**：Calendar 贴片的 `TodoCalendarViewModel` 订阅 `SettingsService.SettingsChanged`（已有事件，若不存在则在 adapter 层重新解析）；当 Todo 被禁用/删除时，日历显示空态并重新解析，不崩溃、不指向孤儿 store。

**月网格固定 6 行 × 7 列 = 42 格**，月份切换不改变贴片高度。高度不足时**不压缩行数**，而是减少每格可见 chip 数（最小 0 条，只显示日期 + `+N`）。

## 7. 组件设计

### 7.1 `TodoCalendarProjection`（新文件，纯函数）

```csharp
public static class TodoCalendarProjection
{
    public const int WeeksPerMonth = 6;
    public const int DaysPerWeek = 7;
    public const int CellCount = WeeksPerMonth * DaysPerWeek;

    public static DayOfWeek ResolveWeekStart(CultureInfo culture);
    public static IReadOnlyList<DateOnly> BuildMonthGrid(int year, int month, DayOfWeek weekStart);
    public static IReadOnlyDictionary<DateOnly, IReadOnlyList<TodoItem>> BucketByLocalDate(
        IEnumerable<TodoItem> items, TimeZoneInfo tz);
    public static (IReadOnlyList<TodoItem> visible, int overflow) AssignSlots(
        IReadOnlyList<TodoItem> items, int maxVisible);
}
```

要点：

- **日期归属按本地时区**：`TimeZoneInfo.ConvertTime(due, tz)` 取本地 `DateOnly`，避免 UTC 跨日错位。
- **排序**：未完成在前 → `DueDate` 时间升序 → `SortOrder` → `CreatedAt`。
- **无 `DueDate` 的任务不进入日历**（它们在列表视图里）。
- **不接收 `now` 参数**："今天"的判定属于 VM 职责（可注入时钟），投影层保持无时钟、纯确定。
- `ResolveWeekStart` 用 `culture.DateTimeFormat.FirstDayOfWeek`；测试同时覆盖 `InvariantCulture` 与显式构造的 `zh-CN`/`en-US`，避免依赖机器区域设置。

### 7.2 `TodoCalendarViewModel`（新文件）

```csharp
public sealed partial class TodoCalendarViewModel : ObservableObject, IDisposable
{
    private readonly TodoWidgetViewModel _todo;   // 唯一写入口 + 内存权威
    private readonly TimeZoneInfo _tz;
    private readonly Func<DateTimeOffset> _now;

    [ObservableProperty] private DateOnly _monthAnchor;    // 构造中必须用 _now() 初始化
    [ObservableProperty] private DateOnly? _selectedDate;

    public ObservableCollection<TodoCalendarDayCell> Cells { get; }
    public ObservableCollection<TodoItemViewModel> SelectedDayTasks { get; }
    public string MonthTitle { get; }
    public bool IsEmptyStateVisible { get; }      // Todo 被禁用/无 todo 时

    public Task InitializeAsync();
    public Task ShowMonthAsync(DateOnly month);
    public Task SelectDateAsync(DateOnly date);
    public Task MoveTaskToDateAsync(string taskId, DateOnly date);
    public Task CreateTaskOnDateAsync(DateOnly date, string text);
    public Task ToggleCompletedAsync(string taskId);
    public Task GoToTodayAsync();
    public void Dispose();
}
```

#### 写路径（定案，解决审查报告的阻塞项 B1）

**不复制、也不抽取任何写逻辑；全部委托给内部持有的 `TodoWidgetViewModel`。**

依据（已在源码核实）：

- `SetCompletedAsync(itemId, bool)`（`ItemOperations.cs:113`）内已包含：循环任务完成后 `TodoRecurrenceService.TryCreateNextOccurrence` 生成下一次并插入 `Items`（`:140-147`）、取消完成时 `FindGeneratedOccurrence` 回收已生成实例（`:151-156`）、清 snooze/generated 标记。
- `SetDueDateAsync(itemId, DateTimeOffset?)`（`:325`）内已包含：`Recurrence.AnchorDueDate` 平移（`:337-340`）、`DueDate=null` 时清 `ReminderOffsetMinutes`（`:351-354`）、清 reminder/snooze 时间戳（`:360-363`）、无循环时清 `RecurrenceSeriesId`/`GeneratedNextItemId`（`:355-359`）。
- `AddItemAsync(text, isImportant, dueDate)`（`:50`）。
- 这三个方法改完都调 `SaveAsync()` → `Repository.MergeAsync(snapshot, this)`；而 `TodoWidgetViewModel` 在构造时订阅了 `StoreChanged` 并在外部写入时经 `ReloadFromRepositoryAsync` 全量重载 `Items`（`TodoWidgetViewModel.cs:132-175`），**所以它的 `Items` 在任何一次保存前都等于仓库最新数据**，快照是完整的，`MergeAsync` 不会把任何条目误判为"外部新增"或丢失。

`MoveTaskToDateAsync` 的实现：

```csharp
var current = _todo.Items.FirstOrDefault(i => i.Id == taskId)?.DueDate;
TimeOnly time = current is { } d ? TimeOnly.FromDateTime(d.LocalDateTime) : new TimeOnly(9, 0);
var local = date.ToDateTime(time);
var target = new DateTimeOffset(local, _tz.GetUtcOffset(local));
await _todo.SetDueDateAsync(taskId, target);
```

（`DueDate` 为 null 的任务不在日历芯片里，拖拽源已过滤；若仍被调用，按 09:00 补上。）

`CreateTaskOnDateAsync`：当天是今天用 `_now()` 的时间部分，否则 09:00，然后 `_todo.AddItemAsync(text, false, due)`。

**结论**：G5 通过"共用同一个 `TodoWidgetViewModel` 实现"达成，而不是靠复制或抽象层。原计划中的 `TodoItemMutations` helper **取消**——没有必要。

#### 同步

- 订阅 `_todo.PropertyChanged` 中与 `Items` 相关的变化（或直接订阅 `_repository.StoreChanged`），经 `_dispatcherQueue.TryEnqueue` + `Interlocked` gate 合并成一次重投影（复用 `_externalReloadGate` 模式）。
- `Dispose` 时退订并 `_todo.Dispose()`（日历持有的 VM 由日历负责释放；若宿主复用外部 VM，则通过构造函数布尔参数 `ownsTodoViewModel` 控制，默认 `true`）。

#### `MonthlyTitle` / 时区

- `_tz` 默认 `TimeZoneInfo.Local`，可注入以测试 DST。
- `_monthAnchor` 初始化为 `_now()` 所在月的 1 号（**防止 `DateOnly` 默认值 `0001-01-01` 导致首帧炸**）。

### 7.3 `TodoCalendarDayCell` / chip（新文件，VM 模型）

```csharp
public sealed class TodoCalendarDayCell : ObservableObject
{
    public DateOnly Date { get; }
    public int DayNumber { get; }
    public bool IsCurrentMonth { get; }
    public bool IsToday { get; }
    public bool IsSelected { get; }
    public bool IsWeekend { get; }
    public IReadOnlyList<TodoCalendarTaskChip> Tasks { get; }   // 已截断
    public int OverflowCount { get; }
    public bool HasTasks => Tasks.Count > 0 || OverflowCount > 0;
}

public sealed record TodoCalendarTaskChip(
    string Id, string Text, bool IsCompleted, string ColorHex, bool IsImportant);
```

**`MaxTasksPerCell` 放在 Content 层（视图），不放在 VM**：同一份数据在不同尺寸的宿主（Todo 内日历 vs 贴片）需要不同的截断数量。Content 在 `SizeChanged` 时按可用高度算出 `maxVisible` 再调 `AssignSlots`。VM 只提供桶，不决定截断。

### 7.4 `TodoCalendarContent.xaml(.cs)`（新组件）

- `UserControl`，主构造函数 `TodoCalendarContent(TodoCalendarViewModel vm)`。
- 结构：顶部导航（`◀ 2026 年 10 月 ▶` + 「今天」）→ 星期表头（按文化首日）→ 固定 6 行月网格 → 可选当日任务列表。
- 每格 `CalendarDayCellTemplate`：日期数字 + 最多 N 条 chip + `+N`。
- **响应式**：实现 `IWidgetResponsiveLayoutContent`。宽度 < 约 360px 隐藏当日列表只保留网格；高度不足减少 chip 数（不改行数）。

**实现接口清单**（解决审查 B4，逐项明确）：

| 接口 | 是否实现 | 说明 |
|---|---|---|
| `IWidgetResponsiveLayoutContent` | ✅ | 尺寸变化时重算 chip 上限与是否显示当日列表 |
| `IWidgetInteractiveResizeContent` | ✅ | 拖拽缩放期间暂停重投影，结束时重算 |
| `IWidgetFeedbackSource` | ✅ | chip 的"打开详情"经 `FeedbackRequested` 转发给宿主；**独立贴片没有详情宿主时，退化为就地展开内联编辑（改名 + 完成）**，不弹不存在的详情页 |
| `IWidgetTransientStateContent` | ⚠️ 可选 | 若实现，仅保存 `MonthAnchor` / `SelectedDate`，便于窗口重建恢复 |
| `IWidgetAddActionContent` | ✅ | 宿主标题栏的"+"动作 → 选中今天并聚焦新建框 |
| `IWidgetGroupContentCacheable` | ❌ 不实现 | 日历不与其它组件分组缓存，避免复杂化 |

**拖放**（解决审查 B5）：

- chip 拖：`DragStarting` 里 `args.Data.SetText(taskId)`（与现有原生拖放同类格式，AOT 友好）+ `args.Data.RequestedOperation = Move`；**必须 `args.Cancel = true` 以外的方式阻止窗口整体拖动**——实测若窗口拖动区吞掉事件，则把 chip 设为窗口拖动区的"排除子区域"（参照 `QuickPanelWindow.IsPointInHeaderCaption` 的做法）。
- 格子接收：`AllowDrop = true`，`Drop` 时读取文本 → 校验是当前可见任务 id → `MoveTaskToDateAsync`。
- **撤销**：拖拽改期**替换** `TodoWidgetViewModel` 的 undo 快照（其 `TodoUndoSnapshot` 是单槽的），所以"拖完后按 Ctrl+Z"能撤销到拖之前的状态；跨表面撤销不做扩展。这是与列表视图一致的行为。

**格子交互**：点格子 → `SelectDateAsync`；点 chip → 打开详情或内联编辑；chip 勾选 → `ToggleCompletedAsync`；空格双击 → 内联新建（TextBox + Enter）→ `CreateTaskOnDateAsync`；右键 chip → 菜单（今天/明天/清除日期/删除/颜色）；滚轮 → 切月。

### 7.5 Calendar 贴片接入（解决审查 B3，完整清单）

**必须修改的接线点**（缺任一处会导致启动/删除/重置异常）：

1. `WidgetKind` 枚举末尾追加 `Calendar`（值 10；`WidgetKindJsonConverter` 按名序列化，末尾追加安全）。
2. `WidgetRegistry.CreateDefault()`：加 `new(WidgetKind.Calendar, CanCreateWindow: true, IsImplemented: true)`。
3. `WidgetContentFactory.DescriptorList`：加 descriptor（`IsFeatureWidget: true`、`HasSettingsPage: true`、图标用 Calendar 字形，**不与 Glance 的 `` 重复**、`ChromeCategory`/`DefaultChromeMode` 参考 Todo）。
4. 新增 `TodoCalendarContentProvider : IWidgetContentProvider`（走 target resolver，`SettingsService == null` 时**抛异常**）。
5. 新增 `TodoCalendarContentAdapter : WidgetContentAdapterBase`，实现 §7.4 表中接口。
6. **`WidgetManager.CreateFeatureWidgetHandlers`**：加 `Calendar` 条目（否则 `CreateOrShowFeatureWidgetAsync` 打日志后返回 null）。
7. **`WidgetManager.CreateWindowProviders`**：加 `Calendar` 条目（否则启动恢复 `CreateRegisteredWidgetFromConfigAsync` 抛 `NotSupportedException`）。
8. **`WidgetManager.RemoveWidgetAsync` 的 FeatureWidget 分支**：Calendar 必须走"最后一个实例才关总开关"的写法（参照 Todo），否则删一个贴片就把 Calendar 全局关掉。
9. **`WidgetManager.ResetFeatureWidgetAsync`**：加 Calendar 分支。
10. **`WidgetTopologyLayoutService.IsFixedContentKind`**：把 Calendar 加入可缩放集合（它可 resize）。
11. 标题 switch 四处：`WidgetManager.GetDefaultFeatureWidgetTitle`、`WidgetManager.Groups.cs`、`WidgetTitleIconMode`、`WidgetSettingsMenuHelper`。
12. 尺寸默认（`WidgetManager` 的尺寸 switch + `WidgetCompactWarmupSchedulePolicy`）：宽 ≈ 360、高 ≈ 420。
13. `ContentWidgetWindow.xaml.cs` 的无边框尺寸锁定判断：确认 Calendar 不被误判。
14. **`SettingsService` 归一化**：确认 Calendar 的 `IsDisabled` 语义**不**照抄 Glance 的强制翻转逻辑。
15. i18n：按实际语言文件目录核对数量（**不照抄"13 个"**），补 `WidgetContent.Calendar.*`、`Calendar.*`。
16. **AOT**：新文件追加到 `TodoViewModels.AotBindableProperties.cs`（若有新可绑定属性）**以及** `scripts/publish-aot-audit.ps1` 的 source 清单**末尾**（该脚本用位置下标引用，插在中间会让所有 positional 引用错位）。

**`CreateWidgetOfKindAsync` 不需要加 `case`**：`default` 分支已走 `IsContentFeatureWidgetKind → CreateSingletonContentFeatureWidgetAsync`，加了 Handler 就够。

### 7.6 Todo 组件内的视图切换

- 状态存 `WidgetConfig.Metadata["Todo.ViewMode"]`（**不用新属性**，符合 `TodoMasterDetailSettings` 的既有范例；`ResetFeatureWidgetConfig` 的 `Metadata.Clear()` 自然复位；不影响现有快照测试）。
- 顶部工具区加"列表/月历"切换。
- 切到月历时**懒构造** `TodoCalendarViewModel` + `TodoCalendarContent`（与 `QuickPanelWindow.EnsureTodoContent` 同模式），切回时保留实例。
- **`TodoWidgetViewModel` 新增的视图模式属性必须注册进 `TodoViewModels.AotBindableProperties.cs`**，否则 AOT 下绑定失败。

## 8. 测试计划

| 文件 | 覆盖 |
|------|------|
| `TodoCalendarProjectionTests.cs` | 周起点（Invariant / zh-CN / en-US 显式构造）；42 格（首末日期、跨月、闰年、月末）；本地时区分桶（含 DST 切换日）；排序；截断与溢出；无 `DueDate` 排除 |
| `TodoCalendarViewModelTests.cs` | 初始化；`MoveTaskToDate` 保留时分且**循环 `AnchorDueDate` 同步平移**；`CreateTaskOnDate` 落库；`ToggleCompletedAsync` 完成循环任务**生成下一次**；外部 `MutateAsync` 后重投影；Dispose 后无残留订阅；`SettingsService` 无 Todo 时进入空态而非崩溃 |
| `TodoTargetResolverTests.cs` | 多 Todo 配置时选第一个启用的；Todo 被禁用/删除时的行为；无配置时回退 `"todo"` |
| 扩展 `FeatureWidgetKindContractTests` | **必须更新精确顺序断言**（新增 Calendar）；Calendar 注册可创建、descriptor 存在 |
| 其它引用 Glance 的契约测试 | `WidgetContentFactoryTests`、`SettingsViewModelFeatureWidgetTests`、`FeatureWidgetsSettingsCoordinatorTests`、`WidgetTitleIconModeTests`、`WidgetSettingsMenuHelperTests`、`TodoOrphanCleanupContractTests` 逐个检查是否需要新条目 |

**测试隔离**（解决审查 B6）：

- 每个用例新建 GUID 临时目录 + `new TodoWidgetStore(dir, id)`（沿用 `TodoRepositoryTests` 风格）。
- 用例结束时调 `TodoRepository.TryRemove(storePath)`，防止进程级 `Repositories` 字典随用例数无限增长。
- 同一用例内新建指向同一路径的 store 会拿到同一 repo 实例，`StoreChanged` 会把两个 VM 串起来——测试里显式 `Dispose` 旧的 VM。
- 时钟通过 `Func<DateTimeOffset>` 注入 VM；但 `TodoWidgetViewModel`/helper 内部写的 `UpdatedAt = DateTimeOffset.UtcNow` **不可控**，因此**不要断言精确时间戳**，只断言"非空/被刷新"。

## 9. 风险与缓解

| 风险 | 缓解 |
|------|------|
| 贴片频繁重投影导致闪烁 | 投影为纯内存操作；`StoreChanged` 经 `Interlocked` gate 合并到一次 Dispatcher 投递 |
| 月网格高度跳动 | 固定 6 行；只减 chip 数不减行数 |
| `WidgetKind` 新增影响旧配置 | 末尾追加 + 按名序列化 |
| 拖拽与窗口拖动冲突 | chip 的 `DragStarting` 阻止窗口移动；必要时把 chip 区排除出拖动区（参照 `QuickPanelWindow.IsPointInHeaderCaption`） |
| 时区/DST 边界 | 投影统一 `TimeZoneInfo.ConvertTime`；测试覆盖跨日与 DST 切换日 |
| 孤儿 store | target resolver 统一解析，不按各自 config.Id 建 store |
| 删贴片误关全局开关 | 参照 Todo 的"最后一个实例才关"写法 |

## 10. 实现阶段（顺序已按审查意见调整）

| 阶段 | 内容 | 可独立合并 |
|---|---|---|
| **0** | `TodoTargetResolver`（+ 测试）：统一"当前启用的 Todo"解析；快捷面板改为使用它 | ✅ |
| **1** | `TodoCalendarProjection` + 测试（纯逻辑，零 UI 风险） | ✅ |
| **2** | `TodoCalendarViewModel` + `TodoCalendarContent`；在 Todo 组件内接入"列表/月历"切换（含 `Todo.ViewMode` Metadata 与 AOT 注册） | ✅ |
| **3** | `WidgetKind.Calendar` 全接入（§7.5 的 16 项清单）+ i18n + 契约测试更新 | ✅ |
| **4** | AOT 冒烟（新增 Calendar 持久化 smoke fixture）+ 全量回归 | ✅ |

## 11. 留给构建者的决定

- 月网格用 `Grid`（固定 42 格，无需虚拟化）。
- chip 文本：单行 `TextTrimming="CharacterEllipsis"` + ToolTip 全文。
- 贴片默认隐藏已完成任务，右键菜单可切换。
- 新建任务默认时间：当天是今天则 `_now()`，否则 09:00。
- 首版周起点跟随系统文化，不做配置开关。

## 12. 用户视角的关键体验约束

这些不是功能清单，是"用户会不会觉得好用"的判断标准，实现时以此为准：

1. **打开就能看到"这周有什么"**——贴片默认停在当月、选中今天、今天格高亮，不需要任何操作。
2. **一眼看出轻重**——chip 用任务现有的颜色标记着色，已完成的任务加删除线并降低不透明度；重要任务有视觉区分。
3. **拖一下就改期，不需要进详情页**——这是日历视图相对列表的唯一不可替代价值。
4. **点空格子就能加事**——双击 → 直接输入 → Enter，不弹对话框。
5. **勾选即完成，循环任务自动接上下一次**——用户不该感知"循环是怎么实现的"。
6. **切月不闪、不跳、不卡**——贴片是常驻的，任何抖动都会被反复看到。
7. **改动即时同步**——在贴片勾了任务，Todo 列表那一行立刻变。

## 13. 决策记录（构建者审查的裁决）

| 审查项 | 裁决 | 理由 |
|---|---|---|
| B1 写路径矛盾 | **委托 `TodoWidgetViewModel` 的公开方法**；`TodoItemMutations` helper 取消 | 这三个方法已内含循环/提醒/锚点全部语义，且 `Items` 经 `StoreChanged` 保持与仓库同步，快照完整 |
| B2 Context 字段不足 | 新增 `TodoTargetResolver`；`SettingsService == null` 时抛异常 | 静默回退会建空 store，命中反目标 1 |
| B3 接入点漏 24+ | §7.5 改为完整清单，标明每处缺失的后果 | 删除/重置/恢复三条路径都会炸 |
| B4 接口清单写成"…" | §7.4 逐项列表，含独立贴片无详情宿主时的退化方案 | 决定 G2/G3 能否落地 |
| B5 拖拽与撤销未定义 | `SetText(taskId)` + Move；撤销复用单槽快照 | AOT 友好且与列表一致 |
| B6 测试隔离 | 沿用 GUID 目录 + `TryRemove` + 显式 Dispose；不断言精确时间戳 | 进程级仓库缓存按路径键控 |
| 视图模式存 `Metadata` 而非新属性 | 采纳 | 符合 `TodoMasterDetailSettings` 范例，无需迁移 |
| `MaxTasksPerCell` 放 Content 层 | 采纳 | 不同宿主尺寸不同，VM 只管数据 |
| 6 行 vs 压缩矛盾 | 固定 6 行，只减 chip 数 | 防贴片高度跳动 |
| 新文件追加到 AOT 清单末尾 | 采纳 | 脚本按位置下标引用，插中间会错位 |
