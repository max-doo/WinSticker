# Todo 日历视图与独立日历贴片 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Todo 组件里加月历视图、并新增一个可交互的独立桌面日历贴片（`WidgetKind.Calendar`），两者共用同一套日历组件与同一份 Todo 数据。

**Architecture:** 三层——纯函数投影层 `TodoCalendarProjection`（日期分桶/网格/截断）、VM 层 `TodoCalendarViewModel`（持有 `TodoWidgetViewModel` 作为唯一写入口）、视图层 `TodoCalendarContent`（两个宿主复用）。Calendar 贴片不拥有自己的 store，经 `TodoTargetResolver` 读取当前启用的 Todo 的 store。

**Tech Stack:** WinUI 3 / .NET 10 (`net10.0-windows10.0.22621.0`)、CommunityToolkit.Mvvm、xUnit、Native AOT

**Spec:** `docs/superpowers/specs/2026-10-24-todo-calendar-design.md`

## Global Constraints

- 构建/测试前设 `$env:DOTNET_ROOT="$env:LOCALAPPDATA\Microsoft\dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"`（SDK 仅在此目录）。
- 测试命令：`dotnet test .\tests\WinSticker.Tests\WinSticker.Tests.csproj --no-restore --verbosity:minimal -p:Platform=x64`。**绿 = 通过数 == 总数**，`失败: 0` 单独出现不算绿（可能中断）。
- 改动应用代码后：先停掉仓库路径下运行的 `WinSticker.exe` 再构建；构建成功的唯一证据是 `src/WinSticker/bin/Debug/net10.0-windows10.0.22621.0/WinSticker.dll` 的写入时间晚于构建开始时间。
- 构建后从 `src/WinSticker/bin/Debug/net10.0-windows10.0.22621.0/WinSticker.exe` 启动，并报告路径。不要从 `Output`/`artifacts`/`AppPackages` 启动。
- **禁止** `Co-Authored-By`、`Generated with`、任何助手署名（`.githooks/commit-msg` 会拒绝）。
- 新增源码文件**追加到 `scripts/publish-aot-audit.ps1` 的 source 清单末尾**（脚本按位置下标引用，插中间会让所有 positional 引用错位）。
- `WidgetKind` 新值**追加在枚举末尾**（`WidgetKindJsonConverter` 按名序列化）。
- 中文注释与当前代码库风格一致；i18n 键按现有 `Todo.*` / `WidgetContent.*` 命名。

## Review Focus

1. **跨 UTC 日界的到期时间**：23:30（本地）到期的任务必须落在"今天"格，不能因为 `DateTimeOffset.UtcNow` 跨天而落到明天格 → Task 2 测试用带偏移的 `DateTimeOffset` 覆盖。
2. **循环任务在日历上完成**：勾选"每周一"的任务必须生成下一次并让列表也看到，不能只在日历里消失 → Task 6 测试断言完成后仓库里出现下一条。
3. **拖拽改期后循环锚点**：拖"每周一"的任务到周三，`Recurrence.AnchorDueDate` 必须一起平移，否则下次生成会回到周一 → Task 6 测试断言。
4. **Todo 被禁用/删除时的日历贴片**：不能崩、不能显示别人的空 store、不能把操作写到孤儿 store → Task 5 测试断言空态而非异常传播到 UI。
5. **删除一个日历贴片**：不能把 Calendar 功能总开关关掉（用户会以为"日历消失了"）→ Task 8 测试断言仍有其它实例时开关保持。
6. **月网格跨月/闰年边界**：2 月 29 日、月末 31 号、周一/周日为起始两种文化下 42 格首末日期正确 → Task 2 测试。

---

## File Structure

| 文件 | 职责 | 新增/修改 |
|---|---|---|
| `src/WinSticker/Services/TodoTargetResolver.cs` | 解析"当前启用的 Todo"→ `(WidgetConfig, TodoWidgetStore)` | 新增 |
| `src/WinSticker/Services/TodoCalendarProjection.cs` | 纯函数：月网格、分桶、截断 | 新增 |
| `src/WinSticker/ViewModels/TodoCalendarViewModel.cs` | 日历 VM：月份/选中日/写操作委托 | 新增 |
| `src/WinSticker/ViewModels/TodoCalendarDayCell.cs` | 格与 chip 的 VM 模型 | 新增 |
| `src/WinSticker/Controls/WidgetContents/TodoCalendarContent.xaml(.cs)` | 日历 UI 组件（两宿主复用） | 新增 |
| `src/WinSticker/Controls/WidgetContents/TodoCalendarContentAdapter.cs` | 贴片内容适配器 | 新增 |
| `src/WinSticker/Services/TodoCalendarContentProvider.cs` | `IWidgetContentProvider` | 新增 |
| `src/WinSticker/ViewModels/QuickPanelViewModel.Notes.cs` | `GetActiveTodoWidgetId` 改用 resolver | 修改 |
| `src/WinSticker/Models/WidgetConfig.cs` | `WidgetKind.Calendar` | 修改 |
| `src/WinSticker/Services/WidgetRegistry.cs` | 注册 Calendar | 修改 |
| `src/WinSticker/Services/WidgetContentFactory.cs` | descriptor | 修改 |
| `src/WinSticker/Services/WidgetManager.cs` | handlers / windowProviders / Remove / title switch / 尺寸 | 修改 |
| `src/WinSticker/Services/WidgetManager.FeatureWidgets.cs` | FeatureWidgets switch / Reset | 修改 |
| `src/WinSticker/Services/WidgetManager.Groups.cs` | title switch | 修改 |
| `src/WinSticker/Services/WidgetTitleIconMode.cs` | 图标模式 | 修改 |
| `src/WinSticker/Services/WidgetSettingsMenuHelper.cs` | 设置菜单 | 修改 |
| `src/WinSticker/Services/WidgetTopologyLayoutService.cs` | `IsFixedContentKind` | 修改 |
| `src/WinSticker/Services/WidgetCompactWarmupSchedulePolicy.cs` | 预热阈值 | 修改 |
| `src/WinSticker/Controls/WidgetContents/TodoWidgetContent.xaml(.cs)` | 列表/月历切换 | 修改 |
| `src/WinSticker/ViewModels/TodoWidgetViewModel*.cs` | `TodoViewMode` 状态 | 修改 |
| `src/WinSticker/Strings/*.json` | i18n | 修改 |
| `tests/WinSticker.Tests/TodoTargetResolverTests.cs` | 新增 |
| `tests/WinSticker.Tests/TodoCalendarProjectionTests.cs` | 新增 |
| `tests/WinSticker.Tests/TodoCalendarViewModelTests.cs` | 新增 |
| `tests/WinSticker.Tests/FeatureWidgetKindContractTests.cs` | 更新精确顺序断言 |

