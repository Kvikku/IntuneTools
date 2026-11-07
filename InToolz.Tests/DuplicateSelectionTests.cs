namespace InToolz.Tests;

/// <summary>
/// The Cleanup page's "Select older" and "Select unassigned" buttons pre-select items for a permanent
/// delete. These tests pin what they will and, more importantly, will never select.
/// </summary>
public class DuplicateSelectionTests
{
    private static readonly DateTimeOffset T0 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static int _nextId;

    private static DuplicateContentInfo Item(
        string name = "Baseline",
        string type = "Settings Catalog",
        string platform = "windows",
        DateTimeOffset? created = null,
        bool? assigned = null,
        string? id = null) => new()
    {
        ContentName = name,
        ContentType = type,
        ContentPlatform = platform,
        CreatedDateTime = created,
        HasAssignments = assigned,
        ContentId = id ?? $"id-{Interlocked.Increment(ref _nextId)}",
    };

    private static HashSet<string> Ids(IEnumerable<DuplicateContentInfo> items) => items.Select(i => i.ContentId).ToHashSet();

    // --- What "the same item" means ---------------------------------------------------------

    [Fact]
    public void KeyOf_IgnoresCaseAndSurroundingWhitespaceInTheName()
    {
        Assert.Equal(
            DuplicateSelection.KeyOf("Baseline", "Settings Catalog", "windows"),
            DuplicateSelection.KeyOf("  baseline\t", "Settings Catalog", "windows"));
    }

    [Fact]
    public void KeyOf_DoesNotIgnoreInnerWhitespaceInTheName()
    {
        Assert.NotEqual(
            DuplicateSelection.KeyOf("Win Baseline", "T", "p"),
            DuplicateSelection.KeyOf("Win  Baseline", "T", "p"));
    }

    [Fact]
    public void KeyOf_ContentTypeIsExact()
    {
        Assert.NotEqual(
            DuplicateSelection.KeyOf("Baseline", "Settings Catalog", "windows"),
            DuplicateSelection.KeyOf("Baseline", "settings catalog", "windows"));
        Assert.NotEqual(
            DuplicateSelection.KeyOf("Baseline", "App - Windows app (Win32)", "windows"),
            DuplicateSelection.KeyOf("Baseline", "App - iOS VPP app", "windows"));
    }

    [Fact]
    public void KeyOf_PlatformIsTrimmedButCaseSensitive()
    {
        // Characterisation of the behaviour the page always had: platform strings come from Graph in a
        // consistent case, so only whitespace is forgiven.
        Assert.Equal(
            DuplicateSelection.KeyOf("Baseline", "T", "windows"),
            DuplicateSelection.KeyOf("Baseline", "T", " windows "));
        Assert.NotEqual(
            DuplicateSelection.KeyOf("Baseline", "T", "windows"),
            DuplicateSelection.KeyOf("Baseline", "T", "Windows"));
    }

    [Fact]
    public void KeyOf_NullsBehaveAsEmpty()
    {
        Assert.Equal(DuplicateSelection.KeyOf("", "", ""), DuplicateSelection.KeyOf(null, null, null));
    }

    // --- Select older -----------------------------------------------------------------------

    [Fact]
    public void SelectOlder_KeepsTheNewestAndSelectsTheRest()
    {
        var oldest = Item(created: T0);
        var middle = Item(created: T0.AddDays(10));
        var newest = Item(created: T0.AddDays(20));

        var selected = DuplicateSelection.SelectOlder(new[] { middle, newest, oldest });

        Assert.Equal(Ids(new[] { oldest, middle }), Ids(selected));
    }

    [Fact]
    public void SelectOlder_NothingToSelect_WhenNoItemHasADuplicate()
    {
        var items = new[]
        {
            Item(name: "A", created: T0),
            Item(name: "B", created: T0),
            Item(name: "C", created: T0),
        };

        Assert.Empty(DuplicateSelection.SelectOlder(items));
        Assert.Empty(DuplicateSelection.SelectOlder(Array.Empty<DuplicateContentInfo>()));
    }

    [Fact]
    public void SelectOlder_GroupsNamesIgnoringCaseAndWhitespace()
    {
        var older = Item(name: "Baseline", created: T0);
        var newer = Item(name: "  BASELINE ", created: T0.AddDays(1));

        Assert.Equal(Ids(new[] { older }), Ids(DuplicateSelection.SelectOlder(new[] { older, newer })));
    }

    [Fact]
    public void SelectOlder_SameNameButDifferentTypeOrPlatform_IsNotADuplicate()
    {
        var items = new[]
        {
            Item(type: "Settings Catalog", created: T0),
            Item(type: "Device Configuration", created: T0.AddDays(1)),
            Item(platform: "macOS", created: T0.AddDays(2)),
        };

        Assert.Empty(DuplicateSelection.SelectOlder(items));
    }

    [Fact]
    public void SelectOlder_EachGroupKeepsItsOwnNewest()
    {
        var aOld = Item(name: "A", created: T0);
        var aNew = Item(name: "A", created: T0.AddDays(5));
        var bOld = Item(name: "B", created: T0.AddDays(100));
        var bNew = Item(name: "B", created: T0.AddDays(200));

        var selected = DuplicateSelection.SelectOlder(new[] { aOld, bNew, aNew, bOld });

        Assert.Equal(Ids(new[] { aOld, bOld }), Ids(selected));
    }

