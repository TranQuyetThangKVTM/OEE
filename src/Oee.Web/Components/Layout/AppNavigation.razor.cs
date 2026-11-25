using Microsoft.AspNetCore.Components.Routing;

namespace Oee.Web.Components.Layout;

public partial class AppNavigation
{
    // Mỗi bool điều khiển trạng thái xổ/thu của đúng một MudNavGroup; chỉ một nhóm mở tại một thời điểm.
    private bool _productionExpanded;
    private bool _masterDataExpanded;
    private bool _organizationExpanded;
    private bool _reportsExpanded;
    private bool _alertsExpanded;
    private bool _plcExpanded;
    private bool _systemExpanded;
    private AppNavigationGroup _activeGroup;

    protected override void OnInitialized()
    {
        // Theo dõi LocationChanged để mở đúng nhóm cha khi người dùng điều hướng bằng link hoặc code.
        Navigation.LocationChanged += OnLocationChanged;
        ActivateCurrentRoute();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        ActivateCurrentRoute();
        _ = InvokeAsync(StateHasChanged);
    }

    private void ActivateCurrentRoute()
    {
        // Bỏ query/fragment rồi ánh xạ URL hiện tại sang nhóm menu cha.
        var path = "/" + Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', '#')[0].Trim('/');
        _activeGroup = GroupFor(path);
        if (_activeGroup == AppNavigationGroup.None)
        {
            CollapseAll();
        }
        else
        {
            SetExpanded(_activeGroup, true);
        }
    }

    private void SetExpanded(AppNavigationGroup group, bool expanded)
    {
        // Accordion behavior: mở nhóm mới thì thu mọi nhóm khác để menu dễ quan sát.
        if (expanded)
        {
            CollapseAll();
        }

        switch (group)
        {
            case AppNavigationGroup.Production: _productionExpanded = expanded; break;
            case AppNavigationGroup.MasterData: _masterDataExpanded = expanded; break;
            case AppNavigationGroup.Organization: _organizationExpanded = expanded; break;
            case AppNavigationGroup.Reports: _reportsExpanded = expanded; break;
            case AppNavigationGroup.Alerts: _alertsExpanded = expanded; break;
            case AppNavigationGroup.Plc: _plcExpanded = expanded; break;
            case AppNavigationGroup.System: _systemExpanded = expanded; break;
        }
    }

    private void CollapseAll()
    {
        _productionExpanded = false;
        _masterDataExpanded = false;
        _organizationExpanded = false;
        _reportsExpanded = false;
        _alertsExpanded = false;
        _plcExpanded = false;
        _systemExpanded = false;
    }

    // CSS class này tạo “sáng đèn” ở header nhóm cha chứa route đang mở.
    private string GroupHeaderClass(AppNavigationGroup group)
        => group == _activeGroup ? "oee-nav-group oee-nav-group--active" : "oee-nav-group";

    private static AppNavigationGroup GroupFor(string path)
    {
        if (path.StartsWith("/production", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/downtime", StringComparison.OrdinalIgnoreCase)) return AppNavigationGroup.Production;
        if (path.StartsWith("/master", StringComparison.OrdinalIgnoreCase)) return AppNavigationGroup.MasterData;
        if (path.StartsWith("/equipment", StringComparison.OrdinalIgnoreCase)) return AppNavigationGroup.Organization;
        if (path.StartsWith("/reports", StringComparison.OrdinalIgnoreCase)) return AppNavigationGroup.Reports;
        if (path.StartsWith("/alerts", StringComparison.OrdinalIgnoreCase)) return AppNavigationGroup.Alerts;
        if (path.StartsWith("/plc", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/integration", StringComparison.OrdinalIgnoreCase)) return AppNavigationGroup.Plc;
        if (path.StartsWith("/users", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/account/users", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/audit", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/account/audit", StringComparison.OrdinalIgnoreCase)) return AppNavigationGroup.System;
        return AppNavigationGroup.None;
    }

    public void Dispose()
    {
        // Gỡ event handler khi component bị dispose để tránh giữ circuit/component trong bộ nhớ.
        Navigation.LocationChanged -= OnLocationChanged;
        GC.SuppressFinalize(this);
    }
}
