using IntuneTools.Utilities;
using Microsoft.Graph.Beta.Models;
using Xunit;

namespace IntuneTools.Tests;

public class AssignmentInfoTests
{
    [Fact]
    public void FromTarget_NullTarget_ReturnsUnknownType()
    {
        var result = AssignmentInfo.FromTarget("assign-1", null);

        Assert.Equal("assign-1", result.AssignmentId);
        Assert.Equal("Unknown", result.TargetType);
        Assert.Null(result.GroupId);
        Assert.Null(result.FilterId);
    }

    [Fact]
    public void FromTarget_NullAssignmentId_IsPreserved()
    {
        var result = AssignmentInfo.FromTarget(null, null);

        Assert.Null(result.AssignmentId);
        Assert.Equal("Unknown", result.TargetType);
    }

    [Fact]
    public void FromTarget_AllLicensedUsersTarget_MapsCorrectly()
    {
        var target = new AllLicensedUsersAssignmentTarget();

        var result = AssignmentInfo.FromTarget("a1", target);

        Assert.Equal("All Users", result.TargetType);
        Assert.Null(result.GroupId);
    }

    [Fact]
    public void FromTarget_AllDevicesTarget_MapsCorrectly()
    {
        var target = new AllDevicesAssignmentTarget();

        var result = AssignmentInfo.FromTarget("a2", target);

        Assert.Equal("All Devices", result.TargetType);
        Assert.Null(result.GroupId);
    }

    [Fact]
    public void FromTarget_GroupTarget_ExtractsGroupId()
    {
        var target = new GroupAssignmentTarget { GroupId = "group-123" };

        var result = AssignmentInfo.FromTarget("a3", target);

        Assert.Equal("Group", result.TargetType);
        Assert.Equal("group-123", result.GroupId);
    }

    [Fact]
    public void FromTarget_ExclusionGroupTarget_ExtractsGroupId()
    {
        var target = new ExclusionGroupAssignmentTarget { GroupId = "group-456" };

        var result = AssignmentInfo.FromTarget("a4", target);

        Assert.Equal("Exclusion Group", result.TargetType);
        Assert.Equal("group-456", result.GroupId);
    }

    [Fact]
    public void FromTarget_WithFilter_ExtractsFilterInfo()
    {
        var target = new GroupAssignmentTarget
        {
            GroupId = "group-789",
            DeviceAndAppManagementAssignmentFilterId = "filter-1",
            DeviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.Include
        };

        var result = AssignmentInfo.FromTarget("a5", target);

        Assert.Equal("filter-1", result.FilterId);
        Assert.Equal("Include", result.FilterType);
    }

    [Fact]
    public void ToString_BasicTargetType_ReturnsType()
    {
        var info = new AssignmentInfo { TargetType = "All Users" };

        Assert.Equal("All Users", info.ToString());
    }

    [Fact]
    public void ToString_NullTargetType_ReturnsUnknown()
    {
        var info = new AssignmentInfo { TargetType = null };

        Assert.Equal("Unknown", info.ToString());
    }

    [Fact]
    public void ToString_WithGroupId_AppendsGroupId()
    {
        var info = new AssignmentInfo { TargetType = "Group", GroupId = "g-123" };

        Assert.Equal("Group (g-123)", info.ToString());
    }

    [Fact]
    public void ToString_WithFilter_AppendsFilterInfo()
    {
        var info = new AssignmentInfo
        {
            TargetType = "Group",
            GroupId = "g-1",
            FilterId = "f-1",
            FilterType = "Include"
        };

        Assert.Equal("Group (g-1) [Filter: f-1, Type: Include]", info.ToString());
    }

    [Fact]
    public void ToString_FilterWithoutGroupId_OnlyShowsFilter()
    {
        var info = new AssignmentInfo
        {
            TargetType = "All Devices",
            FilterId = "f-2",
            FilterType = "Exclude"
        };

        Assert.Equal("All Devices [Filter: f-2, Type: Exclude]", info.ToString());
    }
}
