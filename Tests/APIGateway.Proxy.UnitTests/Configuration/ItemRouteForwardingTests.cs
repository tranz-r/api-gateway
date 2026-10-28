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
public sealed class ItemRouteForwardingTests
{
    [Theory]
    [InlineData("replan-proposals", 200)]
    [InlineData("replan-proposals/7da10375-c6f4-4c7c-aa2f-b2fd83824093/apply", 409)]
    [InlineData("7da10375-c6f4-4c7c-aa2f-b2fd83824093/confirm-windows", 422)]
    [InlineData("replan-proposals", 503)]
    public async Task AdminPlanning_RequiresAuthentication_AndPreservesUpstreamErrors(string suffix, int status)
    {
        var path = "/api/v1/admin/item-transport/routes/" + suffix;
        const string payload = """{"localDate":"2026-11-02","routeIds":["a","b"]}""";
        string? received = null;
        await using var upstream = CreateApp();
        upstream.MapPost(path, async context =>
        {
            received = await new StreamReader(context.Request.Body).ReadToEndAsync(context.RequestAborted);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            context.Response.Headers.RetryAfter = "2";
            await context.Response.WriteAsync("""{"code":"route-review"}""", context.RequestAborted);
        });
        await upstream.StartAsync();
        await using var proxy = CreateProxy(Address(upstream)); await proxy.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(Address(proxy)) };
        using var anonymous = await client.PostAsync(path, new StringContent(payload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); Assert.Null(received);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "fixture");
        using var response = await client.PostAsync(path, new StringContent(payload, Encoding.UTF8, "application/json"));
        Assert.Equal(status, (int)response.StatusCode); Assert.Equal(payload, received);
        Assert.Contains("route-review", await response.Content.ReadAsStringAsync());
        Assert.Equal("2", response.Headers.RetryAfter?.ToString());
    }
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        return builder.Build();
    }

    private static WebApplication CreateProxy(string upstream)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
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
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(Request.Headers.Authorization == "Bearer fixture"
            ? AuthenticateResult.Success(new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", "fixture")], "test")), "test"))
            : AuthenticateResult.NoResult());
    }
}