---

### Task 0: `TodoTargetResolver` — 统一"当前启用的 Todo"解析

**Files:**
- Create: `src/WinSticker/Services/TodoTargetResolver.cs`
- Modify: `src/WinSticker/ViewModels/QuickPanelViewModel.Notes.cs:31-40`（`GetActiveTodoWidgetId` 改用它）
- Test: `tests/WinSticker.Tests/TodoTargetResolverTests.cs`

**Interfaces:**
- Consumes: `AppSettings.Widgets`（`List<WidgetConfig>`）、`WidgetConfig.WidgetKind`、`WidgetConfig.Id`、`WidgetConfig.IsDisabled`
- Produces:
  - `public static class TodoTargetResolver`
  - `public static WidgetConfig? ResolveConfig(AppSettings settings)`
  - `public static string ResolveWidgetId(AppSettings settings)` → 找不到时返回 `"todo"`
  - `public const string FallbackWidgetId = "todo";`

- [ ] **Step 1: 写失败测试**

```csharp
using WinSticker.Models;
using WinSticker.Services;
using Xunit;

namespace WinSticker.Tests;

public sealed class TodoTargetResolverTests
{
    [Fact]
    public void ResolveWidgetId_PrefersFirstEnabledTodo()
    {
        var settings = new AppSettings();
        settings.Widgets.Add(new WidgetConfig { Id = "todo-a", WidgetKind = WidgetKind.Todo, IsDisabled = true });
        settings.Widgets.Add(new WidgetConfig { Id = "todo-b", WidgetKind = WidgetKind.Todo });
        settings.Widgets.Add(new WidgetConfig { Id = "todo-c", WidgetKind = WidgetKind.Todo });

        Assert.Equal("todo-b", TodoTargetResolver.ResolveWidgetId(settings));
    }

    [Fact]
    public void ResolveWidgetId_FallsBackWhenNoTodoWidget()
    {
        var settings = new AppSettings();
        settings.Widgets.Add(new WidgetConfig { Id = "other", WidgetKind = WidgetKind.Glance });

        Assert.Equal("todo", TodoTargetResolver.ResolveWidgetId(settings));
        Assert.Null(TodoTargetResolver.ResolveConfig(settings));
    }

    [Fact]
    public void ResolveWidgetId_FallsBackWhenAllDisabled()
    {
        var settings = new AppSettings();
        settings.Widgets.Add(new WidgetConfig { Id = "todo-a", WidgetKind = WidgetKind.Todo, IsDisabled = true });

        Assert.Equal("todo", TodoTargetResolver.ResolveWidgetId(settings));
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test .\tests\WinSticker.Tests\WinSticker.Tests.csproj --no-restore --verbosity:minimal -p:Platform=x64 --filter "FullyQualifiedName~TodoTargetResolverTests"`
Expected: 编译失败 `CS0103: 名称"TodoTargetResolver"在不存在于当前上下文中`

- [ ] **Step 3: 实现 resolver**

`ResolveConfig` 返回第一个 `WidgetKind == Todo && !IsDisabled` 的配置；`ResolveWidgetId` 为 `ResolveConfig(settings)?.Id ?? FallbackWidgetId`。

- [ ] **Step 4: 改 `QuickPanelViewModel.Notes.cs` 的 `GetActiveTodoWidgetId`**

`_activeTodoWidgetId = TodoTargetResolver.ResolveWidgetId(_settingsService.Settings);` 替换原 LINQ 查询；保留 `_activeTodoWidgetId` 缓存字段语义。

- [ ] **Step 5: 跑全量确认绿**

Run: 全量测试命令
Expected: 通过数 == 总数

- [ ] **Step 6: 提交**

```bash
git add src/WinSticker/Services/TodoTargetResolver.cs src/WinSticker/ViewModels/QuickPanelViewModel.Notes.cs tests/WinSticker.Tests/TodoTargetResolverTests.cs
git commit -m "feat: 抽出 TodoTargetResolver 统一解析当前启用的 Todo widget"
```

---

### Task 1: `TodoCalendarProjection` — 月网格生成

**Files:**
- Create: `src/WinSticker/Services/TodoCalendarProjection.cs`
- Test: `tests/WinSticker.Tests/TodoCalendarProjectionTests.cs`（本任务只写网格部分）

**Interfaces:**
- Produces:
  - `public static class TodoCalendarProjection`
  - `public const int WeeksPerMonth = 6; public const int DaysPerWeek = 7; public const int CellCount = 42;`
  - `public static DayOfWeek ResolveWeekStart(CultureInfo culture)`
  - `public static IReadOnlyList<DateOnly> BuildMonthGrid(int year, int month, DayOfWeek weekStart)`

- [ ] **Step 1: 写失败测试**

