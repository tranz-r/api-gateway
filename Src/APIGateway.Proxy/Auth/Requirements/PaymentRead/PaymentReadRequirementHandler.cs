using Microsoft.AspNetCore.Authorization;

namespace APIGateway.Proxy.Auth.Requirements.PaymentRead;

internal class PaymentReadRequirementHandler(ILogger<PaymentReadRequirementHandler> logger) : AuthorizationHandler<PaymentReadRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PaymentReadRequirement requirement)
    {
        var scopeClaim = context.User.FindFirst(c => c.Type == "scope");

        if (scopeClaim is not null)
        {
            var scopes = scopeClaim.Value.Split(' ', ',', '[', ']');
            
            if(scopes.Contains(requirement.Scope))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
            
            logger.LogWarning("User does not have the required scope: {Scope}", requirement.Scope);
            context.Fail();
            return Task.CompletedTask;
        }
        
        logger.LogWarning("User does not have the required scope claim");
        context.Fail();
        return Task.CompletedTask;
    }
}