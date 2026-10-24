# 时光（Glance）时钟组件改版方案：透明背景 · 天气行 · 多时区

> 状态：方案待评审　｜　作者：Max + 龙龙　｜　日期：2026-10-23
> 目标组件：`Controls/WidgetContents/GlanceWidgetContent`（设置页中文名「时光」，实例名「时光格子」）

---

## 1. 调研：手机时钟组件到底怎么设计的

### 1.1 四个主流样本

| 平台 / 版本 | 时钟组件形态 | 天气 | 多时区 | 透明背景 | 关键手法 |
|---|---|---|---|---|---|
| iOS（时钟 App 世界时钟） | 小：1 城；中：2–4 城；大：4 城 | 独立天气组件，不混排 | 城市名 + 时间 + 相对日差（Today / Tomorrow / +9HRS） | 仅锁屏/StandBy 半透明，主屏为实底 | 极致的"一行一城"压缩表达 |
| Pixel / Google Clock 7.1（Material You） | 五种样式：Analog、Digital、Digital Stacked、World（双时区） | At a Glance 单独承载 | World 样式直接双时区并排 | **官方一等样式**："Digital Stacked" 与 "World" 自带 Transparent 变体 | 透明是"样式"，不是把背景删掉 |
| 三星 One UI | 时钟 + 天气一体 | 图标 + 温度 + 高低温，可挂小时预报条 | 世界时钟组件最多 4 城 | 可选透明/模糊 | 天气是"一行"，不是一块面板 |
| 小米 HyperOS / 澎湃 OS | 纯数字大时钟（2×1 常用） | 精简单行气温 | 需装世界时钟组件 | 背景透明度可拉到最低 | 社区标准玩法：时钟/日期/天气三段式、逐段调透明度 |

补充：第三方美化类组件（KWGT、Color Widgets、万象小组件）把可自定义项固化成五档——背景（纯色/透明/图片）、字体、字号、整体透明度、圆角。说明用户对"透明"的期望是**逐组件可控**，而不是全局一刀切。

### 1.2 可以照搬的五条结论

1. **信息分层最多三层**：主时间（唯一最大视觉权重）→ 天气/日期（次级，一行）→ 时区列表（三级，可压缩）。手机组件在"扫一眼"预算内从不放第四层。
2. **透明必须配可读性补偿**。iOS/Android 的透明样式都靠字重 + 颜色自适应 + 极轻衬底来保命，而不是"纯浮空文字"。这是本方案里最容易翻车的一环。
3. **时区信息要压缩成"城市 + 时间 + 相对日差"**，不显示 UTC 偏移、不显示完整时区名、不在每一行重复"明天/今天"以外的形容词。
4. **天气是摘要行**：图标 + 温度（+ 高低温/降水概率），点击才展开详情。
5. **尺寸决定信息量**，而不是靠用户手调。组件按可用宽高自动换档。

### 1.3 桌面与手机的差异（不能照抄的地方）

| 维度 | 手机组件 | 桌面组件（DeskBox） | 方案含义 |
|---|---|---|---|
| 画布 | 固定网格（2×1、4×2） | 自由缩放，可 200×120 也可 600×400 | 必须做"连续自适应"，而非固定档位裁剪 |
| 背景 | 壁纸，通常较柔和 | 任意壁纸/图片/纯色，可能高对比 | 可读性需要自动 + 手动双保险 |
| 常驻 | 亮屏才看 | 全天常驻，可能多实例 | 刷新成本要算电；时区列表要能按实例独立配置 |
| 已有同类 | Clock 与 Weather 是两个 App | 已有独立天气组件 | 时钟里的天气只做摘要，不做第二套天气详情，避免功能重复 |

---

## 2. 现状盘点（代码级）

