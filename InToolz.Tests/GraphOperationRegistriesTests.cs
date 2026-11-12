using Microsoft.Graph.Beta;

namespace InToolz.Tests;

/// <summary>
/// These registries only build delegates (closures over a GraphServiceClient) without invoking
/// them, so a Demo-Mode-backed client is enough to construct them for key/shape assertions —
/// nothing here makes a network call.
/// </summary>
public class GraphOperationRegistriesTests
{
    private static GraphServiceClient NewClient() => new DemoGraphTestClient().Client;

    // ----- Assign -----

    [Theory]
    [InlineData("Settings Catalog")]
    [InlineData("Device Compliance Policy")]
    [InlineData("macOS Shell Script")]
    [InlineData("Apple BYOD Enrollment Profile")]
    public void AssignRegistry_KnownContentType_HasDefinition(string contentType)
    {
        var registry = GraphOperationRegistries.GetAssignTypeRegistry()
            .ToDictionary(d => d.TypeKey, StringComparer.OrdinalIgnoreCase);

        Assert.True(registry.TryGetValue(contentType, out var definition));
        Assert.NotNull(definition!.AssignAsync);
    }

    [Fact]
    public void AssignRegistry_IncludesMacOSShellScriptWithCorrectCasing()
    {
        // Regression test: this registry used to key macOS Shell Script as "MacOS Shell Script"
        // (capital M), which didn't match ContentTypes.MacOSShellScript ("macOS Shell Script"),
        // so assigning one from AssignmentPage silently did nothing.
        var registry = GraphOperationRegistries.GetAssignTypeRegistry().ToList();

        Assert.Contains(registry, d => d.TypeKey == ContentTypes.MacOSShellScript);
    }

    [Fact]
    public void AssignRegistry_HasUniqueKeys()
    {
        var keys = GraphOperationRegistries.GetAssignTypeRegistry().Select(d => d.TypeKey).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AssignRegistry_EveryKeyExistsInContentTypeRegistry()
    {
        foreach (var definition in GraphOperationRegistries.GetAssignTypeRegistry())
        {
            Assert.NotNull(ContentTypeRegistry.Get(definition.TypeKey));
        }
    }

    [Fact]
    public void AssignRegistry_ExcludesNonAssignableTypes()
    {
        // AssignmentFilter/EntraGroup are assignment targets/modifiers, not assignable content;
        // Application is handled via a separate "App - " prefix special case, not a dictionary key.
        var keys = GraphOperationRegistries.GetAssignTypeRegistry().Select(d => d.TypeKey).ToList();

        Assert.DoesNotContain(ContentTypes.AssignmentFilter, keys);
        Assert.DoesNotContain(ContentTypes.EntraGroup, keys);
        Assert.DoesNotContain(ContentTypes.Application, keys);
    }

    // ----- Delete -----

    [Fact]
    public void DeleteRegistry_HasUniqueKeys()
    {
        var keys = GraphOperationRegistries.GetDeleteTypeRegistry(NewClient(), _ => Task.FromResult(true))
            .Select(d => d.TypeKey).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void DeleteRegistry_EveryKeyExistsInContentTypeRegistry()
    {
        foreach (var definition in GraphOperationRegistries.GetDeleteTypeRegistry(NewClient(), _ => Task.FromResult(true)))
        {
            Assert.NotNull(ContentTypeRegistry.Get(definition.TypeKey));
        }
    }

    [Fact]
    public void DeleteRegistry_CoversEveryContentType()
    {
        // Unlike Assign/Import, delete has no intentional gaps: every registered content type
        // (including Application, AssignmentFilter, EntraGroup) can be deleted directly.
        var keys = GraphOperationRegistries.GetDeleteTypeRegistry(NewClient(), _ => Task.FromResult(true))
            .Select(d => d.TypeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var op in ContentTypeRegistry.All)
        {
            Assert.Contains(op.ContentType, keys);
        }
    }

    [Fact]
    public async Task DeleteRegistry_AutoPilotProfile_UsesInjectedHandler()
    {
        var called = false;
        Task<bool> Handler(string id) { called = true; return Task.FromResult(true); }

        var registry = GraphOperationRegistries.GetDeleteTypeRegistry(NewClient(), Handler)
            .ToDictionary(d => d.TypeKey, StringComparer.OrdinalIgnoreCase);

        Assert.True(registry.TryGetValue(ContentTypes.WindowsAutoPilotProfile, out var definition));
        await definition!.DeleteAsync("some-id");

        Assert.True(called);
    }

    // ----- Rename -----

    [Fact]
    public void RenameRegistry_HasUniqueKeys()
    {
        var keys = GraphOperationRegistries.GetRenameTypeRegistry(NewClient()).Select(d => d.TypeKey).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void RenameRegistry_EveryKeyExistsInContentTypeRegistry()
    {
        foreach (var definition in GraphOperationRegistries.GetRenameTypeRegistry(NewClient()))
        {
            Assert.NotNull(ContentTypeRegistry.Get(definition.TypeKey));
        }
    }

    [Fact]
    public void RenameRegistry_CoversEveryContentType()
    {
        var keys = GraphOperationRegistries.GetRenameTypeRegistry(NewClient())
            .Select(d => d.TypeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var op in ContentTypeRegistry.All)
        {
            Assert.Contains(op.ContentType, keys);
        }
    }

    // ----- Import -----

    [Fact]
    public void ImportRegistry_HasUniqueKeys()
    {
        var client = NewClient();
        var keys = GraphOperationRegistries
            .GetImportTypeRegistry(false, false, new List<string>(), client, client)
            .Select(d => d.TypeKey).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ImportRegistry_EveryKeyExistsInContentTypeRegistry()
    {
        var client = NewClient();
        foreach (var definition in GraphOperationRegistries.GetImportTypeRegistry(false, false, new List<string>(), client, client))
        {
            Assert.NotNull(ContentTypeRegistry.Get(definition.TypeKey));
        }
    }

    [Fact]
    public void ImportRegistry_ExcludesApplication()
    {
        // Apps need their binary package transferred, not just Graph metadata, so the simple
        // metadata import pipeline used for every other type doesn't apply.
        var client = NewClient();
        var keys = GraphOperationRegistries.GetImportTypeRegistry(false, false, new List<string>(), client, client)
            .Select(d => d.TypeKey).ToList();

        Assert.DoesNotContain(ContentTypes.Application, keys);
    }

    // ----- Json -----

    [Theory]
    [InlineData("Settings Catalog")]
    [InlineData("settings catalog")]
    [InlineData("SETTINGS CATALOG")]
    public void JsonContentTypeOperations_IsCaseInsensitive(string contentType)
    {
        Assert.True(GraphOperationRegistries.JsonContentTypeOperations.TryGetValue(contentType, out var ops));
        Assert.NotNull(ops.Export);
        Assert.NotNull(ops.Import);
    }

    [Fact]
    public void JsonContentTypeOperations_EveryKeyExistsInContentTypeRegistry()
    {
        foreach (var key in GraphOperationRegistries.JsonContentTypeOperations.Keys)
        {
            Assert.NotNull(ContentTypeRegistry.Get(key));
        }
    }

    [Fact]
    public void JsonContentTypeOperations_ExcludesApplication()
    {
        // Apps need their binary package transferred, not just Graph metadata.
        Assert.False(GraphOperationRegistries.JsonContentTypeOperations.ContainsKey(ContentTypes.Application));
    }
}
