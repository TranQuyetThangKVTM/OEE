using Oee.Application.Security;

namespace Oee.Application.UnitTests;

/// <summary>
/// Unit test khóa ma trận RBAC theo capability. Các assertion ngăn role nhận quyền rộng hơn
/// khi policy được sửa về sau; backend vẫn là nơi bắt buộc enforcement.
/// </summary>
public sealed class RoleCapabilityMatrixTests
{
    [Fact]
    public void AdminHasEveryCapability()
    {
        // Arrange/Act/Assert: duyệt catalog capability và yêu cầu Admin có toàn bộ.
        Assert.All(Capabilities.All, capability =>
            Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Admin, capability)));
    }

    [Theory]
    [InlineData("PlanningManager", "users.manage")]
    [InlineData("PlanningManager", "audit.read")]
    [InlineData("Operator", "master.write")]
    [InlineData("Viewer", "production.output.write")]
    public void RestrictedRolesDoNotGainPrivilegedCapabilities(string role, string capability)
    {
        // Theory chạy nhiều cặp role/quyền nhạy cảm để chống privilege regression.
        Assert.False(RoleCapabilityMatrix.RoleHasCapability(role, capability));
    }

    [Fact]
    public void ViewerIsReadOnly()
    {
        // Assert quyền đọc cần thiết trước, rồi bảo đảm không capability write/manage nào lọt vào Viewer.
        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Viewer, Capabilities.DashboardRead));
        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Viewer, Capabilities.ReportsRead));
        Assert.DoesNotContain(Capabilities.All.Where(capability => capability.EndsWith("write", StringComparison.Ordinal)
            || capability.EndsWith("manage", StringComparison.Ordinal)), capability =>
            RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Viewer, capability));
    }

    [Fact]
    public void ProductionMutationsFollowApprovedRoleBoundaries()
    {
        // PlanningManager quản lý plan; Operator chỉ ghi downtime; Viewer không mutation.
        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.PlanningManager, Capabilities.ProductionPlanManage));
        Assert.False(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Operator, Capabilities.ProductionPlanManage));

        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Operator, Capabilities.DowntimeWrite));
        Assert.False(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Viewer, Capabilities.DowntimeWrite));
    }

    [Fact]
    public void AlertMutationsAreAvailableToOperationsButNotViewer()
    {
        // Alert acknowledgement thuộc vận hành, còn snapshot recalculation bị giới hạn chặt hơn.
        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Admin, Capabilities.AlertsManage));
        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.PlanningManager, Capabilities.AlertsManage));
        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Operator, Capabilities.AlertsManage));
        Assert.False(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Viewer, Capabilities.AlertsManage));
        Assert.False(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Operator, Capabilities.SnapshotsManage));
    }

    [Fact]
    public void MasterDataReadAndWriteFollowApprovedRoleBoundaries()
    {
        // Kiểm tra riêng read/write để tránh việc có quyền xem vô tình đồng nghĩa quyền sửa.
        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.PlanningManager, Capabilities.MasterDataRead));
        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.PlanningManager, Capabilities.MasterDataWrite));
        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Viewer, Capabilities.MasterDataRead));
        Assert.False(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Viewer, Capabilities.MasterDataWrite));
        Assert.False(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Operator, Capabilities.MasterDataRead));
        Assert.False(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Operator, Capabilities.MasterDataWrite));
    }

    [Fact]
    public void PlcConfigurationIsRestrictedToAdminAndPlanningManager()
    {
        // Cấu hình PLC ảnh hưởng ingestion nên Operator/Viewer không được PlcManage.
        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Admin, Capabilities.PlcManage));
        Assert.True(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.PlanningManager, Capabilities.PlcManage));
        Assert.False(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Operator, Capabilities.PlcManage));
        Assert.False(RoleCapabilityMatrix.RoleHasCapability(RoleCapabilityMatrix.Viewer, Capabilities.PlcManage));
    }
}
