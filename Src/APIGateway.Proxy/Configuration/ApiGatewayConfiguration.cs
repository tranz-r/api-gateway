using System.Security.Claims;
using System.Text;
using APIGateway.Proxy.Auth;
using APIGateway.Proxy.Auth.Requirements.PaymentRead;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
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
    
    internal static void RegisterAuthentication(this IServiceCollection services, WebApplicationBuilder builder)
    {
        var secret = builder.Configuration["SUPER_BASE_JWT_SECRET_KEY"];
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = $"https://{builder.Configuration["SUPER_BASE_PROJECT_ID"]}.supabase.co/auth/v1";
            options.Audience = "authenticated";
            options.IncludeErrorDetails = true; // Helpful for debugging

            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = options.Authority,

                ValidateAudience = true,
                ValidAudience = options.Audience,
          
                ValidateLifetime = true,
                //ClockSkew = TimeSpan.FromMinutes(2)
            };
        });;
    }
}