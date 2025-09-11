using APIGateway.Proxy.Configuration;
using APIGateway.Proxy.Constants;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.RegisterAuthorizationHandlers();
    builder.Services.AddHealthChecks();

    builder.Services.AddHttpLogging(o => o.CombineLogs = true);

    builder.Services.RegisterAuthentication(builder);

    // Add services to the container.
    builder.Services.AddCors(options =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        options.AddPolicy(Cors.TranzrAPIGatewayCorsPolicy,
            policy =>
            {
                policy.WithOrigins(allowedOrigins ?? [])
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
    });
    builder.Services.RegisterAuthorizationPolicies();
    
    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    // builder.Services.RegisterOpenTelemetry(builder);

    var app = builder.Build();

    app.UseHttpLogging();
    app.MapHealthChecks("/healthz");
    app.MapHealthChecks("/ready");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapReverseProxy().RequireAuthorization();

    app.Run();
}
catch (Exception)
{
    Log.CloseAndFlush();
}