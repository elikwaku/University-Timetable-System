using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.Core.Scheduling;
using UniversityTimetable.Infrastructure.Data;
using UniversityTimetable.Infrastructure.Identity;
using UniversityTimetable.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database Configuration (SQLite default for self-contained execution, fully compatible with SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=university_timetable.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// ASP.NET Core Identity Configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure Application Cookie paths
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Admin/Account/Login"; // Default login path
    options.AccessDeniedPath = "/Admin/Account/AccessDenied";
    options.Cookie.Name = "UniversityTimetableAuth";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// Dependency Injection for Core & Infrastructure Services
builder.Services.AddScoped<IAIExplainabilityService, AIExplainabilityService>();
builder.Services.AddScoped<ISchedulingEngine, AISchedulingEngine>();
builder.Services.AddScoped<StudentCsvImporter>();
builder.Services.AddScoped<TimetableExportService>();

var app = builder.Build();

// Seed Database automatically on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await DbSeeder.SeedAsync(context, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Route Configuration for Dual Portals (/admin and /student)
app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{controller=AdminDashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "student",
    pattern: "Student/{controller=StudentDashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