```csharp
using System.Globalization;
using WinSticker.Services;
using Xunit;

namespace WinSticker.Tests;

public sealed class TodoCalendarProjectionTests
{
    [Fact]
    public void BuildMonthGrid_ReturnsFortyTwoConsecutiveDays()
    {
        var cells = TodoCalendarProjection.BuildMonthGrid(2026, 10, DayOfWeek.Monday);
        Assert.Equal(42, cells.Count);
        for (int i = 1; i < cells.Count; i++)
        {
            Assert.Equal(cells[i - 1].AddDays(1), cells[i]);
        }
    }

    [Fact]
    public void BuildMonthGrid_StartsOnRequestedWeekdayAndContainsWholeMonth()
    {
        var monday = TodoCalendarProjection.BuildMonthGrid(2026, 10, DayOfWeek.Monday);
        Assert.Equal(DayOfWeek.Monday, monday[0].DayOfWeek);
        Assert.Contains(new DateOnly(2026, 10, 1), monday);
        Assert.Contains(new DateOnly(2026, 10, 31), monday);

        var sunday = TodoCalendarProjection.BuildMonthGrid(2026, 10, DayOfWeek.Sunday);
        Assert.Equal(DayOfWeek.Sunday, sunday[0].DayOfWeek);
        Assert.Contains(new DateOnly(2026, 10, 1), sunday);
        Assert.Contains(new DateOnly(2026, 10, 31), sunday);
    }

    [Fact]
    public void BuildMonthGrid_HandlesLeapFebruaryAndMonthEnd()
    {
        var cells = TodoCalendarProjection.BuildMonthGrid(2024, 2, DayOfWeek.Monday);
        Assert.Contains(new DateOnly(2024, 2, 29), cells);
        Assert.Contains(new DateOnly(2024, 2, 1), cells);

        var january = TodoCalendarProjection.BuildMonthGrid(2026, 1, DayOfWeek.Monday);
        Assert.Contains(new DateOnly(2026, 1, 31), january);
    }

    [Theory]
    [InlineData("en-US", DayOfWeek.Sunday)]
    [InlineData("zh-CN", DayOfWeek.Monday)]
    public void ResolveWeekStart_MatchesCulture(string cultureName, DayOfWeek expected)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        Assert.Equal(expected, TodoCalendarProjection.ResolveWeekStart(culture));
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `--filter "FullyQualifiedName~TodoCalendarProjectionTests"`
Expected: 编译失败（类型不存在）

- [ ] **Step 3: 实现**

`BuildMonthGrid`：以该月 1 号为锚，回退到 `weekStart` 对应星期，再连续生成 42 天。`ResolveWeekStart` 返回 `culture.DateTimeFormat.FirstDayOfWeek`。`using System.Globalization;`。

- [ ] **Step 4: 运行确认通过**

Run: `--filter "FullyQualifiedName~TodoCalendarProjectionTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/WinSticker/Services/TodoCalendarProjection.cs tests/WinSticker.Tests/TodoCalendarProjectionTests.cs
git commit -m "feat: 新增 TodoCalendarProjection 月网格生成"
```

---

### Task 2: `TodoCalendarProjection` — 分桶、排序、截断

**Files:**
- Modify: `src/WinSticker/Services/TodoCalendarProjection.cs`
- Test: `tests/WinSticker.Tests/TodoCalendarProjectionTests.cs`（追加）

**Interfaces:**
- Produces:
  - `public static IReadOnlyDictionary<DateOnly, IReadOnlyList<TodoItem>> BucketByLocalDate(IEnumerable<TodoItem> items, TimeZoneInfo tz)`
  - `public static (IReadOnlyList<TodoItem> visible, int overflow) AssignSlots(IReadOnlyList<TodoItem> items, int maxVisible)`

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public void BucketByLocalDate_UsesLocalCalendarDayNotUtcDay()
{
    var tz = TimeZoneInfo.CreateCustomTimeZone("t+8", TimeSpan.FromHours(8), "t+8", "t+8");
    var items = new List<TodoItem>
    {
        new() { Id = "a", Text = "late night", DueDate = new DateTimeOffset(2026, 10, 24, 23, 30, 0, TimeSpan.FromHours(8)) }
    };

    var buckets = TodoCalendarProjection.BucketByLocalDate(items, tz);

    Assert.True(buckets.ContainsKey(new DateOnly(2026, 10, 24)));
    Assert.Equal("a", buckets[new DateOnly(2026, 10, 24)][0].Id);
}

[Fact]
public void BucketByLocalDate_ExcludesItemsWithoutDueDate()
{
    var items = new List<TodoItem>
    {
        new() { Id = "a", Text = "dated", DueDate = DateTimeOffset.UtcNow },
        new() { Id = "b", Text = "undated", DueDate = null }
    };

    var buckets = TodoCalendarProjection.BucketByLocalDate(items, TimeZoneInfo.Utc);

    Assert.DoesNotContain(buckets.Values.SelectMany(v => v), item => item.Id == "b");
}

[Fact]
public void BucketByLocalDate_OrdersActiveBeforeCompletedThenByTime()
{
    var day = new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
    var items = new List<TodoItem>
    {
        new() { Id = "done", Text = "done", IsCompleted = true, DueDate = day.AddHours(9) },
        new() { Id = "late", Text = "late", DueDate = day.AddHours(18), SortOrder = 1 },
        new() { Id = "early", Text = "early", DueDate = day.AddHours(8), SortOrder = 5 }
    };

    var buckets = TodoCalendarProjection.BucketByLocalDate(items, TimeZoneInfo.Utc);

    Assert.Equal(["early", "late", "done"], buckets[new DateOnly(2026, 10, 24)].Select(item => item.Id));
}

[Fact]
public void AssignSlots_TruncatesAndCountsOverflow()
{
    var items = Enumerable.Range(0, 5)
        .Select(i => new TodoItem { Id = i.ToString(), Text = $"t{i}", DueDate = DateTimeOffset.UtcNow })
        .ToList();

    var (visible, overflow) = TodoCalendarProjection.AssignSlots(items, maxVisible: 3);

    Assert.Equal(3, visible.Count);
    Assert.Equal(2, overflow);
}

[Fact]
public void AssignSlots_WithZeroMaxReturnsEverythingAsOverflow()
{
    var items = new List<TodoItem> { new() { Id = "a", Text = "a", DueDate = DateTimeOffset.UtcNow } };

    var (visible, overflow) = TodoCalendarProjection.AssignSlots(items, maxVisible: 0);

    Assert.Empty(visible);
    Assert.Equal(1, overflow);
}
```

- [ ] **Step 2: 运行确认失败**

Expected: 编译失败（方法不存在）

- [ ] **Step 3: 实现**

- `BucketByLocalDate`：`TimeZoneInfo.ConvertTime(item.DueDate.Value, tz)` 取 `.Date` → `DateOnly`；无 `DueDate` 跳过。
- 排序：`IsCompleted` 升序 → `DueDate` 升序 → `SortOrder` → `CreatedAt`。
- `AssignSlots`：`maxVisible <= 0` 时全进 overflow。

- [ ] **Step 4: 运行确认通过**（`--filter "FullyQualifiedName~TodoCalendarProjectionTests"`，全部 9 个用例 PASS）

- [ ] **Step 5: 提交**

```bash
git add src/WinSticker/Services/TodoCalendarProjection.cs tests/WinSticker.Tests/TodoCalendarProjectionTests.cs
git commit -m "feat: TodoCalendarProjection 支持本地日期分桶与格子截断"
```

---

### Task 3: `TodoCalendarDayCell` / `TodoCalendarTaskChip` 模型

