using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using IPSwitch.Core.Localization;

namespace IPSwitch.Core.Tests;

internal static class TestSetup
{
    /// <summary>Die fachlichen Tests prüfen deutsche Meldungstexte.</summary>
    [ModuleInitializer]
    internal static void UseGerman() => Loc.SetLanguage("de");
}

public partial class LocalizationTests
{
    private static readonly string[] Codes = Loc.Languages.Select(l => l.Code).ToArray();

    public static TheoryData<string> Languages => new(Codes);

    [GeneratedRegex(@"\{(\d+)\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex("\"([a-z][a-zA-Z]*(?:\\.[a-zA-Z0-9]+)+)\"")]
    private static partial Regex KeyLiteral();

    [Fact]
    public void AllEightLanguagesAreRegistered()
    {
        Assert.Equal(["de", "en", "tr", "fr", "it", "ar", "zh", "ru"], Codes);
        Assert.True(Loc.Languages.Single(l => l.Code == "ar").RightToLeft);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Language_HasExactlyTheEnglishKeys(string code)
    {
        var english = Loc.Table("en");
        var table = Loc.Table(code);
        Assert.NotEmpty(table);
        Assert.Empty(english.Keys.Except(table.Keys));
        Assert.Empty(table.Keys.Except(english.Keys));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Language_HasSamePlaceholdersAndNoEmptyTexts(string code)
    {
        var english = Loc.Table("en");
        foreach (var (key, text) in Loc.Table(code))
        {
            Assert.False(string.IsNullOrWhiteSpace(text), $"{code}: {key} ist leer");
            var expected = Placeholder().Matches(english[key]).Select(m => m.Value).ToHashSet();
            var actual = Placeholder().Matches(text).Select(m => m.Value).ToHashSet();
            Assert.True(expected.SetEquals(actual), $"{code}: {key} hat Platzhalter {string.Join(",", actual)}, erwartet {string.Join(",", expected)}");
        }
    }

    [Fact]
    public void EveryKeyUsedInSourceExists()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "IPSwitch.sln")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var english = Loc.Table("en");
        var prefixes = english.Keys.Select(k => k[..k.IndexOf('.')]).ToHashSet();
        var missing = Directory.EnumerateFiles(Path.Combine(root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => KeyLiteral().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value))
            .Where(key => !Regex.IsMatch(key, @"\.(exe|json|log|md|ico|xml|dll)$"))
            .Where(key => prefixes.Contains(key[..key.IndexOf('.')]) && !english.ContainsKey(key))
            .Distinct()
            .ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void FallsBackToEnglishAndThenToKey()
    {
        try
        {
            Loc.SetLanguage("en");
            Assert.Equal("Connected", Loc.T("status.up"));
            Assert.Equal("does.not.exist", Loc.T("does.not.exist"));
            Loc.SetLanguage("xx");
            Assert.True(Loc.IsSupported(Loc.Current));
        }
        finally
        {
            Loc.SetLanguage("de");
        }
    }

    [Fact]
    public void Arabic_IsRightToLeft()
    {
        try
        {
            Loc.SetLanguage("ar");
            Assert.True(Loc.IsRightToLeft);
        }
        finally
        {
            Loc.SetLanguage("de");
        }

        Assert.False(Loc.IsRightToLeft);
    }
}
