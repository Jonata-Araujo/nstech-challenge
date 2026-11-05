using OrderService.Application.Abstractions;
using OrderService.Application.Auth;
using OrderService.Infrastructure.Auth;

namespace OrderService.Api.Infrastructure;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? Username => Principal?.FindFirst(AppClaims.Subject)?.Value;

    public Guid? CustomerId =>
        Guid.TryParse(Principal?.FindFirst(AppClaims.CustomerId)?.Value, out var id) ? id : null;

    public bool IsAdmin => Principal?.IsInRole(Roles.Admin) ?? false;
}
