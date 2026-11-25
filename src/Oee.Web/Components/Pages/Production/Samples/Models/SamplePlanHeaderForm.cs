using System.ComponentModel.DataAnnotations;

namespace Oee.Web.Components.Pages.Production;

internal sealed class SamplePlanHeaderForm
{
    [Required, StringLength(32)]
    public string? Code { get; set; }

    [Required, StringLength(200)]
    public string? Name { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }
}
