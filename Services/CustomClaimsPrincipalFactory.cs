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
        // Gọi phương thức gốc để lấy các claims mặc định
        var identity = await base.GenerateClaimsAsync(user);

        // Thêm claim Username (ưu tiên DisplayName, sau đó UserName, cuối cùng Email)
        var username = user.DisplayName ?? user.UserName ?? user.Email ?? "Unknown";
        identity.AddClaim(new Claim("Username", username));

        // Thêm claim AvatarUrl
        var avatarUrl = user.AvatarUrl ?? "/images/default-avatar.png";
        identity.AddClaim(new Claim("AvatarUrl", avatarUrl));

        // Thêm claims cho tất cả vai trò của người dùng
        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return identity;
    }
}