using Oee.Application.Contracts.Modules;

namespace Oee.Web.Components.Pages;

internal sealed record DashboardRatioCardView(string Key, string Label, DashboardRatioMetric Metric, string Icon, string Description);
