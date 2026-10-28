using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace APIGateway.Proxy.UnitTests.Configuration;

/// <summary>Exercise the real configured YARP routes over loopback, including anonymous requests.</summary>
public sealed class QuickQuoteForwardingTests
{
    private const string Payload = """{"vertical":"Removals","sizeId":"2-bed","origin":{"address":"Pickup","latitude":52,"longitude":-1,"floor":3,"hasElevator":false},"destination":{"address":"Delivery","latitude":51,"longitude":-2,"floor":2,"hasElevator":true}}""";

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(429)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task Starter_ForwardsExpandedContractAndStatus_WithoutAuthentication(int status)
    {
        string? received = null;
        await using var upstream = CreateApp();
        upstream.MapPost("/api/v2/quote/starter", async context =>
        {
            received = await new StreamReader(context.Request.Body).ReadToEndAsync(context.RequestAborted);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            context.Response.Headers.RetryAfter = "2";
            await context.Response.WriteAsync("{\"lowTotal\":123.60,\"estimateBasis\":{\"crewCount\":1},\"code\":\"preview\"}", context.RequestAborted);
        });
        await upstream.StartAsync();
        await using var proxy = CreateProxy(Address(upstream));
        await proxy.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(Address(proxy)) };
        using var response = await client.PostAsync("/api/v2/quote/starter", new StringContent(Payload, Encoding.UTF8, "application/json"));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(Payload, received);
        Assert.Contains("estimateBasis", await response.Content.ReadAsStringAsync());
        Assert.Equal("2", response.Headers.RetryAfter?.ToString());
        using var protectedResponse = await client.GetAsync("/api/v1/admin/pricing");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Starter_PropagatesCallerCancellation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var upstream = CreateApp();
        upstream.MapPost("/api/v2/quote/starter", async context =>
        {
            started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, context.RequestAborted); }
            catch (OperationCanceledException) { cancelled.TrySetResult(); }
        });
        await upstream.StartAsync();
        await using var proxy = CreateProxy(Address(upstream));
        await proxy.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(Address(proxy)) };
        using var cancellation = new CancellationTokenSource();
        var pending = client.PostAsync("/api/v2/quote/starter", new StringContent(Payload, Encoding.UTF8, "application/json"), cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData("https://tranzzer.com")]
    [InlineData("https://www.tranzzer.com")]
    [InlineData("https://tranzrmoves.com")]
    [InlineData("https://www.tranzrmoves.com")]
    public async Task Starter_AllowsMarketingOriginPreflight(string origin)
    {
        await using var proxy = CreateProxy("http://127.0.0.1:1/");
        await proxy.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(Address(proxy)) };
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v2/quote/starter");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:3000")]
    [InlineData("http://tranzzercom.local")]
    public async Task Starter_AllowsDevelopmentOriginsOnlyInDevelopment(string origin)
    {
        await using var proxy = CreateProxy("http://127.0.0.1:1/", development: true);
        await proxy.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(Address(proxy)) };
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v2/quote/starter");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        using var response = await client.SendAsync(request);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Theory]
    [InlineData("http://127.0.0.1:3000")]
    [InlineData("http://tranzzercom.local")]
    [InlineData("https://untrusted.example")]
    public async Task Starter_DoesNotAllowDevelopmentOrUnlistedOriginsInProduction(string origin)
    {
        await using var proxy = CreateProxy("http://127.0.0.1:1/");
        await proxy.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(Address(proxy)) };
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v2/quote/starter");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        using var response = await client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        return builder.Build();
    }

    private static WebApplication CreateProxy(string upstream, bool development = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        if (development) builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json"));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:tranzr-cluster:Destinations:ai:Address"] = upstream
        });
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, NoCredentialsHandler>("test", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("AuthenticatedPolicy", policy => policy.RequireAuthenticatedUser()));
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyMethod().AllowAnyHeader().AllowCredentials()));
        builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
        var app = builder.Build();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapReverseProxy().RequireAuthorization();
        return app;
    }

    private static string Address(WebApplication app) =>
        app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

    private sealed class NoCredentialsHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
    }
}
