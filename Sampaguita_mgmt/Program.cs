using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using SeniorManagement.Helpers;
using SeniorManagement.Hubs;
using SeniorManagement.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Razor Runtime Compilation for Development (requires NuGet package)
if (builder.Environment.IsDevelopment())
{
    // Make sure you have this NuGet package installed:
    // Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation
    builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
}

// Add SignalR (for real-time activity updates)
builder.Services.AddSignalR();

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Name = ".SeniorManagement.Session";
});

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Name = ".SeniorManagement.Auth";
    });

// Add authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Administrator"));

    // Add financial management policy if needed
    options.AddPolicy("FinancialManager", policy =>
        policy.RequireRole("Administrator", "FinancialManager", "Treasurer"));
});

// Register Database Helper
builder.Services.AddScoped<DatabaseHelper>();

// Register Auth Helper
builder.Services.AddScoped<AuthHelper>();

// Register Activity Helper  
builder.Services.AddScoped<ActivityHelper>();

// Register Monthly Contributions Repository
builder.Services.AddScoped<IMonthlyContributionRepository, MonthlyContributionRepository>();

// Add HTTP context accessor
builder.Services.AddHttpContextAccessor();

// Add logging configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// Configure file upload size limits (for profile pictures)
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 20971520; // 20MB limit
});

// Configure Kestrel for file uploads
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 20971520; // 20MB
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Configure static files for profile pictures uploads
app.UseStaticFiles(); // This serves wwwroot

// ==============================================
// 2. Directory Structure Implementation
// ==============================================
var contentRootPath = app.Environment.ContentRootPath;

// Create base uploads directory
var uploadsPath = Path.Combine(contentRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
    Console.WriteLine($"Created uploads directory: {uploadsPath}");
}

// Create profiles directory for profile pictures
var profilesDir = Path.Combine(uploadsPath, "profiles");
if (!Directory.Exists(profilesDir))
{
    Directory.CreateDirectory(profilesDir);
    Console.WriteLine($"Created profiles directory: {profilesDir}");
}

// Create logs directory for contribution logs
var logsDir = Path.Combine(contentRootPath, "logs", "contributions");
if (!Directory.Exists(logsDir))
{
    Directory.CreateDirectory(logsDir);
    Console.WriteLine($"Created logs directory: {logsDir}");
}

// Create exports directory for export files
var exportsDir = Path.Combine(contentRootPath, "exports");
if (!Directory.Exists(exportsDir))
{
    Directory.CreateDirectory(exportsDir);
    Console.WriteLine($"Created exports directory: {exportsDir}");
}

// Create temp directory for temporary files
var tempDir = Path.Combine(contentRootPath, "temp");
if (!Directory.Exists(tempDir))
{
    Directory.CreateDirectory(tempDir);
    Console.WriteLine($"Created temp directory: {tempDir}");
}

// Configure static file serving for uploads directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

// Configure static file serving for exports directory (if needed for downloads)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(exportsDir),
    RequestPath = "/exports",
    // Optional: Add security restrictions for exports
    ServeUnknownFileTypes = false
});

app.UseRouting();

// Add session middleware
app.UseSession();

// Add authentication & authorization
app.UseAuthentication();
app.UseAuthorization();

// Custom middleware to ensure session is available for logging
app.Use(async (context, next) =>
{
    await context.Session.LoadAsync();

    // Set security headers
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

    await next();
});

// Map controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Map Dues controller routes
app.MapControllerRoute(
    name: "dues",
    pattern: "Dues/{action=Index}/{id?}",
    defaults: new { controller = "Dues" });

// Map Senior controller routes
app.MapControllerRoute(
    name: "senior",
    pattern: "Senior/{action=Index}/{id?}",
    defaults: new { controller = "Senior" });

// Add SignalR hub endpoint
app.MapHub<ActivityHub>("/activityHub");

// Test endpoint
app.MapGet("/test", async (context) =>
{
    await context.Response.WriteAsync("Senior Management System is running!");
});

// Health check endpoint
app.MapGet("/health", async (context) =>
{
    await context.Response.WriteAsync("OK");
});

// Directory cleanup for temp files (optional - run on startup)
try
{
    // Clean up temp files older than 1 day
    var tempFiles = Directory.GetFiles(tempDir);
    foreach (var file in tempFiles)
    {
        var fileInfo = new FileInfo(file);
        if (fileInfo.CreationTime < DateTime.Now.AddDays(-1))
        {
            fileInfo.Delete();
            Console.WriteLine($"Cleaned up old temp file: {file}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error cleaning temp directory: {ex.Message}");
}

// Startup message with directory info
Console.WriteLine("==============================================");
Console.WriteLine("Senior Management System Started Successfully!");
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
Console.WriteLine("Directory Structure Created:");
Console.WriteLine($"  • Profiles: {profilesDir}");
Console.WriteLine($"  • Logs: {logsDir}");
Console.WriteLine($"  • Exports: {exportsDir}");
Console.WriteLine($"  • Temp: {tempDir}");
Console.WriteLine("==============================================");

app.Run();