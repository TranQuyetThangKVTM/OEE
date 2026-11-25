using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>Kiểm tra transport thật của Blazor/SignalR, không chỉ thuộc tính Authorize trên page.</summary>
[Collection("Read endpoint host")]
public sealed class RealtimeTransportSecurityTests(ReadEndpointHostFixture fixture)
{
    [Theory]
    [InlineData("POST", "/_blazor/negotiate?negotiateVersion=1")]
    [InlineData("GET", "/_blazor?id=unknown")]
    [InlineData("POST", "/_blazor/disconnect")]
    [InlineData("GET", "/_blazor/initializers")]
    [InlineData("POST", "/hubs/oee-live/negotiate?negotiateVersion=1")]
    [InlineData("GET", "/hubs/oee-live?id=unknown")]
    public async Task AnonymousTransportRequestIsRejectedBeforeCreatingAConnection(string method, string path)
    {
        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.DoesNotContain("connectionToken", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/_blazor")]
    [InlineData("/hubs/oee-live")]
    public async Task AnonymousWebSocketCannotUpgradeWithoutNegotiation(string path)
    {
        var socketClient = fixture.App.Server.CreateWebSocketClient();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var socket = await socketClient.ConnectAsync(new Uri("ws://localhost" + path), CancellationToken.None);
        });
        Assert.Contains("status code: 401", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnonymousLoginRemainsUsableWithoutABlazorCircuit()
    {
        using var client = fixture.CreateClient();
        using var response = await client.GetAsync("/Account/Login");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("\"type\":\"server\"", html, StringComparison.Ordinal);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        using var post = await client.PostAsync("/account/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token),
            ["returnUrl"] = "/",
            ["userName"] = "",
            ["password"] = ""
        }));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains("error=required", post.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/_blazor")]
    [InlineData("/hubs/oee-live")]
    public async Task SignedInReaderCanNegotiateAndConnect(string path)
    {
        // Chỉ thay lookup user SQL trong test; cookie được ký/giải mã bởi handler thật.
        using var app = fixture.App.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme,
                options => options.Events.OnValidatePrincipal = _ => Task.CompletedTask)));
        using var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "transport-reader")], IdentityConstants.ApplicationScheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), IdentityConstants.ApplicationScheme);
        var cookie = options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket);
        client.DefaultRequestHeaders.Add("Cookie", cookie);

        using var response = await client.PostAsync(path + "/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var negotiation = await response.Content.ReadFromJsonAsync<Negotiation>();
        Assert.False(string.IsNullOrEmpty(negotiation?.ConnectionToken));

        var socketClient = app.Server.CreateWebSocketClient();
        socketClient.ConfigureRequest = request => request.Headers.Cookie = cookie;
        using var socket = await socketClient.ConnectAsync(new Uri("ws://localhost" + path + "?id=" + Uri.EscapeDataString(negotiation!.ConnectionToken)), CancellationToken.None);
        Assert.Equal(WebSocketState.Open, socket.State);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var protocol = path == "/_blazor" ? "blazorpack" : "json";
        var handshake = "{\"protocol\":\"" + protocol + "\",\"version\":1}\u001e";
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(handshake)),
            WebSocketMessageType.Text, true, timeout.Token);
        var buffer = new byte[4096];
        var received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        // BlazorPack có thể gộp handshake JSON và thông điệp nhị phân trong cùng frame.
        Assert.NotEqual(WebSocketMessageType.Close, received.MessageType);
        Assert.StartsWith("{}\u001e", Encoding.UTF8.GetString(buffer, 0, received.Count), StringComparison.Ordinal);
    }

    private sealed record Negotiation(string ConnectionToken);
}
