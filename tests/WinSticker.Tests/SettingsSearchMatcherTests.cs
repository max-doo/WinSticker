using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class SettingsSearchMatcherTests
{
    [Fact]
    public void ExactTitleRanksAheadOfOtherMatches()
    {
        int exact = SettingsSearchMatcher.GetScore("透明度", "透明度", "外观 / 窗口材质", "调整背景透明程度");
        int prefix = SettingsSearchMatcher.GetScore("透明", "透明度", "外观 / 窗口材质", "调整背景透明程度");
        int breadcrumb = SettingsSearchMatcher.GetScore("外观", "透明度", "外观 / 窗口材质", "调整背景透明程度");

        Assert.True(exact < prefix);
        Assert.True(prefix < breadcrumb);
    }

    [Fact]
    public void MultipleTermsCanMatchAcrossTitleAndBreadcrumb()
    {
        int score = SettingsSearchMatcher.GetScore(
            "待办 行数",
            "内容展示行数",
            "功能贴纸 / 待办",
            "控制每条待办内容显示的行数");

        Assert.NotEqual(SettingsSearchMatcher.NoMatch, score);
    }

    [Fact]
    public void DescriptionProvidesFallbackSearchText()
    {
        int score = SettingsSearchMatcher.GetScore(
            "Ctrl+Enter",
            "回车键行为",
            "功能贴纸 / 随记",
            "使用 Ctrl+Enter 保存当前内容");

        Assert.NotEqual(SettingsSearchMatcher.NoMatch, score);
    }

    [Fact]
    public void MissingTermDoesNotMatch()
    {
        int score = SettingsSearchMatcher.GetScore(
            "天气 透明度",
            "刷新间隔",
            "功能贴纸 / 天气",
            "控制天气数据的刷新频率");

        Assert.Equal(SettingsSearchMatcher.NoMatch, score);
    }
}
