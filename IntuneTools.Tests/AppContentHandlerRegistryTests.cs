using System.Linq;
using System.Reflection;
using Xunit;

namespace IntuneTools.Tests;

/// <summary>
/// Sanity checks for <c>AppContentHandlerRegistry</c>: the handling-mode
/// dispatch is the single contract every Phase 1+ feature relies on, so
/// regressing it should hurt loudly.
/// </summary>
public class AppContentHandlerRegistryTests
{
    private static readonly Assembly IntuneToolsAssembly = typeof(IntuneTools.Utilities.ContentTypeRegistry).Assembly;
    private static readonly System.Type RegistryType = IntuneToolsAssembly.GetType(
        "IntuneTools.Graph.IntuneHelperClasses.Applications.AppContentHandlerRegistry",
        throwOnError: true)!;
    private static readonly System.Type ModeType = IntuneToolsAssembly.GetType(
        "IntuneTools.Graph.IntuneHelperClasses.Applications.HandlingMode",
        throwOnError: true)!;

    private static object? GetMode(string odataType)
    {
        var method = RegistryType.GetMethod("GetMode", BindingFlags.Public | BindingFlags.Static)!;
        return method.Invoke(null, new object?[] { odataType });
    }

    [Theory]
    [InlineData("#microsoft.graph.win32LobApp", "BinaryRoundTrip")]
    [InlineData("#microsoft.graph.iosVppApp", "ManualHandover")]
    [InlineData("#microsoft.graph.macOsVppApp", "ManualHandover")]
    [InlineData("#microsoft.graph.androidManagedStoreApp", "ManualHandover")]
    [InlineData("#microsoft.graph.winGetApp", "ManualHandover")]
    [InlineData("#microsoft.graph.webApp", "Cloneable")]
    [InlineData("#microsoft.graph.officeSuiteApp", "Cloneable")]
    [InlineData("", "Cloneable")]
    [InlineData(null, "Cloneable")]
    public void GetMode_returns_expected_mode(string? odataType, string expectedModeName)
    {
        var actual = GetMode(odataType!);
        Assert.NotNull(actual);
        Assert.Equal(expectedModeName, actual!.ToString());
    }

    [Fact]
    public void GetMode_is_case_insensitive()
    {
        var lower = GetMode("#microsoft.graph.win32lobapp")!.ToString();
        var canonical = GetMode("#microsoft.graph.win32LobApp")!.ToString();
        Assert.Equal(canonical, lower);
        Assert.Equal("BinaryRoundTrip", canonical);
    }

    [Fact]
    public void Win32_handler_is_registered()
    {
        var getHandler = RegistryType.GetMethod("GetHandler", BindingFlags.Public | BindingFlags.Static)!;
        var handler = getHandler.Invoke(null, new object?[] { "#microsoft.graph.win32LobApp" });
        Assert.NotNull(handler);

        var odataProp = handler!.GetType().GetProperty("ODataType")!;
        Assert.Equal("#microsoft.graph.win32LobApp", odataProp.GetValue(handler));
    }

    [Fact]
    public void Manual_handover_list_includes_the_known_tenant_bound_types()
    {
        var prop = RegistryType.GetProperty("ManualHandoverODataTypes", BindingFlags.Public | BindingFlags.Static)!;
        var values = (System.Collections.Generic.IEnumerable<string>)prop.GetValue(null)!;
        var set = values.ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        Assert.Contains("#microsoft.graph.iosVppApp", set);
        Assert.Contains("#microsoft.graph.macOsVppApp", set);
        Assert.Contains("#microsoft.graph.androidManagedStoreApp", set);
        Assert.Contains("#microsoft.graph.androidForWorkApp", set);
        Assert.Contains("#microsoft.graph.winGetApp", set);
    }
}