| 能力 | 现状 | 位置 |
|---|---|---|
| 时间/日期/星期/年 | 已有，可逐项显隐 | `GlanceWidgetViewModel`（`ShowTime`/`ShowDate`/`ShowWeekday`/`ShowYear`） |
| 布局 | 4 种：`Immersive` / `Centered` / `Editorial` / `Calendar` | `GlanceLayoutMode` |
| 背景图 | Bing / 在线图库 / 本地文件/文件夹，带可读性遮罩、轮播、转场 | `GlanceImageService`、`BackgroundSource` |
| 材质与透明度 | **全局设置**（`WidgetMaterialType`：Acrylic / AcrylicBase / Mica / MicaAlt / Solid；`WidgetOpacity` 0–1） | `WidgetShellSettingsSlice` → `WidgetWindowBase.ApplyBackdropPreference` |
| 多实例 | 每个时光格子一份独立 `glance.json` | `GlanceWidgetStore(widgetId)` |
| 天气 | 完整：MSN + OpenMeteo、地理编码、缓存、4 套图标 | `WeatherService`、`WeatherCacheStore.Current`、`Assets/WeatherIcons/{fluent,flat,line}` |
| 时区 | **零基础**，全库只有待办通知用到本机 `TimeZoneInfo.Local` | — |
| 持久化 | `GlanceWidgetData.CurrentVersion = 10`，`Normalize()` 统一收敛 | `Models/GlanceWidgetData.cs`、`Services/GlanceWidgetStore.cs` |
| 显示项开关入口 | 右键菜单 Toggle，设置页无 | `GlanceWidgetContextMenuBuilder` |
| AOT | Native AOT 发布，绑定属性必须登记白名单 | `GlanceWidgetViewModel.AotBindableProperties.cs` |
| 本地化 | 13 个 `Strings/*.json`，键前缀 `Glance.` | `Strings/zh-CN.json` 等 |

### 三个硬约束（决定实现路径）

1. **材质与透明度目前是全局的**。要让"只有时钟透明、文件格子保持玻璃"，必须在窗口层引入 per-widget override，而不是改全局设置语义。
2. **没有 `None`/`Transparent` 材质**。`WidgetMaterialKinds` 只有 5 种，透明需要新增一种"无材质"路径：不挂 controller、不刷 accent、表面全透明。
3. **AOT + JsonSerializerContext**。新增的每个绑定属性都要进白名单，新增字段要能被 `GlancePreferencesJsonContext` 序列化，否则 Debug 好用、AOT 版静默失效——这是本项目最容易漏的坑。

---

## 3. 方案总览

保持现有"一个可缩放画布"的架构，把内容改成**可组合的三段**：

```
┌─────────────────────────────┐
│  09:41            ← 主时间（唯一大字）
│  10月23日 周五     ← 日期行（次级）
│  ☀ 21° 晴  12°~24° ← 天气行（摘要，可关）
│  ─────────────────────────  ← 可选细分隔线（透明模式下不画）
│  纽约   21:41  +1天        ← 世界时钟条（城市 · 时间 · 相对日差）
│  伦敦   14:41  今天        │
│  东京   22:41  今天        │
└─────────────────────────────┘
```

三条产品原则：

- 时钟里的天气**只做一行摘要**，不展开逐时/7 天——那是天气组件的职责。
- 透明模式下**不画边框、不画分隔线、不画卡片**，靠文字权重和留白分区。
- 时区列表**跟随 12/24 小时制**，永不显示 UTC 偏移数字。

### 尺寸档位（按可用宽高自动切换，用户无需设置）

| 档位 | 触发条件（可用区域） | 时间 | 日期 | 天气 | 时区 | 备注 |
|---|---|---|---|---|---|---|
| S | 宽 < 220 或 高 < 150 | 中号 | 仅星期 | 图标 + 温度（无文字描述） | 隐藏 | 极简角落钟 |
| M | 220 ≤ 宽 < 340 | 大号 | 日期 + 星期 | 图标 + 温度 + 描述 | 最多 2 城 | 默认推荐 |
| L | 宽 ≥ 340 或 高 ≥ 240 | 特大 | 日期 + 星期 + 年（可选） | 图标 + 温度 + 高低温 + 降水 | 最多 5 城（两列或列表） | 桌面主时钟 |
| Calendar | 现有日历布局 | 小号 | 日期行 | 日历标题栏内联 | 折叠到日历上方一行 | 不改原有排布 |

