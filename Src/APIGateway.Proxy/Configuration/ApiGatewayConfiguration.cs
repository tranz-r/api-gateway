using System.Security.Claims;
using APIGateway.Proxy.Auth;
using APIGateway.Proxy.Auth.Requirements.PaymentRead;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace APIGateway.Proxy.Configuration;

internal static class ApiGatewayConfiguration
{
    internal static void RegisterAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicy.AuthenticatedPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
            });
    }
    
    internal static void RegisterAuthorizationHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PaymentReadRequirementHandler>();
    }
    
    internal static void RegisterOpenTelemetry(this IServiceCollection services, WebApplicationBuilder builder)
    {
        // Add OpenTelemetry
        // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel

        builder.Logging.AddOpenTelemetry(options =>
        {
            options
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService("tranzr-api-gateway")
                )
                .AddConsoleExporter(); // Optional, for local debugging
        });

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("tranzr-api-gateway"))
                .AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri("http://otel-collector:4317"); // Update as needed
                })
            )
            .WithMetrics(metricsBuilder => metricsBuilder
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter()
                .AddHttpClientInstrumentation()
                // .AddPrometheusExporter()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("tranzr-api-gateway"))
                .AddOtlpExporter(opt => { opt.Endpoint = new Uri("http://otel-collector:4317"); })
            );
    }
    
    internal static void RegisterAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = "https://iam.labgrid.net/realms/tranzr";
            options.MetadataAddress = "https://iam.labgrid.net/realms/tranzr/protocol/openid-connect/certs";
            options.UseSecurityTokenValidators = true;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidIssuers = [
                    "https://iam.labgrid.net/realms/tranzr"
                ],
                ValidateAudience = true,
                ValidAudiences = [
                    "tranzr-api"
                ],
                ValidateLifetime = true
            };
        });
    }
}