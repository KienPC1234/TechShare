#nullable disable
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using LoginSystem.Models;
using System.Threading.Tasks;

public class CustomClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public CustomClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
        _userManager = userManager;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var username = user.DisplayName ?? user.UserName ?? user.Email ?? "Unknown";
        identity.AddClaim(new Claim("Username", username));
        var avatarUrl = user.AvatarUrl ?? "/images/default-avatar.png";
        identity.AddClaim(new Claim("AvatarUrl", avatarUrl));
        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
        if (user.OrganizationId != null)
        {
            identity.AddClaim(new Claim("OrganizationId", user.OrganizationId));
        }
        return identity;
    }
}