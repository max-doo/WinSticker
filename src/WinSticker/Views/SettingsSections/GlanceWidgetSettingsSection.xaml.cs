using System.Collections.ObjectModel;
using System.Globalization;
using WinSticker.Controls;
using WinSticker.Models;
using WinSticker.Services;
using WinSticker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace WinSticker.Views.SettingsSections;

public sealed partial class GlanceWidgetSettingsSection : UserControl
{
    private static readonly string[] DisplayOptions = ["Time", "Date", "Year", "Weekday", "Weather"];
    private sealed partial class SelectionItem : ComboBoxItem
    {
        public SelectionItem(string label, object value)
        {
            Content = label;
            Value = value;
        }

        public object Value { get; }
    }
    private GlanceWidgetStore? _store;
    private readonly SystemFontCatalogService _fontCatalogService = new();
    private readonly DispatcherTimer _scaleSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(280) };
    private readonly DispatcherTimer _sizeScaleSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(280) };
    private readonly DispatcherTimer _surfaceOpacitySaveTimer = new() { Interval = TimeSpan.FromMilliseconds(280) };
    private readonly SemaphoreSlim _instanceSelectionGate = new(1, 1);
    private GlanceWidgetData _settings = new();
    private readonly ObservableCollection<GlanceWorldClockRow> _worldClockRows = new();
    private string? _selectedWidgetId;
    private IntPtr _ownerWindow;
    private bool _isLoading;
    private bool _isSectionLoaded;
    private bool _instanceRefreshQueued;
    private long _instanceSelectionVersion;
    private string _instanceStateSignature = string.Empty;
    private string _optionsLocalizationSignature = string.Empty;

    public GlanceWidgetSettingsSection()
    {
        WorldClockRows = _worldClockRows;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _scaleSaveTimer.Tick += ScaleSaveTimer_Tick;
        _sizeScaleSaveTimer.Tick += SizeScaleSaveTimer_Tick;
        _surfaceOpacitySaveTimer.Tick += SurfaceOpacitySaveTimer_Tick;
        WidgetDangerActionStyle.Apply(DeleteInstanceMenuItem);
    }

    private LocalizationService Localization => App.Current.LocalizationService;

    /// <summary>
    /// The world-clock list binds to these rows with x:Bind (AOT-safe typed
    /// DataTemplate); the rows double as the widget's ViewModel rows so the
    /// settings list and the patch always agree on ordering.
    /// </summary>
    public ObservableCollection<GlanceWorldClockRow> WorldClockRows { get; }

    public void SetOwnerWindow(IntPtr ownerWindow) => _ownerWindow = ownerWindow;

    public void SelectWidget(string widgetId)
    {
        if (!string.IsNullOrWhiteSpace(widgetId))
        {
            _selectedWidgetId = widgetId;
        }
    }

    public async Task RefreshFromStoreAsync()
    {
        await RefreshInstancesAsync(_selectedWidgetId);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isSectionLoaded = true;
        App.Current.SettingsService.SettingsChanged -= OnAppSettingsChanged;
        App.Current.SettingsService.SettingsChanged += OnAppSettingsChanged;
        await RefreshFromStoreAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isSectionLoaded = false;
        App.Current.SettingsService.SettingsChanged -= OnAppSettingsChanged;
    }

    private void OnAppSettingsChanged()
    {
        if (!_isSectionLoaded ||
            _instanceRefreshQueued)
        {
            return;
        }

        _instanceRefreshQueued = true;
        if (!DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    if (!_isSectionLoaded ||
                        App.Current.WidgetManager is not { } manager)
                    {
                        return;
                    }

                    IReadOnlyList<GlanceWidgetInstanceInfo> instances =
                        manager.GetGlanceWidgetInstances();
                    if (!string.Equals(
                            _instanceStateSignature,
                            CreateInstanceStateSignature(manager, instances),
                            StringComparison.Ordinal))
                    {
                        await RefreshInstancesAsync(_selectedWidgetId);
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"[GlanceSettings] Instance state refresh failed: {ex}");
                }
                finally
                {
                    _instanceRefreshQueued = false;
                }
            }))
        {
            _instanceRefreshQueued = false;
        }
    }

    private async Task RefreshInstancesAsync(string? preferredWidgetId)
    {
        await _instanceSelectionGate.WaitAsync();
        try
        {
            await RefreshInstancesCoreAsync(preferredWidgetId);
        }
        finally
        {
            _instanceSelectionGate.Release();
        }
    }

    private async Task RefreshInstancesCoreAsync(string? preferredWidgetId)
    {
        UpdateInstanceLocalization();
        if (App.Current.WidgetManager is not { } manager)
        {
            ApplyEmptyInstanceState();
            return;
        }

        IReadOnlyList<GlanceWidgetInstanceInfo> instances = manager.GetGlanceWidgetInstances();
        _instanceStateSignature = CreateInstanceStateSignature(manager, instances);
        GlanceWidgetInstanceInfo? selected = instances.FirstOrDefault(instance =>
            string.Equals(instance.Id, preferredWidgetId, StringComparison.Ordinal)) ??
            instances.FirstOrDefault();

        _isLoading = true;
        try
        {
            _selectedWidgetId = selected?.Id;
            InstanceComboBox.SelectedItem = SynchronizeInstanceOptions(
                instances,
                selected?.Id);
            ApplyInstanceManagerState(manager, instances, selected);
            EnsureOptionsPopulated();

            if (selected is null)
            {
                _store = null;
                _settings = new GlanceWidgetData();
                return;
            }

            _store = GlanceWidgetStore.ForWidget(selected!.Id);
            _settings = await _store.LoadAsync();
            ApplySettingsToControls();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static string CreateInstanceStateSignature(
        WidgetManager manager,
        IEnumerable<GlanceWidgetInstanceInfo> instances)
    {
        return $"{manager.IsGlanceFeatureEnabled}:" + string.Join(
            "|",
            instances.Select(instance =>
                $"{instance.Id.Length}:{instance.Id}" +
                $"{instance.Name.Length}:{instance.Name}" +
                $"{instance.IsEnabled}"));
    }

    private void ApplyEmptyInstanceState()
    {
        _isLoading = true;
        try
        {
            _selectedWidgetId = null;
            _instanceStateSignature = string.Empty;
            _store = null;
            _settings = new GlanceWidgetData();
            InstanceComboBox.Items.Clear();
            InstanceSettingsPanel.Visibility = Visibility.Collapsed;
            InstanceEnabledToggle.IsOn = false;
            InstanceEnabledToggle.IsEnabled = false;
            InstanceMoreButton.IsEnabled = false;
            LocateInstanceMenuItem.IsEnabled = false;
            DuplicateInstanceMenuItem.IsEnabled = false;
            RenameInstanceMenuItem.IsEnabled = false;
            DeleteInstanceMenuItem.IsEnabled = false;
            InstanceManagerCard.Description = Localization.Format(
                "Glance.Instances.Description",
                0);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void UpdateInstanceLocalization()
    {
        InstanceComboBox.PlaceholderText = Localization.T("Glance.Instances.Empty");
        LocateInstanceMenuItem.Text = Localization.T("Glance.Instances.Locate");
        DuplicateInstanceMenuItem.Text = Localization.T("Glance.Instances.Duplicate");
        RenameInstanceMenuItem.Text = Localization.T("Common.Rename");
        DeleteInstanceMenuItem.Text = Localization.T("Common.Delete");
        WorldClockNameBox.PlaceholderText = Localization.T("Glance.WorldClock.NamePlaceholder");
        WorldClockZoneComboBox.PlaceholderText = Localization.T("Glance.WorldClock.ZonePlaceholder");
    }

    private void PopulateOptions()
    {
        (string Label, object Value)[] fonts =
        [
            (Localization.T("Glance.Typography.SystemDefault"), string.Empty),
            .. _fontCatalogService.GetFontFamilies().Select(name => (name, (object)name))
        ];
        SetOptions(FontComboBox, fonts);
        SetOptions(
            TimeFormatComboBox,
            (Localization.T("Glance.Typography.FollowSystem"), GlanceTimeFormatMode.FollowSystem),
            (Localization.T("Glance.Typography.Hour24"), GlanceTimeFormatMode.Hour24),
            (Localization.T("Glance.Typography.Hour12"), GlanceTimeFormatMode.Hour12));
        SetOptions(
            SurfaceMaterialComboBox,
            (Localization.T("Glance.Surface.FollowSystem"), GlanceSurfaceMaterialMode.FollowSystem),
            (Localization.T("Glance.Surface.Transparent"), GlanceSurfaceMaterialMode.Transparent),
            (Localization.T("Glance.Surface.Glass"), GlanceSurfaceMaterialMode.Glass));
        SetOptions(
            TextContrastComboBox,
            (Localization.T("Glance.Contrast.Auto"), GlanceTextContrastMode.Auto),
            (Localization.T("Glance.Contrast.White"), GlanceTextContrastMode.White),
            (Localization.T("Glance.Contrast.Black"), GlanceTextContrastMode.Black),
            (Localization.T("Glance.Contrast.Off"), GlanceTextContrastMode.Off));
        PopulateWorldClockZoneOptions();
    }

    /// <summary>
    /// System zone list, offset-ordered like Windows' own clock settings. The
    /// display name falls back to the zone id; users rename cities when adding.
    /// </summary>
    private void PopulateWorldClockZoneOptions()
    {
        // Highlight the system zone the user is actually in; same mirroring as
        // Windows' own clock settings so the default needs no searching.
        string localZoneId = TimeZoneInfo.Local.Id;
        SelectionItem? previouslySelected = WorldClockZoneComboBox.SelectedItem as SelectionItem;
        WorldClockZoneComboBox.Items.Clear();
        foreach (TimeZoneInfo zone in TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(zone => zone.BaseUtcOffset)
            .ThenBy(zone => zone.Id, StringComparer.OrdinalIgnoreCase))
        {
            WorldClockZoneComboBox.Items.Add(new SelectionItem(
                zone.DisplayName,
                zone.Id));
        }

        WorldClockZoneComboBox.SelectedItem =
            (previouslySelected is not null
                ? WorldClockZoneComboBox.Items
                    .OfType<SelectionItem>()
                    .FirstOrDefault(option => Equals(option.Value, previouslySelected.Value))
                : null)
            ?? WorldClockZoneComboBox.Items
                .OfType<SelectionItem>()
                .FirstOrDefault(option => Equals(option.Value, localZoneId));
    }

    private void EnsureOptionsPopulated()
    {
        string localizationSignature =
            $"{Localization.CurrentCultureName}|{CultureInfo.CurrentCulture.Name}";
        bool optionsArePopulated =
            FontComboBox.Items.Count > 0 &&
            TimeFormatComboBox.Items.Count > 0 &&
            SurfaceMaterialComboBox.Items.Count > 0 &&
            TextContrastComboBox.Items.Count > 0 &&
            WorldClockZoneComboBox.Items.Count > 0;
        if (optionsArePopulated &&
            string.Equals(
                _optionsLocalizationSignature,
                localizationSignature,
                StringComparison.Ordinal))
        {
            return;
        }

        PopulateOptions();
        _optionsLocalizationSignature = localizationSignature;
    }

    private static void SetOptions(
        ComboBox comboBox,
        params (string Label, object Value)[] options)
    {
        comboBox.Items.Clear();
        foreach ((string label, object value) in options)
        {
            comboBox.Items.Add(new SelectionItem(label, value));
        }
    }

    private async Task LoadSelectedInstanceAsync(
        string selectedWidgetId,
        long selectionVersion)
    {
        if (App.Current.WidgetManager is not { } manager)
        {
            await RefreshInstancesCoreAsync(null);
            return;
        }

        IReadOnlyList<GlanceWidgetInstanceInfo> instances = manager.GetGlanceWidgetInstances();
        GlanceWidgetInstanceInfo? selected = instances.FirstOrDefault(instance =>
            string.Equals(instance.Id, selectedWidgetId, StringComparison.Ordinal));
        if (selected is null)
        {
            await RefreshInstancesCoreAsync(null);
            return;
        }

        GlanceWidgetStore store = GlanceWidgetStore.ForWidget(selected.Id);
        GlanceWidgetData settings = await store.LoadAsync();
        if (selectionVersion != _instanceSelectionVersion ||
            InstanceComboBox.SelectedItem is not SelectionItem { Value: string currentWidgetId } ||
            !string.Equals(currentWidgetId, selectedWidgetId, StringComparison.Ordinal))
        {
            return;
        }

        _isLoading = true;
        try
        {
            _instanceStateSignature = CreateInstanceStateSignature(manager, instances);
            _selectedWidgetId = selected.Id;
            ApplyInstanceManagerState(manager, instances, selected);
            EnsureOptionsPopulated();
            _store = store;
            _settings = settings;
            ApplySettingsToControls();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyInstanceManagerState(
        WidgetManager manager,
        IReadOnlyCollection<GlanceWidgetInstanceInfo> instances,
        GlanceWidgetInstanceInfo? selected)
    {
        bool hasSelection = selected is not null;
        InstanceSettingsPanel.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        InstanceMoreButton.IsEnabled = hasSelection;
        LocateInstanceMenuItem.IsEnabled = selected?.IsEnabled == true;
        DuplicateInstanceMenuItem.IsEnabled = hasSelection;
        RenameInstanceMenuItem.IsEnabled = hasSelection;
        DeleteInstanceMenuItem.IsEnabled = hasSelection;
        InstanceEnabledToggle.IsEnabled = hasSelection && manager.IsGlanceFeatureEnabled;
        InstanceEnabledToggle.IsOn = selected?.IsEnabled == true;
        InstanceManagerCard.Description = Localization.Format(
            "Glance.Instances.Description",
            instances.Count);
        if (!manager.IsGlanceFeatureEnabled)
        {
            InstanceManagerCard.Description =
                $"{InstanceManagerCard.Description} {Localization.T("Glance.Instances.MasterOff")}";
        }
    }

    private SelectionItem? SynchronizeInstanceOptions(
        IReadOnlyList<GlanceWidgetInstanceInfo> instances,
        string? selectedWidgetId)
    {
        SelectionItem[] options = InstanceComboBox.Items
            .OfType<SelectionItem>()
            .ToArray();
        bool optionsMatch = options.Length == instances.Count &&
            options.Select((option, index) =>
                    option.Value is string id &&
                    string.Equals(id, instances[index].Id, StringComparison.Ordinal) &&
                    string.Equals(option.Content as string, instances[index].Name, StringComparison.Ordinal))
                .All(matches => matches);
        if (!optionsMatch)
        {
            InstanceComboBox.Items.Clear();
            foreach (GlanceWidgetInstanceInfo instance in instances)
            {
                InstanceComboBox.Items.Add(new SelectionItem(instance.Name, instance.Id));
            }

            options = InstanceComboBox.Items
                .OfType<SelectionItem>()
                .ToArray();
        }

        return options.FirstOrDefault(option =>
            option.Value is string id &&
            string.Equals(id, selectedWidgetId, StringComparison.Ordinal));
    }

    private void ApplySettingsToControls()
    {
        bool wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            SelectOption(FontComboBox, _settings.TimeFontFamily ?? string.Empty);
            SelectOption(TimeFormatComboBox, _settings.TimeFormat);
            SelectOption(SurfaceMaterialComboBox, _settings.SurfaceMaterial);
            SelectOption(TextContrastComboBox, _settings.TextContrast);
            ShowSecondsToggle.IsOn = _settings.ShowSeconds;
            TimeScaleSlider.Value = _settings.TimeScale;
            SizeScaleSlider.Value = _settings.SizeScale;
            SurfaceOpacitySlider.Value = _settings.SurfaceMaterialOpacity;
            TextPlateToggle.IsOn = _settings.ShowTextPlate;
            UpdateDisplaySelectionSummary();
            UpdateWorldClockList();
            UpdateSurfaceMaterialState();
        }
        finally
        {
            _isLoading = wasLoading;
        }
    }

    private static void SelectOption(ComboBox comboBox, object value)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<SelectionItem>()
            .FirstOrDefault(option => Equals(option.Value, value));
    }

    private async Task SaveAsync(Action<GlanceWidgetData> update)
    {
        GlanceWidgetStore? store = _store;
        if (_isLoading || store is null)
        {
            return;
        }

        update(_settings);
        await store.SaveAsync(_settings);
    }

    private async Task FlushPendingSavesAsync()
    {
        bool hasPendingSave = _scaleSaveTimer.IsEnabled ||
            _sizeScaleSaveTimer.IsEnabled ||
            _surfaceOpacitySaveTimer.IsEnabled;
        _scaleSaveTimer.Stop();
        _sizeScaleSaveTimer.Stop();
        _surfaceOpacitySaveTimer.Stop();
        if (hasPendingSave && _store is { } store)
        {
            await store.SaveAsync(_settings);
        }
    }

    private async void InstanceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InstanceComboBox.SelectedItem is not SelectionItem { Value: string selectedWidgetId })
        {
            return;
        }

        long selectionVersion = ++_instanceSelectionVersion;
        if (string.Equals(_selectedWidgetId, selectedWidgetId, StringComparison.Ordinal))
        {
            return;
        }

        await _instanceSelectionGate.WaitAsync();
        try
        {
            if (selectionVersion != _instanceSelectionVersion)
            {
                return;
            }

            await FlushPendingSavesAsync();
            if (selectionVersion != _instanceSelectionVersion)
            {
                return;
            }

            await LoadSelectedInstanceAsync(selectedWidgetId, selectionVersion);
        }
        catch (Exception ex)
        {
            App.Log($"[GlanceSettings] Instance selection failed: {ex}");
        }
        finally
        {
            _instanceSelectionGate.Release();
        }
    }

    private async void InstanceEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading ||
            string.IsNullOrWhiteSpace(_selectedWidgetId) ||
            App.Current.WidgetManager is not { } manager)
        {
            return;
        }

        await manager.SetGlanceWidgetInstanceEnabledAsync(
            _selectedWidgetId,
            InstanceEnabledToggle.IsOn);
        await RefreshInstancesAsync(_selectedWidgetId);
    }

    private async void AddInstanceButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.Current.WidgetManager is not { } manager)
        {
            return;
        }

        await FlushPendingSavesAsync();
        GlanceWidgetInstanceInfo created = await manager.CreateGlanceWidgetAsync();
        await RefreshInstancesAsync(created.Id);
    }

    private async void LocateInstanceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_selectedWidgetId) &&
            App.Current.WidgetManager is { } manager)
        {
            await manager.LocateGlanceWidgetAsync(_selectedWidgetId);
        }
    }

    private async void DuplicateInstanceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedWidgetId) ||
            App.Current.WidgetManager is not { } manager)
        {
            return;
        }

        await FlushPendingSavesAsync();
        GlanceWidgetInstanceInfo created = await manager.CreateGlanceWidgetAsync(_selectedWidgetId);
        await RefreshInstancesAsync(created.Id);
    }

    private async void RenameInstanceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedWidgetId) ||
            App.Current.WidgetManager is not { } manager ||
            XamlRoot is null)
        {
            return;
        }

        GlanceWidgetInstanceInfo? selected = manager.GetGlanceWidgetInstances()
            .FirstOrDefault(instance =>
                string.Equals(instance.Id, _selectedWidgetId, StringComparison.Ordinal));
        if (selected is null)
        {
            return;
        }

        var nameBox = new TextBox
        {
            Text = selected.Name,
            MinWidth = 320,
            MaxLength = 80,
            PlaceholderText = Localization.T("Glance.Instances.RenamePlaceholder")
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Localization.T("Glance.Instances.RenameTitle"),
            Content = nameBox,
            PrimaryButtonText = Localization.T("Common.Save"),
            CloseButtonText = Localization.T("Common.Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary ||
            string.IsNullOrWhiteSpace(nameBox.Text))
        {
            return;
        }

        await manager.RenameWidgetAsync(selected.Id, nameBox.Text);
        await RefreshInstancesAsync(selected.Id);
    }

    private async void DeleteInstanceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedWidgetId) ||
            App.Current.WidgetManager is not { } manager ||
            XamlRoot is null)
        {
            return;
        }

        GlanceWidgetInstanceInfo? selected = manager.GetGlanceWidgetInstances()
            .FirstOrDefault(instance =>
                string.Equals(instance.Id, _selectedWidgetId, StringComparison.Ordinal));
        if (selected is null)
        {
            return;
        }

        if (!await ConfirmDialog.ShowAsync(
                XamlRoot,
                Localization,
                Localization.Format("Glance.Instances.DeleteTitle", selected.Name),
                Localization.T("Glance.Instances.DeleteDescription"),
                Localization.T("Common.Delete")))
        {
            return;
        }

        await FlushPendingSavesAsync();
        await manager.RemoveGlanceWidgetAsync(selected.Id);
        _selectedWidgetId = null;
        await RefreshInstancesAsync(null);
    }

    private void DisplayContentDropDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not DropDownButton button)
        {
            return;
        }

        SettingsMultiSelectMenu.Show(
            button,
            DisplayOptions,
            GetDisplayOptionLabel,
            IsDisplayOptionSelected,
            _ => true,
            ToggleDisplayOption);
    }

    private async void ToggleDisplayOption(string option)
    {
        GlanceDisplayElement? element = option switch
        {
            "Time" => GlanceDisplayElement.Time,
            "Date" => GlanceDisplayElement.Date,
            "Year" => GlanceDisplayElement.Year,
            "Weekday" => GlanceDisplayElement.Weekday,
            "Weather" => GlanceDisplayElement.Weather,
            _ => null
        };
        if (element is null)
        {
            return;
        }

        bool isVisible = GlanceWidgetSettingsPolicy.IsDisplayElementVisible(
            _settings,
            element.Value);
        GlanceWidgetSettingsPolicy.SetDisplayElement(
            _settings,
            element.Value,
            !isVisible);

        UpdateDisplaySelectionSummary();
        if (_store is { } store)
        {
            await store.SaveAsync(_settings);
        }
    }

    private async void ShowSecondsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoading)
        {
            await SaveAsync(settings => settings.ShowSeconds = ShowSecondsToggle.IsOn);
        }
    }

    private async void FontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontComboBox.SelectedItem is SelectionItem { Value: string font })
        {
            await SaveAsync(settings => settings.TimeFontFamily = string.IsNullOrWhiteSpace(font) ? null : font);
        }
    }

    private async void TimeFormatComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (TimeFormatComboBox.SelectedItem is SelectionItem
            {
                Value: GlanceTimeFormatMode timeFormat
            })
        {
            await SaveAsync(settings => settings.TimeFormat = timeFormat);
        }
    }

    private async void SurfaceMaterialComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (SurfaceMaterialComboBox.SelectedItem is SelectionItem
            {
                Value: GlanceSurfaceMaterialMode mode
            })
        {
            await SaveAsync(settings =>
                GlanceWidgetSettingsPolicy.SetSurfaceMaterial(settings, mode));
            UpdateSurfaceMaterialState();
        }
    }

    private void SurfaceOpacitySlider_ValueChanged(
        object sender,
        RangeBaseValueChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.SurfaceMaterialOpacity = Math.Clamp(e.NewValue, 0.05, 1.0);
        UpdateSurfaceMaterialState();
        _surfaceOpacitySaveTimer.Stop();
        _surfaceOpacitySaveTimer.Start();
    }

    private async void SurfaceOpacitySaveTimer_Tick(object? sender, object e)
    {
        _surfaceOpacitySaveTimer.Stop();
        if (_store is { } store)
        {
            await store.SaveAsync(_settings);
        }
    }

    private async void TextContrastComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (TextContrastComboBox.SelectedItem is SelectionItem
            {
                Value: GlanceTextContrastMode mode
            })
        {
            await SaveAsync(settings =>
                GlanceWidgetSettingsPolicy.SetTextContrast(settings, mode));
        }
    }

    private async void TextPlateToggle_Toggled(object sender, RoutedEventArgs e)
        => await SaveAsync(settings => settings.ShowTextPlate = TextPlateToggle.IsOn);

    private void TimeScaleSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.TimeScale = e.NewValue;
        _scaleSaveTimer.Stop();
        _scaleSaveTimer.Start();
    }

    private async void ScaleSaveTimer_Tick(object? sender, object e)
    {
        _scaleSaveTimer.Stop();
        if (_store is { } store)
        {
            await store.SaveAsync(_settings);
        }
    }

    private void SizeScaleSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        GlanceWorldClockPolicy.SetSizeScale(_settings, e.NewValue);
        SizeScaleValue.Text = $"{Math.Round(_settings.SizeScale * 100):0}%";
        _sizeScaleSaveTimer.Stop();
        _sizeScaleSaveTimer.Start();
    }

    private async void SizeScaleSaveTimer_Tick(object? sender, object e)
    {
        _sizeScaleSaveTimer.Stop();
        if (_store is { } store)
        {
            await store.SaveAsync(_settings);
        }
    }

    // ─── World clocks ────────────────────────────────────────────

    private async void AddWorldClockButton_Click(object sender, RoutedEventArgs e)
    {
        if (WorldClockZoneComboBox.SelectedItem is not SelectionItem { Value: string zoneId } ||
            _settings.WorldClocks.Count >= GlanceWorldClockPolicy.MaximumWorldClockCount ||
            _settings.WorldClocks.Any(clock => string.Equals(
                clock.TimeZoneId,
                zoneId,
                StringComparison.OrdinalIgnoreCase)))
        {
            // Full list or a zone that is already on the patch: adding again
            // would just rewrite the same row.
            return;
        }

        string? cityName = string.IsNullOrWhiteSpace(WorldClockNameBox.Text)
            ? null
            : WorldClockNameBox.Text.Trim();
        GlanceWorldClockPolicy.AddWorldClock(_settings, zoneId, cityName);
        if (_store is { } store)
        {
            await store.SaveAsync(_settings);
        }

        WorldClockNameBox.Text = string.Empty;
        WorldClockNameBox.Focus(FocusState.Programmatic);
        UpdateWorldClockList();
    }

    private async void RemoveWorldClockButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string zoneId })
        {
            GlanceWorldClockPolicy.RemoveWorldClock(_settings, zoneId);
            if (_store is { } store)
            {
                await store.SaveAsync(_settings);
            }

            UpdateWorldClockList();
        }
    }

    private void UpdateWorldClockList()
    {
        List<GlanceWorldClockData> clocks =
            GlanceWorldClockPolicy.NormalizeWorldClocks(_settings.WorldClocks);
        _worldClockRows.Clear();
        foreach (GlanceWorldClockData clock in clocks)
        {
            _worldClockRows.Add(new GlanceWorldClockRow(clock.TimeZoneId, clock.CityName ?? string.Empty)
            {
                SettingsDisplayName =
                    $"{GlanceWorldClockPolicy.ResolveCityName(clock)}  ·  {clock.TimeZoneId}"
            });
        }

        WorldClockCountText.Text = Localization.Format(
            "Glance.WorldClock.Count",
            _worldClockRows.Count,
            GlanceWorldClockPolicy.MaximumWorldClockCount);
        AddWorldClockButton.IsEnabled = _worldClockRows.Count < GlanceWorldClockPolicy.MaximumWorldClockCount;
    }

    /// <summary>
    /// The opacity slider and the contrast controls only mean something for
    /// their own material; hiding them keeps the section honest about what the
    /// widget is actually drawing.
    /// </summary>
    private void UpdateSurfaceMaterialState()
    {
        bool isGlass = _settings.SurfaceMaterial == GlanceSurfaceMaterialMode.Glass;
        SurfaceOpacityCard.Visibility = isGlass ? Visibility.Visible : Visibility.Collapsed;
        SurfaceOpacityValue.Text = $"{Math.Round(_settings.SurfaceMaterialOpacity * 100):0}%";
    }

    private void UpdateDisplaySelectionSummary()
    {
        var selected = new List<string>(5);
        if (_settings.ShowTime) selected.Add(Localization.T("Glance.Display.Time"));
        if (_settings.ShowDate) selected.Add(Localization.T("Glance.Display.Date"));
        if (_settings.ShowYear) selected.Add(Localization.T("Glance.Display.Year"));
        if (_settings.ShowWeekday) selected.Add(Localization.T("Glance.Display.Weekday"));
        if (_settings.ShowWeather) selected.Add(Localization.T("Glance.Display.Weather"));
        DisplayContentDropDownButton.Content = selected.Count == 0
            ? Localization.T("Glance.Display.None")
            : string.Join(Localization.T("Glance.Display.Separator"), selected);
    }

    private string GetDisplayOptionLabel(string option) => option switch
    {
        "Time" => Localization.T("Glance.Display.Time"),
        "Date" => Localization.T("Glance.Display.Date"),
        "Year" => Localization.T("Glance.Display.Year"),
        "Weekday" => Localization.T("Glance.Display.Weekday"),
        "Weather" => Localization.T("Glance.Display.Weather"),
        _ => option
    };

    private bool IsDisplayOptionSelected(string option) => option switch
    {
        "Time" => _settings.ShowTime,
        "Date" => _settings.ShowDate,
        "Year" => _settings.ShowYear,
        "Weekday" => _settings.ShowWeekday,
        "Weather" => _settings.ShowWeather,
        _ => false
    };
}
