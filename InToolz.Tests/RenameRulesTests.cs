using System.Xml.Linq;

namespace InToolz.Tests;

/// <summary>
/// The rules behind the Renaming page. Rename writes new names into live tenants, so "which mode
/// is selected" and "is this enough to start" are exactly the kind of decisions #104 and #116 are
/// about: they used to live in two copies in the page's code-behind, with no way to test either.
/// </summary>
public class RenameRulesTests
{
    public static TheoryData<Variables.RenameMode> AllModes()
    {
        var data = new TheoryData<Variables.RenameMode>();
        foreach (var mode in Enum.GetValues<Variables.RenameMode>()) data.Add(mode);
        return data;
    }

    // --- Parsing the mode from a ComboBoxItem Tag -------------------------------------------

    [Theory]
    [MemberData(nameof(AllModes))]
    public void TryParseMode_EveryModeParsesFromItsOwnName(Variables.RenameMode expected)
    {
        Assert.True(RenameRules.TryParseMode(expected.ToString(), out var mode));
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("prefix")]            // names are exact: no case folding
    [InlineData("PREFIX")]
    [InlineData(" Prefix")]           // and no trimming
    [InlineData("Prefix ")]
    [InlineData("Replace")]           // not a mode
    [InlineData("Prefix,Suffix")]     // not a flags combination
    [InlineData("0")]                 // a number is not a mode, even though Enum.TryParse would accept it
    [InlineData("3")]
    [InlineData("99")]
    [InlineData("-1")]
    public void TryParseMode_AnythingElseIsNotAMode(string? tag)
    {
        Assert.False(RenameRules.TryParseMode(tag, out _));
    }

    [Fact]
    public void TryParseMode_NotAMode_DoesNotQuietlyBecomeTheDefault()
    {
        // The old code turned any out-of-range ComboBox index into index 0, which is Prefix.
        // A selection that can't be read has to be refused, never reinterpreted.
        Assert.False(RenameRules.TryParseMode("not a mode", out var mode));

        // The out value is the enum's default, which is Prefix. That is why callers must act on the
        // return value, and why the page refuses to rename when it is false.
        Assert.Equal(Variables.RenameMode.Prefix, mode);
    }

    // --- What each mode needs ---------------------------------------------------------------

    [Theory]
    [InlineData(Variables.RenameMode.Prefix, true)]
    [InlineData(Variables.RenameMode.Suffix, true)]
    [InlineData(Variables.RenameMode.FindAndReplace, true)]
    [InlineData(Variables.RenameMode.Description, true)]
    [InlineData(Variables.RenameMode.RemovePrefix, false)]       // blank = remove a bracket-style prefix
    [InlineData(Variables.RenameMode.RemoveSuffix, true)]        // no auto-detect for suffixes
    [InlineData(Variables.RenameMode.RemoveDescription, false)]  // takes no input
    public void RequiresNewName_PerMode(Variables.RenameMode mode, bool expected)
    {
        Assert.Equal(expected, RenameRules.RequiresNewName(mode));
    }

    [Fact]
    public void RequiresNewName_IsDecidedForEveryMode()
    {
        // A new mode added to the enum shows up here, so someone has to decide on purpose whether it
        // needs text, instead of it silently inheriting "yes".
        var decided = new[]
        {
            Variables.RenameMode.Prefix, Variables.RenameMode.Suffix, Variables.RenameMode.FindAndReplace,
            Variables.RenameMode.Description, Variables.RenameMode.RemovePrefix, Variables.RenameMode.RemoveSuffix,
            Variables.RenameMode.RemoveDescription,
        };
        Assert.Equal(Enum.GetValues<Variables.RenameMode>().OrderBy(m => m), decided.OrderBy(m => m));
    }