    [Fact]
    public void SelectOlder_AnItemWithNoCreationDateCountsAsTheOldest()
    {
        var undated = Item(created: null);
        var dated = Item(created: T0);

        Assert.Equal(Ids(new[] { undated }), Ids(DuplicateSelection.SelectOlder(new[] { dated, undated })));
        Assert.Equal(Ids(new[] { undated }), Ids(DuplicateSelection.SelectOlder(new[] { undated, dated })));
    }

    [Fact]
    public void SelectOlder_WhenNoDatesAreKnown_KeepsTheFirstListedAndNeverTheWholeGroup()
    {
        var first = Item(created: null);
        var second = Item(created: null);
        var third = Item(created: null);

        var selected = DuplicateSelection.SelectOlder(new[] { first, second, third });

        Assert.Equal(Ids(new[] { second, third }), Ids(selected));
        Assert.DoesNotContain(first.ContentId, Ids(selected));
    }

    [Fact]
    public void SelectOlder_WhenDatesTie_KeepsTheFirstListed()
    {
        var first = Item(created: T0);
        var second = Item(created: T0);

        Assert.Equal(Ids(new[] { second }), Ids(DuplicateSelection.SelectOlder(new[] { first, second })));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void SelectOlder_NeverSelectsAWholeGroup(int groupSize)
    {
        var group = Enumerable.Range(0, groupSize).Select(n => Item(created: T0.AddDays(n))).ToList();

        var selected = DuplicateSelection.SelectOlder(group);

        Assert.Equal(groupSize - 1, selected.Count);
        Assert.DoesNotContain(group.Last().ContentId, Ids(selected));   // the newest is the one kept
    }

    [Fact]
    public void SelectOlder_DoesNotDependOnTheOrderTheListIsIn()
    {
        var items = Enumerable.Range(0, 6).Select(n => Item(created: T0.AddDays(n))).ToList();

        var forwards = Ids(DuplicateSelection.SelectOlder(items));
        var backwards = Ids(DuplicateSelection.SelectOlder(Enumerable.Reverse(items)));

        Assert.Equal(forwards, backwards);
    }

    // --- Select unassigned ------------------------------------------------------------------

    [Fact]
    public void SelectUnassigned_SelectsTheUnassignedAndKeepsTheAssigned()
    {
        var assigned = Item(assigned: true);
        var unassigned = Item(assigned: false);

        var selected = DuplicateSelection.SelectUnassigned(new[] { assigned, unassigned });

        Assert.Equal(Ids(new[] { unassigned }), Ids(selected));
    }

    [Fact]
    public void SelectUnassigned_SelectsEveryUnassignedItemWhenOneIsAssigned()
    {
        var keeper = Item(assigned: true);
        var a = Item(assigned: false);
        var b = Item(assigned: false);

        Assert.Equal(Ids(new[] { a, b }), Ids(DuplicateSelection.SelectUnassigned(new[] { a, keeper, b })));
    }

    [Fact]
    public void SelectUnassigned_AGroupWithNothingAssigned_SelectsNothing()
    {
        // No assigned item to keep, so there is no safe choice. The user decides.
        var items = new[] { Item(assigned: false), Item(assigned: false), Item(assigned: false) };

        Assert.Empty(DuplicateSelection.SelectUnassigned(items));
    }

    [Fact]
    public void SelectUnassigned_AnItemWhoseAssignmentsAreUnknownIsNeverSelected()
    {
        var assigned = Item(assigned: true);
        var unknown = Item(assigned: null);
        var unassigned = Item(assigned: false);

        var selected = DuplicateSelection.SelectUnassigned(new[] { assigned, unknown, unassigned });

        Assert.Equal(Ids(new[] { unassigned }), Ids(selected));
        Assert.DoesNotContain(unknown.ContentId, Ids(selected));
    }

    [Fact]
    public void SelectUnassigned_AGroupWhereNothingCouldBeChecked_SelectsNothing()
    {
        var items = new[] { Item(assigned: null), Item(assigned: null) };

        Assert.Empty(DuplicateSelection.SelectUnassigned(items));
    }

    [Fact]
    public void SelectUnassigned_AnUnassignedItemWithNoDuplicate_IsNotSelected()
    {
        var items = new[]
        {
            Item(name: "Lonely", assigned: false),
            Item(name: "Pair", assigned: true),
            Item(name: "Pair", assigned: false),
        };

        var selected = DuplicateSelection.SelectUnassigned(items);

        Assert.Single(selected);
        Assert.Equal("Pair", selected[0].ContentName);
    }

    [Fact]
    public void SelectUnassigned_GroupsAreIndependent()
    {
        var aAssigned = Item(name: "A", assigned: true);
        var aUnassigned = Item(name: "A", assigned: false);
        var bUnassigned1 = Item(name: "B", assigned: false);
        var bUnassigned2 = Item(name: "B", assigned: false);

        var selected = DuplicateSelection.SelectUnassigned(new[] { bUnassigned1, aUnassigned, bUnassigned2, aAssigned });

        Assert.Equal(Ids(new[] { aUnassigned }), Ids(selected));
    }

    [Fact]
    public void SelectUnassigned_NeverSelectsAnAssignedItem()
    {
        var items = new List<DuplicateContentInfo>();
        foreach (var name in new[] { "A", "B", "C" })
        {
            items.Add(Item(name: name, assigned: true));
            items.Add(Item(name: name, assigned: true));
            items.Add(Item(name: name, assigned: false));
            items.Add(Item(name: name, assigned: null));
        }

        var selected = DuplicateSelection.SelectUnassigned(items);

        Assert.Equal(3, selected.Count);
        Assert.All(selected, i => Assert.False(i.HasAssignments));
    }
}
