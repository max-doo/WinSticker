namespace WinSticker.Tests;

public sealed class SettingsAboutContactTests
{
    [Fact]
    public void AboutSection_KeepsAppInformationAndUpdatesWithoutUpstreamContactEntries()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src/WinSticker/Views/SettingsWindow.xaml"));
        var document = System.Xml.Linq.XDocument.Parse(xaml);
        Assert.Single(document.Descendants().Where(element =>
            element.Name.LocalName == "TextBlock" &&
            element.Attribute("Text")?.Value == "{Binding AppVersion}"));
        Assert.Contains("Settings.About.Description", xaml, StringComparison.Ordinal);
        Assert.Contains("Settings.Update.Title", xaml, StringComparison.Ordinal);
        foreach (string removedEntry in new[]
        {
            "ShowAboutMeButton_Click", "OpenWebsiteButton_Click",
            "ShowFeedbackDialogButton_Click", "ShowMyFeedbackButton_Click",
            "ShowStoreSupportDialogButton_Click", "OpenMicrosoftStoreButton_Click",
            "AboutMeDialog", "SupportWinStickerDialog",
            "wechat-qrcode.jpg", "support-wechat.png", "support-alipay.png"
        })
        {
            Assert.DoesNotContain(removedEntry, xaml, StringComparison.Ordinal);
        }
        string catalog = File.ReadAllText(Path.Combine(root, "src/WinSticker/Services/SettingsSearchCatalog.cs"));
        Assert.DoesNotContain("Settings.About.FeedbackTitle", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("Settings.About.StoreSupportTitle", catalog, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "src",
                    "WinSticker",
                    "WinSticker.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "WinSticker repository root was not found.");
    }
}
