using System.IO.Pipes;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        var projectId = builder.Configuration["SUPER_BASE_PROJECT_ID"];
        var issuer = $"https://{projectId}.supabase.co/auth/v1";
        var jwksUri = $"{issuer}/.well-known/jwks.json";

        // 🔑 Load keys at app startup
        var signingKeys =  LoadSupabaseRsaKeys(jwksUri).ConfigureAwait(false).GetAwaiter().GetResult();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.IncludeErrorDetails = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,

                    ValidateAudience = true,
                    ValidAudience = "authenticated",

                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = signingKeys,
                    ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
                };
            });
    }
    
    public static async Task<List<SecurityKey>> LoadSupabaseRsaKeys(string jwksUrl)
    {
        using var client = new HttpClient();
        var json = await client.GetStringAsync(jwksUrl);
        var doc = JsonDocument.Parse(json);
        var keys = new List<SecurityKey>();

        foreach (var jwk in doc.RootElement.GetProperty("keys").EnumerateArray())
        {
            if (jwk.GetProperty("kty").GetString() != "RSA") continue;

            var e = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("e").GetString());
            var n = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("n").GetString());

            var rsaParams = new RSAParameters
            {
                Exponent = e,
                Modulus = n
            };

            var key = new RsaSecurityKey(RSA.Create(rsaParams))
            {
                KeyId = jwk.GetProperty("kid").GetString()
            };

            keys.Add(key);
        }

        return keys;
    }
}