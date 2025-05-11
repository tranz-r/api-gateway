using Microsoft.AspNetCore.Authorization;

namespace APIGateway.Proxy.Auth.Requirements.PaymentRead;

internal class PaymentReadRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}