**Files:**
- Create: `src/WinSticker/ViewModels/TodoCalendarDayCell.cs`
- Test: `tests/WinSticker.Tests/TodoCalendarDayCellTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class TodoCalendarDayCell`（`Date`, `DayNumber`, `IsCurrentMonth`, `IsToday`, `IsSelected`, `IsWeekend`, `Tasks`, `OverflowCount`, `HasTasks`）
  - `public sealed record TodoCalendarTaskChip(string Id, string Text, bool IsCompleted, string ColorHex, bool IsImportant)`

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public void DayCell_ExposesDerivedFlags()
{
    var cell = new TodoCalendarDayCell(new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 1), isToday: true, isSelected: false);

    Assert.Equal(24, cell.DayNumber);
    Assert.True(cell.IsCurrentMonth);
    Assert.True(cell.IsToday);
    Assert.False(cell.IsSelected);
    Assert.True(cell.IsWeekend); // 2026-10-24 是周六
    Assert.False(cell.HasTasks);
}

[Fact]
public void DayCell_OutsideMonthIsNotCurrentMonth()
{
    var cell = new TodoCalendarDayCell(new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 1), isToday: false, isSelected: false);
    Assert.False(cell.IsCurrentMonth);
    Assert.False(cell.IsWeekend); // 周一
}
```

- [ ] **Step 2: 运行确认失败** → 编译失败

- [ ] **Step 3: 实现**

`TodoCalendarDayCell` 实现 `INotifyPropertyChanged`（`ObservableObject`），`Tasks`/`OverflowCount` 通过 `SetCell(...)` 方法更新。`HasTasks => Tasks.Count > 0 || OverflowCount > 0`。`IsWeekend` 为周六或周日。

- [ ] **Step 4: 运行确认通过**

- [ ] **Step 5: 提交**

```bash
git add src/WinSticker/ViewModels/TodoCalendarDayCell.cs tests/WinSticker.Tests/TodoCalendarDayCellTests.cs
git commit -m "feat: 新增日历格与任务 chip 的视图模型"
```

---

### Task 4: `TodoCalendarViewModel` — 加载与投影

**Files:**
- Create: `src/WinSticker/ViewModels/TodoCalendarViewModel.cs`
- Test: `tests/WinSticker.Tests/TodoCalendarViewModelTests.cs`

**Interfaces:**
- Consumes: `TodoTargetResolver`(Task 0)、`TodoCalendarProjection`(Task 1/2)、`TodoCalendarDayCell`(Task 3)、`TodoWidgetViewModel`
- Produces:
  - `public TodoCalendarViewModel(TodoWidgetStore store, LocalizationService localizationService, WidgetConfig config, SettingsService? settingsService = null, Func<DateTimeOffset>? clock = null, TimeZoneInfo? timeZone = null)`
  - `public Task InitializeAsync()`
  - `public Task ShowMonthAsync(DateOnly month)`
  - `public Task SelectDateAsync(DateOnly date)`
  - `public Task GoToTodayAsync()`
  - `public ObservableCollection<TodoCalendarDayCell> Cells { get; }`
  - `public DateOnly MonthAnchor { get; }`（`[ObservableProperty]`）
  - `public DateOnly? SelectedDate { get; }`（`[ObservableProperty]`）
  - `public ObservableCollection<TodoItemViewModel> SelectedDayTasks { get; }`
  - `public string MonthTitle { get; }`
  - `public int MaxTasksPerCell { get; set; }`（由视图设置）

- [ ] **Step 1: 写失败测试**

```csharp
private static void Cleanup(string dir)
{
    TodoRepository.TryRemove(new TodoWidgetStore(Path.Combine(dir, "widgets"), "cal-test").StorePath);
    try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
}

private static (TodoCalendarViewModel vm, string dir) CreateEnvironment()
{
    string dir = Path.Combine(Path.GetTempPath(), "winsticker-cal-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var store = new TodoWidgetStore(Path.Combine(dir, "widgets"), "cal-test");
    var settings = new SettingsService(dir);
    var loc = new LocalizationService(settings);
    var fixedNow = new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);
    var vm = new TodoCalendarViewModel(store, loc, new WidgetConfig { Id = "cal-test", WidgetKind = WidgetKind.Todo },
        settings, () => fixedNow, TimeZoneInfo.Utc);
    return (vm, dir);
}

[Fact]
public async Task Initialize_BuildsSixWeekGridCoveringAnchorMonth()
{
    var (vm, dir) = CreateEnvironment();
    try
    {
        await vm.InitializeAsync();
        Assert.Equal(42, vm.Cells.Count);
        Assert.Equal(new DateOnly(2026, 10, 1), vm.MonthAnchor);
        Assert.Contains(vm.Cells, cell => cell.IsToday && cell.Date == new DateOnly(2026, 10, 24));
    }
    finally { Cleanup(dir); }
}

[Fact]
public async Task ShowMonth_RebuildsGridToRequestedMonth()
{
    var (vm, dir) = CreateEnvironment();
    try
    {
        await vm.InitializeAsync();
        await vm.ShowMonthAsync(new DateOnly(2026, 12, 1));
        Assert.Equal(new DateOnly(2026, 12, 1), vm.MonthAnchor);
        Assert.Contains(vm.Cells, cell => cell.Date == new DateOnly(2026, 12, 1) && cell.IsCurrentMonth);
    }
    finally { Cleanup(dir); }
}