    // --- Validation -------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Validate_CompleteInputs_AreAccepted(Variables.RenameMode mode)
    {
        Assert.Null(RenameRules.Validate(mode, "Some text", prefixSymbolSelected: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankName_IsRejectedWhereTheModeNeedsText(string? blank)
    {
        Assert.Equal("Please enter a new name.", RenameRules.Validate(Variables.RenameMode.Prefix, blank, true));
        Assert.Equal("Please enter a new name.", RenameRules.Validate(Variables.RenameMode.Suffix, blank, true));
        Assert.Equal("Please enter a new name.", RenameRules.Validate(Variables.RenameMode.Description, blank, true));
        Assert.Equal("Please enter a new name.", RenameRules.Validate(Variables.RenameMode.RemoveSuffix, blank, true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankFindText_GetsItsOwnMessage(string? blank)
    {
        Assert.Equal("Please enter the text to find.", RenameRules.Validate(Variables.RenameMode.FindAndReplace, blank, true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankName_IsFineWhereTheModeNeedsNone(string? blank)
    {
        Assert.Null(RenameRules.Validate(Variables.RenameMode.RemovePrefix, blank, prefixSymbolSelected: false));
        Assert.Null(RenameRules.Validate(Variables.RenameMode.RemoveDescription, blank, prefixSymbolSelected: false));
    }

    [Fact]
    public void Validate_PrefixMode_NeedsABracketStyle()
    {
        Assert.Equal("Please select a prefix option.", RenameRules.Validate(Variables.RenameMode.Prefix, "Team", prefixSymbolSelected: false));
        Assert.Null(RenameRules.Validate(Variables.RenameMode.Prefix, "Team", prefixSymbolSelected: true));
    }

    [Theory]
    [InlineData(Variables.RenameMode.Suffix)]
    [InlineData(Variables.RenameMode.FindAndReplace)]
    [InlineData(Variables.RenameMode.Description)]
    [InlineData(Variables.RenameMode.RemovePrefix)]
    [InlineData(Variables.RenameMode.RemoveSuffix)]
    [InlineData(Variables.RenameMode.RemoveDescription)]
    public void Validate_OnlyPrefixModeCaresAboutTheBracketStyle(Variables.RenameMode mode)
    {
        Assert.Null(RenameRules.Validate(mode, "text", prefixSymbolSelected: false));
    }

    [Fact]
    public void Validate_MissingNameIsReportedBeforeMissingBracketStyle()
    {
        // Same order the page always checked them in, so the user fixes the first thing first.
        Assert.Equal("Please enter a new name.", RenameRules.Validate(Variables.RenameMode.Prefix, "", prefixSymbolSelected: false));
    }

    // --- The XAML and the enum agree ----------------------------------------------------------
    // The enum used to say "keep integer mapping stable with ComboBox order", and the page cast the
    // ComboBox index to the enum. Nothing enforced that; reordering an item in the XAML would have
    // made "Add Suffix" rename in a different mode. The items now carry their mode as a Tag.

    [Fact]
    public void RenamingPageXaml_EveryComboBoxItemHasAValidUniqueModeTag_CoveringEveryMode()
    {
        var xaml = XDocument.Load(RepoFile("Pages", "RenamingPage.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var comboBox = xaml.Descendants(presentation + "ComboBox")
            .Single(e => (string?)e.Attribute(x + "Name") == "RenameModeComboBox");
        var tags = comboBox.Elements(presentation + "ComboBoxItem")
            .Select(item => (string?)item.Attribute("Tag"))
            .ToList();

        Assert.All(tags, tag =>
        {
            Assert.False(string.IsNullOrEmpty(tag), "Every item in the rename-mode ComboBox needs a Tag.");
            Assert.True(RenameRules.TryParseMode(tag, out _), $"Tag '{tag}' is not a RenameMode name.");
        });
        Assert.Equal(tags.Count, tags.Distinct().Count());
        Assert.Equal(
            Enum.GetNames<Variables.RenameMode>().OrderBy(n => n, StringComparer.Ordinal),
            tags.OrderBy(t => t, StringComparer.Ordinal));
    }

    // Finds a file in the source tree by walking up from the test binaries to the folder that holds
    // InToolz.csproj. Fails loudly if it can't, rather than skipping.
    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "InToolz.csproj")))
            dir = dir.Parent;

        Assert.True(dir != null, $"Could not find InToolz.csproj above {AppContext.BaseDirectory}, so the source tree is not available to this test.");
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }
}
