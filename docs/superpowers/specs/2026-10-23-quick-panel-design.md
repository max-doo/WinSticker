# DeskBox Quick Panel (快捷弹窗) Design Specification

## 1. Overview & Goals

The Quick Panel is a central, floating productivity popup for DeskBox, modeled on the implementation logic and capabilities of WorkCat's `QuickPanelWindow`, while completely adopting DeskBox's WinUI 3 Fluent Design System aesthetic and architecture.

### Key Goals
- **Global Hotkey & Tray Summon:** Provide seamless access via a configurable global hotkey (defaulting to `Alt+Space` or `Ctrl+Alt+V`) and a system tray menu item ("打开快捷面板").
- **Window Management & Pinning:** Support borderless floating window with DWM rounded corners, native backdrop (Mica / Desktop Acrylic / Solid surface matching user preferences), smooth fade/slide entrance animations, and a Pin toggle (`IsPinned`). When unpinned, clicking outside (`WindowActivationState.Deactivated`) hides the panel.
- **Target Window Tracking & Auto-Paste:** Remember the HWND of the foreground window active prior to popup activation (`_lastActiveHwnd`), enabling clipboard items to be pasted directly into target applications with double-click or Enter.
- **Three Core Functional Tabs:**
  1. **剪贴板 (Clipboard History):** Filter chips (全部, 文本, 图片, 链接, 收藏), real-time search, count display, favorite toggles, copy, delete, and auto-paste.
  2. **待办 (Todo Planning):** Status filters (今天, 逾期, 未安排, 时间线, 已完成) with live badges, "+" quick composer (Title, Note, Date selector, Priority), task completion checkboxes, editing, and deletion.
  3. **便签 (Quick Notes):** Multi-note tab strip, auto-naming / custom title, multiline note editor with auto-save debouncing, task extraction ("提取任务" into Todos), and note deletion.

---

## 2. Architecture & Component Structure

```
src/DeskBox/
├── Views/
│   ├── QuickPanelWindow.xaml               # WinUI 3 window layout with backdrop, header & tabs
│   ├── QuickPanelWindow.xaml.cs            # Window lifecycle, HWND tracking, hotkey & paste interop
│   ├── Controls/
│   │   ├── QuickPanelClipboardView.xaml    # Tab 1: Clipboard history list, filters, search & actions
│   │   ├── QuickPanelClipboardView.xaml.cs
│   │   ├── QuickPanelTodoView.xaml         # Tab 2: Todo multi-view, composer & list
│   │   ├── QuickPanelTodoView.xaml.cs
│   │   ├── QuickPanelNotesView.xaml        # Tab 3: Note tabs, editor & task extractor
│   │   └── QuickPanelNotesView.xaml.cs
├── ViewModels/
│   ├── QuickPanelViewModel.cs              # Central ViewModel coordinating 3 features
│   ├── QuickPanelViewModel.Clipboard.cs    # Clipboard filtering, favorites, search & paste logic
│   ├── QuickPanelViewModel.Todo.cs         # Todo CRUD, views, date projections & filters
│   └── QuickPanelViewModel.Notes.cs        # Note multi-tab management, autosave & task extraction
├── Models/
│   ├── QuickPanelTab.cs                    # Tab enumeration (Clipboard, Todo, Notes)
│   ├── QuickPanelFilter.cs                 # Clipboard filter enumeration (All, Text, Images, Links, Favorites)
│   └── QuickPanelTodoViewMode.cs           # Todo view mode (Today, Overdue, Unscheduled, Timeline, Completed)
└── Services/
    └── QuickPanelService.cs                # Coordinates window singleton, hotkey, and tray integration
```

---

## 3. Detailed Component Designs

### 3.1 Window Host & Win32 Interop (`QuickPanelWindow`)
- **Window Dimensions:** Default width 500px, height 620px (min 420x480).
- **Backdrop:** Applies DeskBox's native system backdrop via `MicaController` or `DesktopAcrylicController` configured through `ThemeService` / `SettingsService`.
- **DWM Attributes:** Window corners rounded with `DWMWA_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND`.
- **Top Header:**
  - App Icon (DeskBox mark) + localized title ("快捷面板").
  - Search trigger / quick filter.
  - Pin Toggle Button: `FontIcon Glyph="&#xE718;"` (Pin / Unpin).
  - Settings Button: `FontIcon Glyph="&#xE713;"` (Opens DeskBox Settings).
  - Close Button: `FontIcon Glyph="&#xE8BB;"` (Hides window).