档位切换沿用现有 `_availableWidth/_availableHeight` 计算路径，不引入新的布局枚举（新增枚举会让日历布局与档位互斥，代价更大）。

---

## 4. 详细设计

### 4.1 透明背景

**交互**：右键菜单与设置页各加一项"背景材质"，三选一：

| 模式 | 视觉效果 | 实现 |
|---|---|---|
| 跟随系统（默认） | 现在的外观 | 走全局 `WidgetMaterialType/WidgetOpacity` |
| 透明 | 只剩文字浮在壁纸上 | 新材质 `None`：跳过所有 controller/accent，表面 `Transparent`，边框 `None`，分隔线隐藏 |
| 玻璃 | 磨砂但很淡（透明度可调 0–100%） | 复用 `Acrylic` 路径，per-widget 覆盖 `WidgetOpacity` |

**技术路径（最小侵入）**：

1. `WidgetMaterialKinds` 增加 `None`；`SupportsOpacity` / `SupportsMaterialIntensity` 显式返回 false；`WindowsCompatibilityService.ResolveWidgetMaterialType` 放行 `None`（不被降级成 Acrylic）。
2. `WidgetWindowBase` 增加两个虚钩子：`protected virtual string? MaterialTypeOverride => null;` 与 `protected virtual double? SurfaceOpacityOverride => null;`；`ApplyBackdropPreference` / `ApplySurfaceStyle` 优先读 override。`ContentWidgetWindow` 在"当前内容为 Glance 且该实例选择透明/玻璃"时返回对应值，其余组件行为不变。
3. `None` 分支：`DwmSetWindowAttribute(SYSTEMBACKDROP_TYPE = NONE)`、`DisableAccentPolicy`、释放 controller、`BackgroundSurface` 全透明、`BorderThickness = 0`。窗口本身仍是 `Background="Transparent"`（已是现状），所以拖拽/缩放区域不受影响。
4. **可读性补偿（必做）**，三档：
   - `Auto`（默认）：取当前壁纸/背景图主色亮度 → 自动选深色字/浅色字（复用 `GlanceImagePaletteService` 已有的调色板采样，纯色壁纸走 `SystemParametersInfo` 取壁纸后采样一次并缓存）。
   - `Light` / `Dark`：用户手动锁定。
   - 文字衬底：透明 + 自动模式时，在文字块背后加一层**极轻的实色衬底**（不透明度 8%–14%，圆角 12，仅覆盖文字边界，随文字块尺寸收缩），替代阴影——本项目禁用投影，衬底是更干净的等价手段。
5. **命中测试**：整窗保持可命中（否则无法拖动/调整），但键盘焦点框、hover 按钮在透明模式下改为仅 hover 时淡入。

### 4.2 天气行

**数据**：不新增网络栈。复用 `WeatherService` + `WeatherCacheStore.Current`（单例缓存 + 30 分钟 TTL + 退避策略），时钟只读缓存，缓存新鲜度不足或为空时才触发一次后台刷新；刷新失败沿用旧值并降级为"仅温度"。

**展示**：

```
☀ 21° 晴  12°~24°
└图标 └当前 └描述 └今日区间（L 档才有）
```

- 单位跟随全局 `WeatherTemperatureUnit`，允许本实例覆盖。
- 图标复用 `Assets/WeatherIcons` 的 4 套风格（Fluent / DeskBox / Flat / Line），本实例可单独选。
- 数据源跟随全局（MSN / OpenMeteo），**不提供 per-widget 数据源**——避免同一台机器两套地理编码。
- 地点：默认跟随全局定位；提供"本实例锁定城市"（复用 `SearchCityAsync`），适合"桌面上永远显示老家天气"。
- 点击行为：单点 = 打开（聚焦）天气组件；无天气组件时 = 打开天气设置分区。
- 失败/离线降级顺序：完整行 → 温度 + 图标 → 隐藏天气行（不显示 `--°` 占位）。

**隐私**：定位沿用全局开关；关闭定位且未锁定城市时不显示天气行（而不是猜一个城市）。

### 4.3 多时区

**数据模型**（每个时光格子独立）：

