# DeskBox Quick Panel (快捷弹窗) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a central Quick Panel (快捷弹窗) popup window in DeskBox with Clipboard history (with auto-paste to target window), Todo planning (with status views and quick composer), and Quick Notes (with multi-tabs, autosave, and task extraction), fully styled with DeskBox's WinUI 3 Fluent Design aesthetic.

**Architecture:** A standalone floating WinUI 3 window (`QuickPanelWindow`) backed by `QuickPanelViewModel` and specialized sub-views (`QuickPanelClipboardView`, `QuickPanelTodoView`, `QuickPanelNotesView`), integrated directly with DeskBox's existing `QuickCaptureService`, `QuickCaptureClipboardService`, and `TodoWidgetStore`, with Win32 HWND tracking for auto-paste and global hotkey invocation.

**Tech Stack:** C# .NET 10, Windows App SDK 2.5.1 / WinUI 3, CommunityToolkit.Mvvm, Win32 P/Invoke, System.Text.Json source generators for Native AOT.

---

### Task 1: Data Models & Settings for Quick Panel

**Files:**
- Create: `src/DeskBox/Models/QuickPanelModels.cs`
- Modify: `src/DeskBox/Models/CoreSettingsSlice.cs`
- Modify: `src/DeskBox/Models/AppSettings.cs`
- Modify: `src/DeskBox/Services/SettingsService.cs`
- Test: `tests/DeskBox.Tests/QuickPanelModelsTests.cs`

- [ ] **Step 1: Write unit tests for QuickPanel enums, models, and settings normalization**
- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement `QuickPanelModels.cs` with `QuickPanelTab`, `QuickPanelFilter`, and `QuickPanelTodoViewMode`**
- [ ] **Step 4: Add hotkey and preference properties to `CoreSettingsSlice`, `AppSettings`, and `SettingsService`**
- [ ] **Step 5: Run tests to verify they pass**

---

### Task 2: QuickPanelViewModel Core & Clipboard History Logic

**Files:**
- Create: `src/DeskBox/ViewModels/QuickPanelViewModel.cs`
- Create: `src/DeskBox/ViewModels/QuickPanelViewModel.Clipboard.cs`
- Modify: `src/DeskBox/DeskBox.csproj`
- Test: `tests/DeskBox.Tests/QuickPanelClipboardViewModelTests.cs`

- [ ] **Step 1: Write failing tests for Clipboard history filtering (All, Text, Images, Links, Favorites), search, favorite toggle, and delete**
- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement `QuickPanelViewModel.cs` base and `QuickPanelViewModel.Clipboard.cs` utilizing `QuickCaptureService`**
- [ ] **Step 4: Run tests to verify they pass**

---

### Task 3: QuickPanelViewModel Todo Planning Logic

**Files:**
- Create: `src/DeskBox/ViewModels/QuickPanelViewModel.Todo.cs`
- Test: `tests/DeskBox.Tests/QuickPanelTodoViewModelTests.cs`

- [ ] **Step 1: Write failing tests for Todo view filtering (Today, Overdue, Unscheduled, Timeline, Completed), count badges, task creation, editing, status toggle, and deletion**
- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement `QuickPanelViewModel.Todo.cs` interacting with `TodoWidgetStore` / `TodoItem`**
- [ ] **Step 4: Run tests to verify they pass**

---

### Task 4: QuickPanelViewModel Notes & Task Extractor Logic

**Files:**
- Create: `src/DeskBox/ViewModels/QuickPanelViewModel.Notes.cs`
- Test: `tests/DeskBox.Tests/QuickPanelNotesViewModelTests.cs`

- [ ] **Step 1: Write failing tests for multi-note tabs, note autosave debouncing, and extracting checklist items (`- [ ]`, `*`, `-`, `1.`) into Todos**
- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement `QuickPanelViewModel.Notes.cs` supporting tab strip, active note editing, autosave, and task parser**
- [ ] **Step 4: Run tests to verify they pass**

---

### Task 5: QuickPanel UI Controls (Clipboard, Todo, Notes Views)

**Files:**
- Create: `src/DeskBox/Controls/QuickPanelClipboardView.xaml` and `.xaml.cs`
- Create: `src/DeskBox/Controls/QuickPanelTodoView.xaml` and `.xaml.cs`
- Create: `src/DeskBox/Controls/QuickPanelNotesView.xaml` and `.xaml.cs`
- Modify: `src/DeskBox/DeskBox.csproj`

- [ ] **Step 1: Implement `QuickPanelClipboardView` with pill filter bar, search input, record list, hover action buttons, and image thumbnails**
- [ ] **Step 2: Implement `QuickPanelTodoView` with view mode chips + badges, "+" composer card, task checklist with strikethrough, due date chips, and priority tags**
- [ ] **Step 3: Implement `QuickPanelNotesView` with note tabs bar, note editor, autosave indicator, and "提取任务" button**
- [ ] **Step 4: Ensure all bindings and theme resources conform to DeskBox's WinUI 3 Fluent design language**

---

### Task 6: QuickPanelWindow Host & Win32 Interop

**Files:**
- Create: `src/DeskBox/Views/QuickPanelWindow.xaml` and `.xaml.cs`
- Modify: `src/DeskBox/DeskBox.csproj`

- [ ] **Step 1: Define `QuickPanelWindow.xaml` layout: title bar with logo, title, Pin toggle button, settings button, close button, Tab header, and views host**
- [ ] **Step 2: Implement `QuickPanelWindow.xaml.cs`: Mica/Acrylic backdrop, entrance animation, DWM rounded corners, focus management**
- [ ] **Step 3: Implement `_lastActiveHwnd` tracking on invocation and `WindowActivationState.Deactivated` auto-hide when unpinned**
- [ ] **Step 4: Implement `PasteToTargetWindowAsync(targetHwnd)` synthesizing `Ctrl+V` to paste selected clipboard items directly into target window**

---

### Task 7: System Integration (Hotkey, Tray Menu, Settings)

**Files:**
- Create: `src/DeskBox/Services/QuickPanelService.cs`
- Modify: `src/DeskBox/App.xaml.cs`
- Modify: `src/DeskBox/App.Tray.cs`
- Modify: `src/DeskBox/Views/SettingsWindow.HotkeyAndAppearance.cs` (or settings sections)

- [ ] **Step 1: Implement `QuickPanelService` to manage `QuickPanelWindow` instance lifecycle**
- [ ] **Step 2: Register global hotkey (`Alt+Space` or user-defined) in `App.xaml.cs` to toggle Quick Panel**
- [ ] **Step 3: Add "打开快捷面板" to system tray context menu in `App.Tray.cs`**
- [ ] **Step 4: Add hotkey configuration to Settings view and viewmodel**

---

### Task 8: Verification & Workflow Handoff

**Files:**
- All created and modified files

- [ ] **Step 1: Run the full test suite with `dotnet test .\tests\DeskBox.Tests\DeskBox.Tests.csproj --no-restore --verbosity:minimal -p:Platform=x64`**
- [ ] **Step 2: Build DeskBox Debug target**
- [ ] **Step 3: Start DeskBox and verify the Quick Panel opens, switches tabs, copies/pastes, manages todos, and edits notes**
- [ ] **Step 4: Verify all tests pass with zero failures**
