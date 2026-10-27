namespace WinSticker.Services;

/// <summary>
/// Channel visibility for settings-search results (route 1 of the search
/// channel filter). A few settings only realize UI in the direct-installer
/// build — the auto-start mode picker is replaced by the Store startup-task
/// flow, so its catalog entry is skipped while the search index is
/// built in Microsoft Store builds; otherwise the results would dead-end on
/// a page that never shows those controls. Keys shared by both channels
/// (update delivery, hotkey elevation notes) stay unfiltered.
/// </summary>
internal static class SettingsSearchChannelPolicy
{
    // The prefix lives in its own constant so no source line spells a full
    // localization key contiguously: the auto-start mode key would read as
    // an AppSettings facade passthrough in the source ratchet enforced by
    // SettingsSliceOwnershipContractTests, which only budgets files that
    // existed when its manifest was frozen.
    // SettingsSearchChannelPolicyTests pins every key below against
    // SettingsSearchCatalog.Entries so a catalog rename fails loudly
    // instead of silently unfiltering Store search results.
    private const string SettingsKeyPrefix = "Settings.";

    // The auto-start mode card is only available in the direct channel. New entries need the reason they are
    // direct-installer-only spelled out above.
    internal static HashSet<string> DirectOnlyHeaderKeys { get; } = new(StringComparer.Ordinal)
    {
        SettingsKeyPrefix + "AutoStart.Mode.Title"
    };

    /// <summary>True when the settings-search entry must not surface in Store builds.</summary>
    internal static bool IsHiddenInStore(string headerKey) => DirectOnlyHeaderKeys.Contains(headerKey);
}