```csharp
public sealed class GlanceTimeZoneEntry
{
    public string TimeZoneId { get; set; } = string.Empty;   // TimeZoneInfo.Id，如 "Asia/Tokyo"
    public string? DisplayName { get; set; }                 // 用户可改：默认为城市名
    public bool ShowDate { get; set; }                       // 该行是否显示日期
}

// GlanceWidgetData 新增
public bool ShowWeather { get; set; } = false;
public bool ShowTimeZones { get; set; } = false;
public List<GlanceTimeZoneEntry> TimeZones { get; set; } = [];
public string? WeatherCityOverride { get; set; }
public double? WeatherLatitudeOverride { get; set; }
public double? WeatherLongitudeOverride { get; set; }
public string? WeatherIconStyleOverride { get; set; }
public string? WeatherTemperatureUnitOverride { get; set; }
public GlanceMaterialMode MaterialMode { get; set; } = GlanceMaterialMode.FollowSystem;
public double? MaterialOpacityOverride { get; set; }
public GlanceTextContrastMode TextContrast { get; set; } = GlanceTextContrastMode.Auto;
```

**时区目录**：

- 首选 `TimeZoneInfo.GetSystemTimeZones()`（Win11 约 140 条），`FindSystemTimeZoneById` 包 try/catch，无效 Id 在 `Normalize()` 里丢弃——注册表被改坏（常见于精简系统）时不能让整个组件崩。
- 叠加**精选城市映射表**（约 40 条：上海/北京/香港/台北/东京/首尔/新加坡/曼谷/迪拜/伦敦/巴黎/柏林/莫斯科/纽约/洛杉矶/旧金山/西雅图/多伦多/圣保罗/悉尼/奥克兰…），用于：① 中文显示名；② 搜索时的首选命中；③ 防止用户看到 `China Standard Time` 这种系统名。
- 搜索：本地模糊匹配（城市中文名 + 英文名 + 时区 Id），**不走网络**。跨语言用户可搜 "Tokyo"。

**渲染规则**（关键，决定"像不像手机"）：

| 元素 | 规则 | 示例 |
|---|---|---|
| 城市名 | 左对齐，`TextFillColorSecondary`，单行截断 | 纽约 |
| 时间 | 右对齐，等宽数字（`Typography.NumeralAlignment="Tabular"`），与主时间同时刷新 | 21:41 |
| 相对日差 | 与本地日期比较：−1 / 0 / +1 → `昨天` / 空 / `明天`；差 2 天以上显示 `+2天` | +1天 |
| 时差 | **不显示**（用户能从日差 + 时间自行判断，多一个字都会破坏节奏） | — |
| 昼夜 | 该地 6:00–18:00 视为昼：夜里时间用 `TextFillColorSecondary` 降一档亮度 | 自动 |
| 排序 | 用户拖拽排序，默认按 UTC 偏移升序（自动处理夏令时） | — |
| 容量 | S 档 0、M 档 2、L 档 5 | — |
| 刷新 | 复用现有分钟级 `_clockTimer`，仅在分钟边界算 N 次 `TimeZoneInfo.ConvertTime`；**不额外起定时器** | — |

**夏令时**：一律走 `TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz)`，绝不缓存固定偏移；切换瞬间可能出现的 "跳过/重复 1 小时" 由 `ConvertTime` 自然处理，UI 只显示结果。

### 4.4 布局与渲染结构

```
Grid (GlanceWidgetContent 根)
 ├ ImageBackground（现有）
 ├ ReadabilityLayer（现有）
 ├ ContentStack（新增容器，Padding 由档位决定）
 │   ├ PrimaryTimeBlock（时间 + 日期行，现有三段布局逻辑保留）
 │   ├ WeatherRow（新）
 │   └ WorldClockBlock（新，ItemsRepeater）
 └ ActionLayer / CalendarLayoutRoot（现有）
```

