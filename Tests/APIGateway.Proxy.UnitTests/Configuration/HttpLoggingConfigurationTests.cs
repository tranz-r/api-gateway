using APIGateway.Proxy.Configuration;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace APIGateway.Proxy.UnitTests.Configuration;

public sealed class HttpLoggingConfigurationTests
{
    [Fact]
    public void AddTranzrHttpLogging_ExcludesSensitiveQueryValues()
    {
        var services = new ServiceCollection();

        services.AddTranzrHttpLogging();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HttpLoggingOptions>>().Value;

        var expectedFields = HttpLoggingFields.RequestMethod
            | HttpLoggingFields.RequestPath
            | HttpLoggingFields.ResponseStatusCode
            | HttpLoggingFields.Duration;

        Assert.Equal(expectedFields, options.LoggingFields);
    }
}