[Fact]
public async Task TaskWithDueDate_AppearsInItsLocalDayCell()
{
    var (vm, dir) = CreateEnvironment();
    try
    {
        await vm.InitializeAsync();
        await vm.Todo.CreateTaskOnDateAsync(new DateOnly(2026, 10, 24), "write tests");
        await vm.InitializeAsync();

        var cell = vm.Cells.Single(c => c.Date == new DateOnly(2026, 10, 24));
        Assert.Contains(cell.Tasks, chip => chip.Text == "write tests");
    }
    finally { Cleanup(dir); }
}
```

（`Cleanup(dir)` 为测试文件内私有 helper：`TodoRepository.TryRemove(...)` + 删除目录。）

- [ ] **Step 2: 运行确认失败** → 编译失败

- [ ] **Step 3: 实现**

- 构造：`MonthAnchor = DateOnly.FromDateTime(_now().LocalDateTime)` 取 1 号；`SelectedDate = 今天`。
- `InitializeAsync`：`await _todo.InitializeAsync()`，然后 `RebuildCells()`：从 `_todo.Items.Select(i => i.Item)` 取数据 → `BucketByLocalDate` → 为 42 个日期生成 `TodoCalendarDayCell` → `AssignSlots(items, MaxTasksPerCell)`。
- `ShowMonthAsync`：设置 `MonthAnchor`，`RebuildCells()`。
- `SelectDateAsync`：设置 `SelectedDate`，重算 `SelectedDayTasks` 与各格 `IsSelected`。
- 订阅 `_todo.PropertyChanged`（`Items` 集合变化时）→ `DispatcherQueue.TryEnqueue(RebuildCells)`，用 `Interlocked` gate 合并。
- `MonthTitle`：`$"{MonthAnchor.Year} 年 {MonthAnchor.Month} 月"`（后续 Task 接 i18n）。

- [ ] **Step 4: 运行确认通过**

- [ ] **Step 5: 提交**

```bash
git add src/WinSticker/ViewModels/TodoCalendarViewModel.cs tests/WinSticker.Tests/TodoCalendarViewModelTests.cs
git commit -m "feat: TodoCalendarViewModel 支持按本地日期投影月网格"
```

---

### Task 5: `TodoCalendarViewModel` — 写操作委托与空态

**Files:**
- Modify: `src/WinSticker/ViewModels/TodoCalendarViewModel.cs`
- Test: `tests/WinSticker.Tests/TodoCalendarViewModelTests.cs`（追加）

**Interfaces:**
- Consumes: `TodoWidgetViewModel.SetCompletedAsync(string, bool)`、`SetDueDateAsync(string, DateTimeOffset?)`、`AddItemAsync(string?, bool, DateTimeOffset?)`
- Produces:
  - `public TodoWidgetViewModel Todo { get; }`
  - `public Task MoveTaskToDateAsync(string taskId, DateOnly date)`
  - `public Task CreateTaskOnDateAsync(DateOnly date, string text)`
  - `public Task ToggleCompletedAsync(string taskId)`
  - `public bool IsEmptyStateVisible { get; }`

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public async Task MoveTaskToDate_PreservesTimeOfDayAndMovesToNewDay()
{
    var (vm, dir) = CreateEnvironment();
    try
    {
        await vm.InitializeAsync();
        var due = new DateTimeOffset(2026, 10, 24, 15, 30, 0, TimeSpan.Zero);
        var created = await vm.Todo.AddItemAsync("ship release", false, due);
        await vm.InitializeAsync();

        await vm.MoveTaskToDateAsync(created!.Id, new DateOnly(2026, 10, 27));

        var reloaded = TodoRepository.ForStore(new TodoWidgetStore(Path.Combine(dir, "widgets"), "cal-test"));
        var data = await reloaded.LoadAsync();
        var item = data.Items.Single(i => i.Id == created.Id);
        // 断言用 DateTimeOffset 自身的本地视图（创建时 Offset=0），与 VM 的 TimeZoneInfo.Utc 一致
        Assert.Equal(new DateOnly(2026, 10, 27), DateOnly.FromDateTime(item.DueDate!.Value.DateTime));
        Assert.Equal(new TimeOnly(15, 30), TimeOnly.FromDateTime(item.DueDate.Value.DateTime));
    }
    finally { Cleanup(dir); }
}

[Fact]
public async Task MoveTaskToDate_ShiftsRecurrenceAnchor()
{
    var (vm, dir) = CreateEnvironment();
    try
    {
        await vm.InitializeAsync();
        var due = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
        var created = await vm.Todo.AddItemAsync("weekly", false, due);
        await vm.Todo.SetRecurrenceAsync(created!.Id, TodoRecurrenceMode.Weekly);

        await vm.MoveTaskToDateAsync(created.Id, new DateOnly(2026, 10, 28));

        var item = vm.Todo.Items.Single(i => i.Id == created.Id);
        Assert.Equal(new DateOnly(2026, 10, 28), DateOnly.FromDateTime(item.DueDate!.Value.UtcDateTime));
        Assert.Equal(new DateOnly(2026, 10, 28), DateOnly.FromDateTime(item.Recurrence!.AnchorDueDate!.Value.UtcDateTime));
    }
    finally { Cleanup(dir); }
}

[Fact]
public async Task ToggleCompleted_OnRecurringTask_GeneratesNextOccurrence()
{
    var (vm, dir) = CreateEnvironment();
    try
    {
        await vm.InitializeAsync();
        var created = await vm.Todo.AddItemAsync("weekly", false, new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));
        await vm.Todo.SetRecurrenceAsync(created!.Id, TodoRecurrenceMode.Weekly);
        int before = vm.Todo.Items.Count;

        await vm.ToggleCompletedAsync(created.Id);

        Assert.True(vm.Todo.Items.Single(i => i.Id == created.Id).IsCompleted);
        Assert.True(vm.Todo.Items.Count > before, "完成循环任务后必须生成下一次");
    }
    finally { Cleanup(dir); }
}

[Fact]
public async Task CreateTaskOnDate_OnToday_UsesClockTime_OnOtherDay_UsesNineAm()
{
    var (vm, dir) = CreateEnvironment();
    try
    {
        await vm.InitializeAsync();
        await vm.CreateTaskOnDateAsync(new DateOnly(2026, 10, 24), "today task");
        await vm.CreateTaskOnDateAsync(new DateOnly(2026, 11, 2), "future task");

        var today = vm.Todo.Items.Single(i => i.Item.Text == "today task");
        var future = vm.Todo.Items.Single(i => i.Item.Text == "future task");
        Assert.Equal(new TimeOnly(10, 0), TimeOnly.FromDateTime(today.DueDate!.Value.UtcDateTime));
        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(future.DueDate!.Value.UtcDateTime));
    }
    finally { Cleanup(dir); }
}
```

- [ ] **Step 2: 运行确认失败** → 编译失败

- [ ] **Step 3: 实现**

- `Todo` 属性暴露内部 `TodoWidgetViewModel`。
- `MoveTaskToDateAsync`：**直接调 `_todo.SetDueDateAsync(id, target)`**（其中 `target` 保留原时分；无原 `DueDate` 用 09:00）。不要自己写锚点平移逻辑。
- `CreateTaskOnDateAsync`：今天用 `_now()` 时间，否则 09:00，调 `_todo.AddItemAsync(text, false, due)`。
- `ToggleCompletedAsync`：先读当前完成态，再 `_todo.SetCompletedAsync(id, !current)`。
- `IsEmptyStateVisible`：Todo 目标解析失败或 `_todo.Items.Count == 0`。
- **`Dispose`**：退订 `_todo.PropertyChanged` 并 `_todo.Dispose()`（构造参数 `ownsTodoViewModel`，默认 `true`）。

- [ ] **Step 4: 运行确认通过**

- [ ] **Step 5: 跑全量确认绿**

- [ ] **Step 6: 提交**