- 使用 `ItemsRepeater` + `VirtualizingLayout` 的简化版（时区最多 5 行，直接 `StackPanel` 更省事且 AOT 友好——**选 StackPanel**，避免 virtualization 在 AOT 下的模板解析问题）。
- 透明模式：`ContentStack` 外不加任何 Border；三段之间用 `Spacing`（8–14）分区，不画线。
- 玻璃模式：保留现有卡片外观，天气行/时区条内嵌到卡片内。

### 4.5 设置界面与右键菜单

**右键菜单**（`GlanceWidgetContextMenuBuilder`）新增分组：

- 显示项：`天气` / `世界时钟` （沿用现有 Toggle 构造）
- 背景材质：`跟随系统` / `透明` / `玻璃`（Radio）
- 世界时钟：`管理时区…` → 打开设置页对应分区

**设置页**（`Views/SettingsSections/GlanceWidgetSettingsSection.xaml`）新增两个 Expander：

1. **天气**：显示开关、图标风格、温度单位（跟随/覆盖）、地点（跟随定位/锁定城市 + 搜索框）、点击行为。
2. **世界时钟**：开关、城市列表（可拖拽 + 上移/下移按钮，每行显示城市名可编辑、是否显示日期、删除）、"+ 添加城市" 搜索框、容量提示（当前档位最多 N 个）。

**一致性**：所有新开关都通过 `GlanceWidgetSettingsPolicy` 收敛（与现有 `SetDisplayElement` 同路径），保证右键菜单与设置页不会各写一套逻辑。

### 4.6 持久化与迁移

- `GlanceWidgetData.CurrentVersion` 10 → 11。
- 迁移为**纯增量**：旧 `glance.json` 反序列化后新字段取默认值（`ShowWeather=false`、`ShowTimeZones=false`），即"默认行为与今天完全一致"，现有用户零感知。
- `Normalize()` 追加：时区列表去空 / 去重（按 TimeZoneId）/ 截断到 20 条（存储上限，渲染再按档位截断）/ 无效 Id 丢弃 / `MaterialOpacityOverride` 夹到 0–1 / 枚举非法值回落。
- 备份与云同步：`GlanceWidgetData` 已在备份切片内，新增字段自然随云备份走；需在备份 schema 兼容性检查里确认反序列化容错（缺字段 → 默认值）。

### 4.7 AOT / 本地化 / 可访问性

- **AOT**：`GlanceWidgetViewModel.AotBindableProperties.cs` 白名单补充新绑定属性（`WeatherSummary`、`WeatherIconSource`、`WeatherVisible`、`TimeZoneRows`、`ShowWeatherRow`、`ShowWorldClockBlock`、`HasTimeZones`、`IsTransparentSurface`、`ContrastForeground`…）。`GlanceTimeZoneRow` 这类行模型若被 XAML 绑定，必须标 `[WinRT.GeneratedBindableCustomProperty]`（参考 `GlanceCalendarDayDecoration` 的写法）。
- **本地化**：新增约 22 个键，前缀 `Glance.Weather.*` / `Glance.TimeZone.*` / `Glance.Material.*` / `Glance.Contrast.*`，先补 `zh-CN` + `en-US`，其余 11 语言走既有回退并排入后续批次。
- **可访问性**：每行时区 `AutomationProperties.Name` = "纽约 21点41分 明天"；天气行 = "晴 21 度，今日 12 到 24 度"。

---

## 5. 分阶段实施

| 阶段 | 内容 | 产出 | 预估 |
|---|---|---|---|
| P0 | 方案定稿 + 数据模型评审 + 迁移方案确认 | 本文档 + 评审结论 | 0.5d |
| P1 | 透明材质：`None` 材质、per-widget override、可读性 Auto 采样 | 右键菜单可切三种材质 | 1.5d |
| P2 | 天气行：缓存读取、图标、降级、点击行为 | 天气行可用（不含设置页） | 1d |
| P3 | 多时区：目录 + 精选城市表 + 行渲染 + 日差/昼夜 + 分钟刷新 | 时区条可用（不含编辑 UI） | 2.5d |
| P4 | 尺寸档位自适应 + 三段式排版打磨 | S/M/L 三档观感达标 | 1.5d |
| P5 | 设置页 + 右键菜单 + 本地化 + 迁移 v11 | 全链路可配置 | 1.5d |
| P6 | 测试与验收（含 AOT 冒烟、真机多壁纸验收） | 测试清单全绿 | 1.5d |

