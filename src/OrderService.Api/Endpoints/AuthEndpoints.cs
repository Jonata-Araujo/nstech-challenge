using OrderService.Api.Contracts;
using OrderService.Api.Infrastructure;
using OrderService.Application.Auth;

namespace OrderService.Api.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/token", async (TokenRequest request, IssueTokenHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(new IssueTokenCommand(request.Username, request.Password), ct);
                return result.ToHttpResult(Results.Ok);
            })
            .AllowAnonymous()
            .WithTags("Auth")
            .WithName("IssueToken")
            .WithSummary("Issues a JWT for a demo user")
            .Produces<TokenDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