```bash
git add src/WinSticker/ViewModels/TodoCalendarViewModel.cs tests/WinSticker.Tests/TodoCalendarViewModelTests.cs
git commit -m "feat: 日历 VM 复用 TodoWidgetViewModel 完成/改期/新建语义"
```

---

### Task 6: `TodoCalendarContent` XAML 组件

**Files:**
- Create: `src/WinSticker/Controls/WidgetContents/TodoCalendarContent.xaml`
- Create: `src/WinSticker/Controls/WidgetContents/TodoCalendarContent.xaml.cs`
- Test: 由 Task 9 的 AOT 冒烟与手测覆盖（XAML 组件不做单元测试，遵循现有 `TodoWidgetContent` 的做法）

**Interfaces:**
- Consumes: `TodoCalendarViewModel`(Task 4/5)
- Produces: `public sealed partial class TodoCalendarContent : UserControl`，构造 `TodoCalendarContent(TodoCalendarViewModel viewModel)`，`public TodoCalendarViewModel ViewModel { get; }`

- [ ] **Step 1: 实现 XAML 布局**

结构：`Grid` 顶层两行——导航栏（`◀` `MonthTitle` `▶` `今天`）、`ScrollViewer` 内含月网格与当日列表。

月网格：`Grid` 7 列 × 6 行，列/行均 `*`；每格 `Border` 绑定 `TodoCalendarDayCell`，内含日期数字 `TextBlock` + `ItemsRepeater`（`Tasks`）+ `+N` `TextBlock`（`OverflowCount` 可见性由 converter 控制）。

样式引用 `TodoWidgetContent.xaml` 中已有的 chip 视觉资源（颜色标记着色、完成删除线）。

- [ ] **Step 2: 实现 code-behind**

- `SizeChanged` 中按可用高度算 `MaxTasksPerCell`（`(cellHeight - 20) / 18`，clamp 0..5）并写回 VM、触发重建。
- 点格子 → `await ViewModel.SelectDateAsync(cell.Date)`。
- 双击空格子 → 在格内显示 `TextBox`，Enter → `CreateTaskOnDateAsync`。
- chip 勾选 → `ToggleCompletedAsync`。
- chip `DragStarting`：`args.Data.SetText(chip.Id)`，`RequestedOperation = Move`。
- 格子 `Drop`：读取文本 → 校验是当前可见 chip 的 id → `MoveTaskToDateAsync`。
- 滚轮 → `ShowMonthAsync(MonthAnchor.AddMonths(±1))`。
- 「今天」→ `GoToTodayAsync()`。

- [ ] **Step 3: 构建**

Run: 停进程 → `dotnet build .\src\WinSticker\WinSticker.csproj -p:Platform=x64` → 校验 `WinSticker.dll` 时间戳
Expected: 0 errors；dll 时间戳晚于构建开始

- [ ] **Step 4: 手测**

启动 → 在 Todo 组件中确认月网格渲染、今天高亮、点格子筛选、勾选完成生效。

- [ ] **Step 5: 提交**

```bash
git add src/WinSticker/Controls/WidgetContents/TodoCalendarContent.xaml src/WinSticker/Controls/WidgetContents/TodoCalendarContent.xaml.cs
git commit -m "feat: 新增可复用的 TodoCalendarContent 月历组件"
```

---

### Task 7: Todo 组件内"列表/月历"切换

**Files:**
- Modify: `src/WinSticker/ViewModels/TodoWidgetViewModel.cs`（新增 `TodoViewMode` 状态与 `Metadata` 持久化）
- Modify: `src/WinSticker/Controls/WidgetContents/TodoWidgetContent.xaml` / `.xaml.cs`（切换按钮 + `ContentPresenter`）
- Modify: `src/WinSticker/ViewModels/TodoViewModels.AotBindableProperties.cs`（注册新属性）
- Modify: `tests/WinSticker.Tests/`（新增持久化测试）

**Interfaces:**
- Produces:
  - `public enum TodoContentViewMode { List, Calendar }`
  - `TodoWidgetViewModel.ContentMode`（`[ObservableProperty]`，setter 写 `_config.Metadata["Todo.ViewMode"]` 并 `SaveDebounced`）
  - `public const string ViewModeMetadataKey = "Todo.ViewMode";`
  - `public TodoCalendarViewModel? CalendarViewModel { get; }`（懒创建）

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public async Task ContentMode_PersistsToWidgetMetadata()
{
    var (vm, dir) = CreateEnvironment();
    try
    {
        await vm.InitializeAsync();
        vm.ContentMode = TodoContentViewMode.Calendar;

        Assert.Equal("Calendar", vm.Config.Metadata[TodoWidgetViewModel.ViewModeMetadataKey]);
    }
    finally { Cleanup(dir); }
}

