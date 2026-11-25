using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Oee.Infrastructure.IntegrationTests;

[Collection("Read endpoint host")]
public sealed class PdfReportEndpointTests(ReadEndpointHostFixture fixture)
{
    [Fact]
    public async Task SignedInReaderDownloadsPdfWithLegacyUnconfiguredLicense()
    {
        var query = new ReportQueryStub();
        using var app = fixture.App.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Reporting:QuestPdfLicense"] = "Unconfigured" }));
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IReportAnalysisQueryService>(_ => query);
                services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme,
                    options => options.Events.OnValidatePrincipal = _ => Task.CompletedTask);
            });
        });
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "pdf-reader")], IdentityConstants.ApplicationScheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), IdentityConstants.ApplicationScheme);
        client.DefaultRequestHeaders.Add("Cookie", options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket));

        using var response = await client.GetAsync("/api/v1/reports/ShiftKpi/pdf?fromDate=2026-11-04&toDate=2026-11-04");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("ShiftKpi-20261104-20261104.pdf", response.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
        Assert.Equal(5000, query.LastQuery?.PageSize);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("Máy thổi chai", ContentOrderTextExtractor.GetText(Assert.Single(pdf.GetPages())), StringComparison.Ordinal);
    }

    private sealed class ReportQueryStub : IReportAnalysisQueryService
    {
        public ReportQuery? LastQuery { get; private set; }

        public Task<ReportFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReportData> QueryAsync(ReportQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(new ReportData(query.Kind, "KPI theo ca", "04/11/2026", "Asia/Ho_Chi_Minh",
                DateTimeOffset.UtcNow, [], [], [],
                [new(null, "Ca sáng", WorkUnit: "M01 - Máy thổi chai", Total: 285000, Good: 284836,
                    Defect: 164, Availability: 1, Performance: .99m, Quality: .999m, Oee: .989m)], 1, true));
        }
    }
}
