using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace CseReviewer;
public record CurrentUser(string Id, string Name, bool IsAdmin, bool IsSuperAdmin);
public class CurrentUserAccessor(AuthenticationStateProvider authState) {
    public async Task<CurrentUser> Get() {
        var state = await authState.GetAuthenticationStateAsync();
        var user = state.User;
        var isSuperAdmin = user.IsInRole("SuperAdmin");
        return new CurrentUser(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", user.Identity?.Name ?? "", isSuperAdmin || user.IsInRole("Admin"), isSuperAdmin);
    }
}
