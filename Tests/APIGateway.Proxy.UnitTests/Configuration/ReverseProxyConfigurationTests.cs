using System.Text.Json;
using Xunit;

namespace APIGateway.Proxy.UnitTests.Configuration;

public sealed class ReverseProxyConfigurationTests
{
    [Fact]
    public void Appsettings_MapsQuoteRoutesAnonymouslyToTranzrCluster()
    {
        using var document = LoadAppsettings();
        var routes = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes");

        AssertRoute(routes, "quote-route-v2", "/api/v2/quote/{**catch-all}", "tranzr-cluster", "anonymous");
        AssertRoute(routes, "quote-routes-v2", "/api/v2/quotes/{**catch-all}", "tranzr-cluster", "anonymous");
    }

    [Fact]
    public void Appsettings_PreservesAuthenticatedRoutes()
    {
        using var document = LoadAppsettings();
        var routes = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes");

        var expectedAuthenticatedRoutes = new Dictionary<string, string>
        {
            ["role-route"] = "/api/v1/auth/role",
            ["business-auth-context-route"] = "/api/v1/Auth/context",
            ["business-auth-accept-invitation-route"] = "/api/v1/Auth/accept-invitation",
            ["business-auth-profile-route"] = "/api/v1/Auth/profile",
            ["additional-prices-route"] = "/api/v1/additionalprices/{**catch-all}",
            ["legal-authenticated-route"] = "/api/v1/legal/{**catch-all}",
            ["servicefeatures-route"] = "/api/v1/servicefeatures/{**catch-all}",
            ["customer-jobs-route"] = "/api/v1/customer-jobs/{**catch-all}",
            ["business-accounts-route"] = "/api/v1/BusinessAccounts/{**catch-all}",
            ["business-users-route"] = "/api/v1/BusinessUsers/{**catch-all}",
            ["admin-api-route"] = "/api/v1/admin/{**catch-all}",
            ["business-audit-events-route"] = "/api/v1/business-audit-events/{**catch-all}",
            ["business-dashboard-route"] = "/api/v1/BusinessDashboard/{**catch-all}",
            ["business-bookings-route"] = "/api/v1/BusinessBookings/{**catch-all}",
            ["business-locations-route"] = "/api/v1/BusinessLocations/{**catch-all}",
            ["business-contacts-route"] = "/api/v1/BusinessContacts/{**catch-all}",
            ["business-invoices-route"] = "/api/v1/BusinessInvoices/{**catch-all}",
            ["business-statements-route"] = "/api/v1/BusinessStatements/{**catch-all}",
            ["job-templates-route"] = "/api/v1/JobTemplates/{**catch-all}",
            ["tracking-route"] = "/api/v1/Tracking/{**catch-all}",
            ["business-quote-sessions-route"] = "/api/v1/BusinessQuoteSessions/{**catch-all}",
            ["business-checkout-route"] = "/api/v1/BusinessCheckout/{**catch-all}",
            ["driver-jobs-route"] = "/api/v1/driver-jobs/{**catch-all}",
            ["drivers-route"] = "/api/v1/drivers/{**catch-all}",
            ["inventory-import-route"] = "/api/v1/inventory",
        };

        foreach (var (routeId, path) in expectedAuthenticatedRoutes)
        {
            AssertRoute(routes, routeId, path, "tranzr-cluster", "AuthenticatedPolicy");
        }
    }

    private static void AssertRoute(
        JsonElement routes,
        string routeId,
        string expectedPath,
        string expectedCluster,
        string expectedPolicy)
    {
        var route = routes.GetProperty(routeId);

        Assert.Equal(expectedCluster, route.GetProperty("ClusterId").GetString());
        Assert.Equal(expectedPath, route.GetProperty("Match").GetProperty("Path").GetString());
        Assert.Equal(expectedPolicy, route.GetProperty("AuthorizationPolicy").GetString());
    }

    private static JsonDocument LoadAppsettings()
    {
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
    }
}