合计约 10 人日（含返工余量）。

### 验收清单

- [ ] 透明模式下，浅色/深色/高对比/照片壁纸各一张，文字均可读（含中午逆光壁纸）
- [ ] 透明模式不影响其他组件（文件、天气、待办仍是原材质）
- [ ] 时区行在夏令时切换前后显示正确（用 `America/New_York` 手动改系统时间验证）
- [ ] 断网 24 小时后天气行仍显示缓存值，且不显示 `--°`
- [ ] 12/24 小时制切换后，主时间与时区行同步变化
- [ ] 5 个时区 + L 档下，组件缩到 S 档不出现裁剪/重叠（逐像素检查）
- [ ] `glance.json` 从 v10 升级后，旧用户视觉零变化
- [ ] AOT 发布版功能与 Debug 一致（白名单漏项会在这里暴露）
- [ ] `dotnet test .\tests\DeskBox.Tests\DeskBox.Tests.csproj -p:Platform=x64` 通过数 = 总数

### 测试计划

| 类型 | 覆盖 |
|---|---|
| 单元测试 | 时区行组装（日差、昼夜、12/24）、天气行文案（四舍五入、单位、降级）、`Normalize`（无效 Id、去重、截断）、材质 override 解析 |
| 迁移测试 | v10 → v11 增量反序列化、缺字段默认值、损坏 Id 丢弃 |
| 冒烟 | `App.AotGlancePersistenceSmoke` 扩展新字段往返；新增 AOT 绑定白名单断言 |
| 手动 | 多壁纸可读性、多显示器、缩放 100%/150%、深浅主题切换 |

---

## 6. 风险与取舍

| 风险 | 影响 | 应对 |
|---|---|---|
| 材质是全局架构，override 要穿透 3 条背景路径（Win11 controller / Win10 accent / Solid） | 透明只在 Win11 正确、Win10 残留模糊 | 三条路径都读 override；Win10 走 `DisableAccentPolicy` + 透明表面；纳入 Win10 验收 |
| 全透明窗口的拖拽手感 | 用户不知道哪里能拖 | 整窗保持可命中；hover 时显示极淡轮廓（1px，8% 白）；保留现有缩放边 |
| 每分钟 N 个时区 × M 个实例的 CPU | 多实例时轻微耗电 | 只在分钟边界算一次，结果缓存到下一次边界；无秒显示时不启用秒级 timer |
| 天气 API 频率 | 被限流/隐私争议 | 只读共享缓存，30 分钟 TTL，退避策略沿用现有 |
| 时区名本地化 | 中文系统显示英文时区名 | 精选城市表兜底 + 用户可改名 |
| AOT 白名单遗漏 | 发布版静默不显示 | P6 专项 AOT 冒烟；绑定属性集中登记在一个文件 |

**刻意不做**（避免范围蔓延）：

- 不做模拟表盘（指针时钟）——与"手机透明时钟"的诉求无关，且动画成本高。
- 不做秒显示默认开启（可选，默认关）。
- 不在时钟里放第二套天气详情（逐时/7 天）。
- 不做"每个时区单独选图标/颜色"——信息密度已经够了。
- 不改全局材质设置语义，不动其他组件的默认外观。

---

## 7. 待决问题（已定稿 2026-10-23 20:00）

| # | 问题 | 决定 |
|---|---|---|
| 1 | 透明是"每组件"还是"全局" | **仅时光（时钟）组件**。文件、天气、待办等其他组件继续跟随全局材质；实现走 `IWidgetSurfaceAppearanceSource` 逐窗口 override |
| 2 | 天气行默认开还是关 | **默认开启**（`ShowWeather` 默认 `true`），首次显示即带天气摘要 |
| 3 | 时区默认值 | **默认本机时区**（系统时间），其余时区由用户自行添加；不预填猜测城市 |
| 4 | 文字衬底 | **按本方案执行**：Auto 对比度不足才加，衬底 8%–14%，用户可关 |