- **Focus & Dismissal:**
  - On `Activated` event: if `WindowActivationState.Deactivated` and `!IsPinned`, trigger `HidePopup()`.
  - When invoked: query Win32 `GetForegroundWindow()` to record `_lastActiveHwnd` before activating.
- **Auto-Paste Routine:**
  1. User double-clicks an item or clicks "粘贴".
  2. If unpinned, immediately invoke `HidePopup()`.
  3. Activate `_lastActiveHwnd` using `SetForegroundWindow`, `BringWindowToTop`, and `AttachThreadInput`.
  4. Send keyboard input: `Ctrl+V` keydown and keyup.

### 3.2 Clipboard History Tab
- **Data Source:** `QuickCaptureService` and `QuickCaptureClipboardService`.
- **Filter Bar:** Segmented/Pill buttons:
  - 全部 (All)
  - 文本 (Text)
  - 图片 (Images)
  - 链接 (Links)
  - 收藏 (Favorites - maps to `IsPinned`)
- **Search Box:** Instant query filtering across clipboard items.
- **Item Row:**
  - Text item: typography following DeskBox style, 2-line max ellipsis, source label, relative timestamp.
  - Image item: thumbnail preview using `QuickCaptureService.GetOrCreateImageThumbnailPathAsync`.
  - Actions on row: Star (Toggle Favorite / Pinned), Copy, Paste, Delete.
- **Count & Empty State:** Shows total active items (e.g., "15 条记录"); shows friendly empty state illustration when list is empty.

### 3.3 Todo Management Tab
- **Data Source:** Connects to active `TodoWidgetStore` / `TodoItem` repository.
- **View Modes:**
  - 今天 (Today) — with count badge.
  - 逾期 (Overdue) — with warning count badge.
  - 未安排 (Unscheduled) — with count badge.
  - 时间线 (Timeline) — chronological grouping.
  - 已完成 (Completed) — completed items archive.
- **Todo Composer:**
  - Collapsible / expandable or header card.
  - Title input with Enter to submit.
  - Date chips: "今天", "明天", "未安排", or DatePicker.
  - Priority toggles: Normal / High (重要).
- **Todo Item Row:**
  - Checkbox with smooth transition and strikethrough text upon completion.
  - Due date badge (highlighted red when overdue, accent when today).
  - Priority indicator.
  - Hover action buttons: Edit, Delete.

### 3.4 Notes Management Tab
- **Data Source:** Stored via `QuickCaptureService` (`QuickCaptureItemType.Text` items with note titles) or note slice.
- **Multi-Note Tab Strip:**
  - Horizontal list of note tabs.
  - Tab close button ("×") visible when more than one note exists.
  - "+" Button to create a new blank note.
- **Note Editor:**
  - Note Title TextBox (defaults to "便签 1", "便签 2" or custom).
  - Note Content multiline TextBox with word wrap and smooth scrolling.
  - Auto-save debounced (350ms) updating status text ("已保存" / "保存中...").
- **Task Extractor ("提取任务"):**
  - Scans note body for lines formatted as `- [ ] <task>`, `* <task>`, `- <task>`, or `1. <task>`.
  - Shows extraction preview dialog or direct confirmation.
  - Appends extracted tasks directly to the Todo store!

---

## 4. Integration with DeskBox

1. **Settings Integration (`AppSettings`):**
   - Hotkey configuration for Quick Panel (e.g. `QuickPanelHotkeyEnabled`, `QuickPanelHotkeyModifiers`, `QuickPanelHotkeyKey`, default `Alt+Space`).
   - Preference for default active tab (Clipboard, Todo, or Notes).
2. **Tray Menu Integration (`App.Tray.cs`):**
   - Add "打开快捷面板" (Open Quick Panel) menu entry.
3. **Application Lifecycle (`App.xaml.cs`):**
   - Register global hotkey via `GlobalHotkeyService`.
   - Host `QuickPanelWindow` singleton, cleanly recreating if discarded.
4. **Native AOT & Trimming Compatibility:**
   - Ensure all ViewModels, models, and JSON serialization structures are decorated for Native AOT and compiled with source generators.
