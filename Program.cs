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

// Thêm dịch vụ Session
builder.Services.AddDistributedMemoryCache(); // Cung cấp bộ nhớ cache cho Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Thời gian timeout của Session
    options.Cookie.HttpOnly = true; // Cookie chỉ có thể truy cập qua HTTP
    options.Cookie.IsEssential = true; // Cookie cần thiết, không cần consent
});

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

// Thêm middleware Session trước Authentication và Authorization
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Tạo roles, tài khoản SuperAdmin và dữ liệu mẫu
await SeedDataAsync(app.Services);

app.Run();

// Hàm khởi tạo role, SuperAdmin và dữ liệu mẫu
static async Task SeedDataAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Tạo roles
    string[] roleNames = { "User", "Admin", "SuperAdmin" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // Tạo tài khoản SuperAdmin
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

    // Tạo tài khoản Admin mẫu
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

    // Tạo tổ chức mẫu nếu chưa có
    if (!dbContext.Organizations.Any())
    {
        var org = new Organization
        {
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
            OrganizationId = org.Id,
            UserId = adminUser.Id,
            Role = "Admin"
        });
        adminUser.OrganizationId = org.Id;
        await dbContext.SaveChangesAsync();
        await userManager.UpdateAsync(adminUser);
    }
}