### 7.1 实施进度

| 阶段 | 状态 | 落点 |
|---|---|---|
| P1 透明材质 | **已实现，待真机验收** | `WidgetMaterialKinds.None`、`WidgetWindowBase` 材质/透明度 override 与 `SuppressSurfaceChrome`、`DesktopWallpaperContrastProbe`（壁纸亮度采样 + WCAG 对比度判定）、`GlanceSurfaceMaterialMode`/`GlanceTextContrastMode`、数据 v11、右键菜单「背景材质」、设置页「背景与透明」分组、zh/en 文案 |
| P2 天气行 | **已实现，逻辑已验证** | `GlanceWeatherSummaryService`（共用 `WeatherService` 缓存；手动城市 → 缓存定位 → Windows 定位）、`ShowWeather`（默认开）、按宽度三段压缩的温度/天气/温差、`GlanceDisplayElement.Weather` 贯通策略·右键·设置下拉、AOT 白名单补齐 `ShowWeatherRow`/`WeatherIcon`/`WeatherText` |
| P3 多时区 | 未开始 | — |
| P4–P6 | 未开始 | — |

### 7.2 P1 实施中发现的两点现实约束

1. **"透明"不等于"没有背景图"**：`GlanceBackgroundSource` 没有 None 分支，组件永远有一张背景图（Bing/在线/本地）。**已决定并实现**：透明材质下 `BackgroundImageOpacity` 直接归零（照片、可读性遮罩、底部渐变、照片控件一起让位），用户不必再手动把背景图透明度拉到 100%；透明材质因此读作"文字浮在壁纸上"，若要保留照片卡效果请用「玻璃」或「跟随系统」。玻璃/跟随系统两条路径的语义未变。
2. **窗口层 override 必须覆盖三条背景路径**（Win11 controller / Win10 accent / Solid），已按此实现；Win10 需真机验收确认无残留模糊。
3. **"没有 backdrop" ≠ "透明"**（真机反馈"时间组件显示黑色背景"的根因）：第一版把 `SystemBackdrop` 置空、拆掉 controller，以为桌面就会透出来。实际 WinUI 3 在**没有任何 backdrop target** 时会用主题的窗口填充色铺底（深色主题即纯黑），XAML 各层再透明也盖不住它。修正：透明材质改为挂一个 **零 alpha 的 `WinUIEx.TransparentTintBackdrop`**（与 Solid 材质复用同一个实例，只改 `TintColor`），并在 `CanReuseAppliedBackdrop` 的 None 分支改为要求「tint backdrop 已挂上」。这是 Win10/Win11 通用的正确姿势：composition brush 的 alpha 才是决定窗口是否透出桌面的那一层。

### 7.3 P2 逻辑验收记录（Win10 19045，Debug 构建）

| 检查项 | 证据 |
|---|---|
| 透明材质真的落到窗口上 | `[Backdrop.Win10] requested=None resolved=None controllerApplied=True tintBackdropActive=True opacity=0.000`（`tintBackdropActive=True` 是黑底修复后新增的判据，为 False 就说明零 alpha brush 没挂上） |
| 其他组件不受影响 | 同一次启动里天气/文件组件仍为 `requested=Acrylic resolved=Acrylic tintBackdropActive=False opacity=0.800` |
| 天气摘要真的取到数据 | `[GlanceWeather] Summary '15° 晴间多云' range='13°~22°' city='洛杉矶' stale=False`（走 `WeatherCache`，ageMinutes 16.1，未额外发网络请求） |
| 定位降级链 | 手动城市 → 缓存定位 → `WindowsLocationHelper`；三者皆无时只打一行 `[GlanceWeather] No location available; weather row hidden`，不显示猜测温度 |
| 编译 | `dotnet build src/DeskBox/DeskBox.csproj --no-restore` → 0 error（9 warning，全部来自并行分支的 QuickPanel MVVMTK0045） |

视觉验收仍需人眼：天气行的字号（13px）与既有的日期行（14px）一致，在大尺寸格子（实测 684×339）里是"第二行小字"，不会跟随时钟字号放大。