[Fact]
public async Task ContentMode_DefaultsToCalendarWhenMetadataSaysSo()
{
    var config = new WidgetConfig { Id = "x", WidgetKind = WidgetKind.Todo };
    config.Metadata[TodoWidgetViewModel.ViewModeMetadataKey] = "Calendar";
    // 构造 VM 后断言 ContentMode == Calendar
}
```

（第二个测试复用 `TodoWidgetViewModelTests` 已有的构造 helper。）

- [ ] **Step 2: 运行确认失败**

- [ ] **Step 3: 实现**

- `TodoWidgetViewModel`：新增 enum + `ContentMode` 属性；构造时从 `_config.Metadata` 读取；setter 写入并 `_settingsService?.SaveDebounced()`。
- `CalendarViewModel` 懒创建方法 `EnsureCalendarViewModel()`：用同一个 `_store` 构造 `TodoCalendarViewModel`（**共享同一个 store 路径，因此同一 repository**）。
- XAML：工具区加两个 `ToggleButton`（列表/月历图标）；`Grid` 内容区放 `ContentPresenter x:Name="CalendarHost"`，切到 Calendar 时 `CalendarHost.Content = new TodoCalendarContent(vm)` 并隐藏列表。
- AOT：把 `ContentMode` 加入 `TodoViewModels.AotBindableProperties` 的映射表。

- [ ] **Step 4: 跑全量测试**

Expected: 通过数 == 总数（注意 `WidgetContentFactoryTests` 等快照测试是否因 `Metadata` 新增键失败）

- [ ] **Step 5: 提交**

```bash
git add src/WinSticker/ViewModels/TodoWidgetViewModel.cs src/WinSticker/ViewModels/TodoViewModels.AotBindableProperties.cs src/WinSticker/Controls/WidgetContents/TodoWidgetContent.xaml src/WinSticker/Controls/WidgetContents/TodoWidgetContent.xaml.cs tests/WinSticker.Tests/
git commit -m "feat: Todo 组件支持列表/月历视图切换并持久化偏好"
```

---

### Task 8: `WidgetKind.Calendar` 类型接入

**Files:**
- Modify: `src/WinSticker/Models/WidgetConfig.cs:292`（枚举末尾）
- Modify: `src/WinSticker/Services/WidgetRegistry.cs:61-69`
- Modify: `src/WinSticker/Services/WidgetContentFactory.cs`（DescriptorList）
- Test: `tests/WinSticker.Tests/FeatureWidgetKindContractTests.cs`（更新精确顺序断言）

**Interfaces:**
- Produces: `WidgetKind.Calendar`（值 10）

- [ ] **Step 1: 更新契约测试（先写期望）**

```csharp
// FeatureWidgetKindContractTests 的精确顺序断言加入 Calendar
Assert.Equal([WidgetKind.QuickCapture, WidgetKind.Todo, WidgetKind.Music, WidgetKind.Weather, WidgetKind.Search, WidgetKind.Glance, WidgetKind.Calendar], FeatureWidgetSettings.FeatureKinds);
```

- [ ] **Step 2: 运行确认失败**

Expected: 断言失败（顺序中缺 Calendar）或编译失败

- [ ] **Step 3: 实现**

- `WidgetKind` 末尾加 `Calendar`（注释：独立的日历贴片，投影 Todo 的到期日）。
- `WidgetRegistry.CreateDefault()` 加 `new(WidgetKind.Calendar, CanCreateWindow: true, IsImplemented: true)`。
- `WidgetContentFactory.DescriptorList` 加 descriptor：`IsFeatureWidget: true`、`HasSettingsPage: true`、`SettingsSectionTag: "CalendarSettings"`、图标 `""`（Calendar，**不与 Glance 重复**）、`ChromeCategory`/`DefaultChromeMode` 参照 Todo。

- [ ] **Step 4: 跑全量**

Expected: 通过数 == 总数

- [ ] **Step 5: 提交**

```bash
git add src/WinSticker/Models/WidgetConfig.cs src/WinSticker/Services/WidgetRegistry.cs src/WinSticker/Services/WidgetContentFactory.cs tests/WinSticker.Tests/FeatureWidgetKindContractTests.cs
git commit -m "feat: 新增 WidgetKind.Calendar 日历贴片类型"
```

---

### Task 9: Calendar 贴片的 provider / adapter

**Files:**
- Create: `src/WinSticker/Services/TodoCalendarContentProvider.cs`
- Create: `src/WinSticker/Controls/WidgetContents/TodoCalendarContentAdapter.cs`
- Modify: `src/WinSticker/Services/WidgetContentFactory.cs:271-285`（注册 provider）
- Test: `tests/WinSticker.Tests/TodoCalendarContentProviderTests.cs`

**Interfaces:**
- Consumes: `TodoTargetResolver`(Task 0)、`TodoCalendarViewModel`(Task 4)、`TodoCalendarContent`(Task 6)
- Produces:
  - `internal sealed class TodoCalendarContentProvider : IWidgetContentProvider`（`WidgetKind => WidgetKind.Calendar`、`CanCreateDetachedContent => true`）
  - `public sealed class TodoCalendarContentAdapter : WidgetContentAdapterBase, IWidgetResponsiveLayoutContent, IWidgetInteractiveResizeContent, IWidgetFeedbackSource, IWidgetAddActionContent`

- [ ] **Step 1: 写失败测试（provider 解析）**

```csharp
[Fact]
public void CreateDetachedContent_WithoutSettingsService_Throws()
{
    var provider = new TodoCalendarContentProvider();
    var context = new WidgetContentProviderContext(loc, null, null, _ => descriptor);
    Assert.Throws<InvalidOperationException>(() =>
        provider.CreateDetachedContent(new WidgetConfig { Id = "cal", WidgetKind = WidgetKind.Calendar }, context));
}

[Fact]
public void CreateDetachedContent_ResolvesActiveTodoStoreNotOwnId()
{
    // settings 含 todo-b 启用；Calendar config id="cal"
    // 断言 adapter.ViewModel 的 store 路径键为 todo-b，而不是 cal
}
```

- [ ] **Step 2: 运行确认失败**

- [ ] **Step 3: 实现**

- Provider：`SettingsService is null` → `throw new InvalidOperationException(...)`；否则 `TodoTargetResolver.ResolveConfig(settings)`，为 null 时用一个**未持久化的** fallback config（`new WidgetConfig { Id = TodoTargetResolver.FallbackWidgetId, WidgetKind = TodoKind }`）建 store。
- Adapter：构造 `TodoCalendarViewModel`；**不实现** `IWidgetGroupContentCacheable`；`Dispose` 调 VM `Dispose`。
- `WidgetContentFactory.CreateContentProviders()` 加 `new TodoCalendarContentProvider()`。

- [ ] **Step 4: 运行确认通过**

- [ ] **Step 5: 提交**

```bash
git add src/WinSticker/Services/TodoCalendarContentProvider.cs src/WinSticker/Controls/WidgetContents/TodoCalendarContentAdapter.cs src/WinSticker/Services/WidgetContentFactory.cs tests/WinSticker.Tests/TodoCalendarContentProviderTests.cs
git commit -m "feat: 日历贴片 provider 经 resolver 读取当前启用的 Todo"
```

---

### Task 10: WidgetManager 全接线（handlers / windowProviders / 删除 / 重置 / 尺寸）

**Files:**
- Modify: `src/WinSticker/Services/WidgetManager.cs:589-626`（handlers）
- Modify: `src/WinSticker/Services/WidgetManager.cs:628-691`（windowProviders）
- Modify: `src/WinSticker/Services/WidgetManager.cs:2060-2091`（RemoveWidgetAsync）
- Modify: `src/WinSticker/Services/WidgetManager.FeatureWidgets.cs:1188-1197`（Reset）
- Modify: `src/WinSticker/Services/WidgetManager.FeatureWidgets.cs:370-380,420-440`（标题/尺寸 switch）
- Modify: `src/WinSticker/Services/WidgetManager.Groups.cs:696`（标题）
- Modify: `src/WinSticker/Services/WidgetManager.cs:2716`（尺寸 switch）
- Modify: `src/WinSticker/Services/WidgetTopologyLayoutService.cs:1281-1282`（`IsFixedContentKind`）
- Modify: `src/WinSticker/Services/WidgetCompactWarmupSchedulePolicy.cs:35`
- Test: `tests/WinSticker.Tests/`（新增删除语义测试）

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public async Task RemovingOneOfTwoCalendarWidgets_KeepsFeatureEnabled()
{
    // 建两个 WidgetKind.Calendar 配置 → RemoveWidgetAsync(第一个)
    // 断言 FeatureWidgetSettings.IsEnabled(settings, WidgetKind.Calendar) 仍为 true
}

[Fact]
public async Task RemovingLastCalendarWidget_DisablesFeature()
{
    // 只有一个 → 删除后 IsEnabled == false
}
```

