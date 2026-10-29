using System.Text.Json.Nodes;
using IntuneTools.Graph.DemoMode;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using Microsoft.Kiota.Abstractions.Authentication;

namespace IntuneTools.Tests;

/// <summary>
/// Demo Mode's fake Graph backend itself: telling demo clients apart from live ones, and list
/// paging behaving like Graph so code that pages is exercised the same way.
/// </summary>
public class DemoModeTests
{
    [Fact]
    public void IsDemoClient_DemoModeTenant_ReturnsTrue()
    {
        var (client, _) = DemoModeService.CreateSourceTenant();

        Assert.True(DemoModeService.IsDemoClient(client));
    }

    [Fact]
    public void IsDemoClient_OtherClient_ReturnsFalse()
    {
        var client = new GraphServiceClient(new HttpClient(), new AnonymousAuthenticationProvider());

        Assert.False(DemoModeService.IsDemoClient(client));
        Assert.False(DemoModeService.IsDemoClient(null));
    }

    [Fact]
    public async Task SourceSeed_IncludesTheIosVppApp()
    {
        var (client, _) = DemoModeService.CreateSourceTenant();

        var apps = await client.DeviceAppManagement.MobileApps.GetAsync();

        var vpp = Assert.Single(apps!.Value!.OfType<IosVppApp>());
        Assert.Equal("Adobe Acrobat Reader", vpp.DisplayName);
    }

    [Fact]
    public async Task List_WithTop_ReturnsOnePageAndANextLink()
    {
        var graph = new DemoGraphTestClient();
        AddGroups(graph, "Group", 250);

        var page = await graph.Client.Groups.GetAsync(rc => rc.QueryParameters.Top = 100);

        Assert.Equal(100, page!.Value!.Count);
        Assert.NotNull(page.OdataNextLink);
    }

    [Fact]
    public async Task List_WithoutTop_UsesTheDefaultPageSize()
    {
        var graph = new DemoGraphTestClient();
        AddGroups(graph, "Group", 150);

        var page = await graph.Client.Groups.GetAsync();

        Assert.Equal(100, page!.Value!.Count);
        Assert.NotNull(page.OdataNextLink);
    }

    [Fact]
    public async Task List_LastPage_HasNoNextLink()
    {
        var graph = new DemoGraphTestClient();
        AddGroups(graph, "Group", 40);

        var page = await graph.Client.Groups.GetAsync(rc => rc.QueryParameters.Top = 100);

        Assert.Equal(40, page!.Value!.Count);
        Assert.Null(page.OdataNextLink);
    }

    [Fact]
    public async Task List_FilterAcrossPages_EveryPageKeepsTheFilter()
    {
        var graph = new DemoGraphTestClient();
        AddGroups(graph, "Sales", 120);
        AddGroups(graph, "Finance", 130);

        var first = await graph.Client.Groups.GetAsync(rc =>
        {
            rc.QueryParameters.Filter = "contains(displayName, 'Sales')";
            rc.QueryParameters.Top = 50;
        });

        var groups = new List<Group>();
        var iterator = Microsoft.Graph.PageIterator<Group, GroupCollectionResponse>.CreatePageIterator(graph.Client, first!, group =>
        {
            groups.Add(group);
            return true;
        });
        await iterator.IterateAsync();

        Assert.Equal(120, groups.Count);
        Assert.All(groups, g => Assert.StartsWith("Sales", g.DisplayName));
        Assert.Equal(120, groups.Select(g => g.Id).Distinct().Count());
    }

    private static void AddGroups(DemoGraphTestClient graph, string prefix, int count)
    {
        for (var i = 0; i < count; i++)
        {
            graph.Store.Add("groups", new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["@odata.type"] = "#microsoft.graph.group",
                ["displayName"] = $"{prefix} {i:D3}"
            });
        }
    }
}
