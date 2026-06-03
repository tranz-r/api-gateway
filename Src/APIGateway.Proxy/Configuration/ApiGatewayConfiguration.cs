using APIGateway.Proxy.Auth;
using APIGateway.Proxy.Auth.Requirements.PaymentRead;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
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

    internal static void RegisterOpenTelemetry(this IServiceCollection _, WebApplicationBuilder builder)
    {
        const string serviceName = "tranzr-api-gateway";

        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(
        [
            new TraceContextPropagator(),
            new BaggagePropagator()
        ]));

        var openTelemetry = builder.Services.AddOpenTelemetry();
        openTelemetry.ConfigureResource(resource =>
            resource.AddService(serviceName));

        openTelemetry.WithTracing(tracing =>
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("Wolverine"));

        openTelemetry.WithMetrics(metrics =>
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("Wolverine*"));

        if (IsOtlpExportEnabled(builder.Configuration))
        {
            openTelemetry.UseOtlpExporter();
        }
    }

    internal static void RegisterAuthentication(this IServiceCollection _, WebApplicationBuilder builder)
    {
        var projectId = builder.Configuration["SUPER_BASE_PROJECT_ID"];
        var issuer = $"https://{projectId}.supabase.co/auth/v1";
        const string audience = "authenticated";

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.UseSecurityTokenValidators = true;
                options.RefreshOnIssuerKeyNotFound = true;
                options.Authority = issuer;
                options.MetadataAddress = $"{issuer}/.well-known/openid-configuration";
                options.RequireHttpsMetadata = true;
                options.IncludeErrorDetails = true;
                options.MapInboundClaims = false;
                options.Audience = audience;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };

                // options.Events = new JwtBearerEvents
                // {
                //     OnAuthenticationFailed = context =>
                //     {
                //         Console.WriteLine($"JWT failed: {context.Exception}");
                //         return Task.CompletedTask;
                //     },
                //     OnTokenValidated = context =>
                //     {
                //         var sub = context.Principal?.FindFirst("sub")?.Value;
                //         Console.WriteLine($"JWT ok for sub={sub}");
                //         return Task.CompletedTask;
                //     }
                // };
            });
    }

    private static bool IsOtlpExportEnabled(IConfiguration configuration)
    {
        if (string.Equals(
                configuration["OTEL_SDK_DISABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])
               || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));
    }
}
