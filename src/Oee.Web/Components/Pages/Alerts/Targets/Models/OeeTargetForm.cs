
namespace Oee.Web.Components.Pages.Alerts;

internal sealed class OeeTargetForm
{
    public string ScopeType { get; set; } = "Global";
    public string? ScopeId { get; set; }
    public string PeriodCode { get; set; } = "DAY";
    public decimal Oee { get; set; }
    public decimal Availability { get; set; }
    public decimal Performance { get; set; }
    public decimal Quality { get; set; }
    public DateTime? From { get; set; } = DateTime.Today;
    public DateTime? To { get; set; }
    public string? Note { get; set; }
}
