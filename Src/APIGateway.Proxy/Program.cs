using APIGateway.Proxy.Configuration;
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

    builder.Services.RegisterAuthentication();

    // Add services to the container.
    builder.Services.RegisterAuthorizationPolicies();
    
    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    builder.Services.RegisterOpenTelemetry(builder);

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