（复用现有 WidgetManager 测试的构造方式。）

- [ ] **Step 2: 运行确认失败**

- [ ] **Step 3: 实现**

- handlers：`new(WidgetKind.Calendar, async _ => await CreateSingletonContentFeatureWidgetAsync(WidgetKind.Calendar), SetContentFeatureWidgetEnabledAsync, () => HideAndCloseFeatureWidgetAsync(WidgetKind.Calendar))`。
- windowProviders：仿 Todo 的 `CreateContentWidgetFromConfigAsync` 条目。
- `RemoveWidgetAsync`：在 Todo 分支之后加 Calendar 分支——**不删任何 store**（日历不拥有数据），只判断"是否还有其它 Calendar 实例"，没有才 `SetFeatureWidgetEnabledState(Calendar, false)`。
- Reset：把 Calendar 加进 `kind == WidgetKind.Glance` 那类判断旁的合适分支（Calendar 无自有 store，跳过 store reset）。
- `IsFixedContentKind`（`WidgetTopologyLayoutService.cs:1281`）决定"该 kind 的尺寸是否固定不可弹性缩放"。日历**可缩放**，因此**不要**把 Calendar 加进这个函数——保持它不在列表里。加一行注释说明这是有意的。
- 尺寸：Calendar 360×420。
- 预热阈值：Calendar 与 Todo 同档或略低。

- [ ] **Step 4: 跑全量**

- [ ] **Step 5: 提交**

```bash
git add src/WinSticker/Services/ tests/WinSticker.Tests/
git commit -m "feat: Calendar 贴片接入 WidgetManager 生命周期"
```

---

### Task 11: 剩余 switch 与图标/菜单

**Files:**
- Modify: `src/WinSticker/Services/WidgetTitleIconMode.cs:109-121`
- Modify: `src/WinSticker/Services/WidgetSettingsMenuHelper.cs:20-45`
- Modify: `src/WinSticker/Services/WidgetManager.FeatureWidgets.cs`（标题 switch）
- Modify: `src/WinSticker/Services/WidgetManager.Groups.cs`
- Test: 扩展 `WidgetTitleIconModeTests`、`WidgetSettingsMenuHelperTests`

- [ ] **Step 1: 扩展既有测试的期望**

在 `WidgetTitleIconModeTests` / `WidgetSettingsMenuHelperTests` 中加入 Calendar 的期望值。

- [ ] **Step 2: 运行确认失败**

- [ ] **Step 3: 实现**

四处 switch 各加 `WidgetKind.Calendar =>` 分支（图标 `Calendar`、菜单 `CalendarSettings`、标题键 `Calendar.Title`）。

- [ ] **Step 4: 跑全量**

- [ ] **Step 5: 提交**

```bash
git add src/WinSticker/Services/ tests/WinSticker.Tests/
git commit -m "feat: 日历贴片的图标模式与设置菜单"
```

---

### Task 12: i18n

**Files:**
- Modify: `src/WinSticker/Strings/*.json`（先 `ls` 核对实际文件数，不照抄"13 个"）
- Test: 扩展现有的语言键完整性测试（若存在，如 `LocalizationParityTests`）

- [ ] **Step 1: 写失败测试**

若已有"所有语言键一致"的测试，它会在加上新键后失败；否则新增一个断言所有语言文件都含 `Calendar.Title` 的测试。

- [ ] **Step 2: 运行确认失败**

- [ ] **Step 3: 实现**

需新增的键（zh-CN 先写好，其余语言翻译）：

| 键 | zh-CN |
|---|---|
| `Calendar.Title` | 日历 |
| `Calendar.Today` | 今天 |
| `Calendar.TitleFormat` | {0} 年 {1} 月 |
| `Calendar.MoreItems` | +{0} |
| `Calendar.NewTaskPlaceholder` | 添加任务 |
| `Calendar.Empty` | 还没有待办 |
| `Calendar.NoTodoWidget` | 先在设置中启用待办 |
| `WidgetContent.Calendar.StatusLabel` | 日历 |
| `WidgetContent.Calendar.StatusDescription` | 按月查看待办到期安排 |
| `WidgetViewMode.List` / `WidgetViewMode.Calendar` | 列表 / 月历 |

- [ ] **Step 4: 跑全量**

- [ ] **Step 5: 提交**

```bash
git add src/WinSticker/Strings/
git commit -m "feat: 日历功能的界面文案"
```

---

### Task 13: AOT 清单、冒烟与全量回归

**Files:**
- Modify: `scripts/publish-aot-audit.ps1`（source 清单**末尾**追加新文件）
- Modify: `src/WinSticker/App.AotManagedUiSmoke.cs`（若需要 Calendar 冒烟）
- Test: 全量

- [ ] **Step 1: 追加 AOT source 清单条目（末尾）**

`TodoTargetResolver.cs`、`TodoCalendarProjection.cs`、`TodoCalendarViewModel.cs`、`TodoCalendarDayCell.cs`、`TodoCalendarContent.xaml.cs`、`TodoCalendarContentAdapter.cs`、`TodoCalendarContentProvider.cs`。

- [ ] **Step 2: 构建**

停进程 → build → 校验 `WinSticker.dll` 时间戳。

- [ ] **Step 3: 跑全量测试**

Expected: **通过数 == 总数**

- [ ] **Step 4: 启动并报告路径**

从 `src/WinSticker/bin/Debug/net10.0-windows10.0.22621.0/WinSticker.exe` 启动；验证：设置中能看到"日历"；添加后贴片出现；在 Todo 里切的月历与贴片数据一致。

- [ ] **Step 5: 提交**

```bash
git add scripts/publish-aot-audit.ps1 src/WinSticker/App.AotManagedUiSmoke.cs
git commit -m "chore: 日历组件纳入 AOT 审计清单并补冒烟"
```

---

## 自审记录

见文档末尾「Self-Review」——本计划已按 spec 逐节对照，覆盖 §7.1-§7.6 与 §8 全部条目。
