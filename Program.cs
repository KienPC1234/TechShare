using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LoginSystem.Data;
using LoginSystem.Models;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using DotNetEnv;
using AspNetCoreRateLimit;

Env.Load();
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddMemoryCache();
builder.Services.AddInMemoryRateLimiting(); // Cài đặt bộ nhớ rate limiting
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Add SignalR services before builder.Build()
builder.Services.AddSignalR();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

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

// Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseIpRateLimiting();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();

// Map SignalR hub
app.MapHub<LoginSystem.Hubs.ChatHub>("/NotificationHub");
app.MapHub<LoginSystem.Hubs.MessHub>("/mesHub");

await SeedDataAsync(app.Services);

app.Run();

static async Task SeedDataAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Create roles
    string[] roleNames = { "User", "Admin", "SuperAdmin", "Delivery" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // Create SuperAdmin
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
    }

    // Create Admin
    var adminEmail = "admin@example.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = "admin",
            Email = adminEmail,
            DisplayName = "Admin User",
            AvatarUrl = "/images/default-avatar.png"
        };
        var result = await userManager.CreateAsync(adminUser, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    // Create Delivery
    var deliveryEmail = "delivery@example.com";
    var deliveryUser = await userManager.FindByEmailAsync(deliveryEmail);
    if (deliveryUser == null)
    {
        deliveryUser = new ApplicationUser
        {
            UserName = "delivery",
            Email = deliveryEmail,
            DisplayName = "Delivery User",
            AvatarUrl = "/images/default-avatar.png"
        };
        var result = await userManager.CreateAsync(deliveryUser, "Delivery123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(deliveryUser, "Delivery");
        }
    }

    // Create organization
    if (!dbContext.Organizations.Any())
    {
        var org = new Organization
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Cộng Đồng Công Nghệ Việt Nam",
            Slug = "cong-dong-cong-nghe-viet-nam",
            AvatarUrl = "/images/default-org-avatar.png",
            Terms = "Chấp nhận chia sẻ kiến thức và tôn trọng lẫn nhau.",
            IsPrivate = false,
            Description = "<p>Tổ chức dành cho những người yêu công nghệ tại Việt Nam.</p>",
            CreatorId = adminUser.Id
        };
        dbContext.Organizations.Add(org);
        dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            Id = Guid.NewGuid().ToString(),
            OrganizationId = org.Id,
            UserId = adminUser.Id,
            Role = "Admin"
        });
        await dbContext.SaveChangesAsync();
    }

    // Create categories
    if (!dbContext.ItemCategories.Any())
    {
        var categories = new[]
        {
            new ItemCategory { Id = Guid.NewGuid().ToString(), Name = "Đồ điện tử", Description = "Thiết bị điện tử và phụ kiện" },
            new ItemCategory { Id = Guid.NewGuid().ToString(), Name = "Công cụ", Description = "Dụng cụ sửa chữa và làm việc" },
            new ItemCategory { Id = Guid.NewGuid().ToString(), Name = "Sách", Description = "Sách và tài liệu học tập" },
            new ItemCategory { Id = Guid.NewGuid().ToString(), Name = "Khác", Description = "Các mặt hàng khác" }
        };
        dbContext.ItemCategories.AddRange(categories);
        await dbContext.SaveChangesAsync();
    }
}