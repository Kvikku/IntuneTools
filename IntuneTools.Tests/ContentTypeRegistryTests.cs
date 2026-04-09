using IntuneTools.Utilities;
using Xunit;

namespace IntuneTools.Tests;

public class ContentTypeRegistryTests
{
    [Theory]
    [InlineData("Settings Catalog")]
    [InlineData("Device Compliance Policy")]
    [InlineData("Application")]
    [InlineData("PowerShell Script")]
    public void Get_KnownContentType_ReturnsOperations(string contentType)
    {
        var result = ContentTypeRegistry.Get(contentType);

        Assert.NotNull(result);
        Assert.Equal(contentType, result.ContentType);
        Assert.NotNull(result.LoadAll);
        Assert.NotNull(result.Search);
        Assert.False(string.IsNullOrWhiteSpace(result.DisplayNamePlural));
    }

    [Theory]
    [InlineData("settings catalog")]
    [InlineData("SETTINGS CATALOG")]
    [InlineData("Settings catalog")]
    public void Get_IsCaseInsensitive(string contentType)
    {
        var result = ContentTypeRegistry.Get(contentType);

        Assert.NotNull(result);
        Assert.Equal("Settings Catalog", result.ContentType);
    }

    [Theory]
    [InlineData("NonExistent")]
    [InlineData("")]
    [InlineData("  ")]
    public void Get_UnknownContentType_ReturnsNull(string contentType)
    {
        var result = ContentTypeRegistry.Get(contentType);

        Assert.Null(result);
    }

    [Fact]
    public void All_ReturnsAllRegisteredTypes()
    {
        var all = ContentTypeRegistry.All;

        Assert.NotEmpty(all);
        // Verify a few expected types are present
        Assert.Contains(all, op => op.ContentType == ContentTypes.SettingsCatalog);
        Assert.Contains(all, op => op.ContentType == ContentTypes.Application);
        Assert.Contains(all, op => op.ContentType == ContentTypes.EntraGroup);
    }

    [Fact]
    public void All_HasUniqueContentTypes()
    {
        var all = ContentTypeRegistry.All;
        var types = all.Select(op => op.ContentType).ToList();

        Assert.Equal(types.Count, types.Distinct().Count());
    }

    [Fact]
    public void GetMany_ReturnsMatchingSubset()
    {
        var requested = new[] { ContentTypes.SettingsCatalog, ContentTypes.Application };

        var results = ContentTypeRegistry.GetMany(requested).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(ContentTypes.SettingsCatalog, results[0].ContentType);
        Assert.Equal(ContentTypes.Application, results[1].ContentType);
    }

    [Fact]
    public void GetMany_SkipsUnknownTypes()
    {
        var requested = new[] { ContentTypes.SettingsCatalog, "NonExistent", ContentTypes.Application };

        var results = ContentTypeRegistry.GetMany(requested).ToList();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void GetMany_EmptyInput_ReturnsEmpty()
    {
        var results = ContentTypeRegistry.GetMany(Array.Empty<string>()).ToList();

        Assert.Empty(results);
    }
}
