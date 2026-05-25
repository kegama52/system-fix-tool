using System.Security.Claims;

namespace TreasuryFixTool.Api.Services;

/// <summary>Extension helpers shared across controllers and middleware.</summary>
internal static class ContextExtensions
{
    /// <summary>
    /// Returns the current authenticated user's Identity user-id, or <c>null</c> when unauthenticated.
    /// </summary>
    public static string? GetUserId(this HttpContext ctx)
        => ctx.User?.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? ctx.User?.FindFirstValue("sub");
}
