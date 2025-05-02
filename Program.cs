using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LoginSystem.Data;
using LoginSystem.Models;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký dịch vụ Razor Pages và DbContext
builder.Services.AddRazorPages();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cấu hình Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Đăng ký CustomClaimsPrincipalFactory
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, CustomClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "TechShareAuth";
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.Events.OnSigningOut = async context =>
    {
        context.Response.Cookies.Delete("TechShareAuth");
        await Task.CompletedTask;
    };
});

var app = builder.Build();

// Cấu hình middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Tạo roles và tài khoản SuperAdmin
await SeedDataAsync(app.Services);

app.Run();

// Hàm khởi tạo role và SuperAdmin
static async Task SeedDataAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roleNames = { "User", "Admin", "SuperAdmin" };

    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    var superAdminEmail = "superadmin@example.com";
    var superAdminUser = await userManager.FindByEmailAsync(superAdminEmail);

    if (superAdminUser == null)
    {
        superAdminUser = new ApplicationUser
        {
            UserName = "superadmin",
            Email = superAdminEmail,
            DisplayName = "Super Admin",
            AvatarUrl = "/images/default-avatar.png"
        };

        var result = await userManager.CreateAsync(superAdminUser, "SuperAdmin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(superAdminUser, "SuperAdmin");
        }
        else
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"Error creating SuperAdmin: {error.Description}");
            }
        }
    }
}