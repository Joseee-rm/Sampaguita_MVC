using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
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
app.UseStaticFiles();

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

// Create logs directory on startup
var webRootPath = app.Environment.WebRootPath;
var logsDir = Path.Combine(webRootPath, "logs", "contributions");
if (!Directory.Exists(logsDir))
{
    Directory.CreateDirectory(logsDir);
    Console.WriteLine($"Created logs directory: {logsDir}");
}

// Map controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Map Dues controller routes
app.MapControllerRoute(
    name: "dues",
    pattern: "Dues/{action=Index}/{id?}",
    defaults: new { controller = "Dues" });

// Add SignalR hub endpoint
app.MapHub<ActivityHub>("/activityHub");

// Test endpoint
app.MapGet("/test", async (context) =>
{
    await context.Response.WriteAsync("Senior Management System is running!");
});

// Startup message
Console.WriteLine("Senior Management System Started Successfully!");
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");

app.Run();