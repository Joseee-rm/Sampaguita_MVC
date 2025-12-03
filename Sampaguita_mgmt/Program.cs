using Microsoft.AspNetCore.Authentication.Cookies;
using SeniorManagement.Helpers;
using SeniorManagement.Hubs;
using SeniorManagement.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
// Remove .AddRazorRuntimeCompilation() if you don't have the package installed
// If you want it, install: Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation
// Then uncomment: .AddRazorRuntimeCompilation()

// Add SignalR (for real-time activity updates)
builder.Services.AddSignalR();

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // Increased to 8 hours
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8); // Match session timeout
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

// Add authorization
builder.Services.AddAuthorization();

// Register your services - IMPORTANT: Register ActivityLogController here
builder.Services.AddScoped<DatabaseHelper>();
builder.Services.AddScoped<AuthHelper>();
builder.Services.AddScoped<ActivityHelper>();
builder.Services.AddScoped<SeniorManagement.Controllers.ActivityLogController>(); // Add this line

// Add HTTP context accessor - Make sure this is before other services that need it
builder.Services.AddHttpContextAccessor();

// Add logging - Remove this line as it's already added by default
// builder.Services.AddLogging(); // This is redundant

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // Only use HSTS in production
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Add session middleware - MUST come before UseAuthentication
app.UseSession();

// Add authentication & authorization
app.UseAuthentication();
app.UseAuthorization();

// Custom middleware to ensure session is available for logging
app.Use(async (context, next) =>
{
    // Ensure session is available before processing
    await context.Session.LoadAsync();
    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}"); // Changed to start at Login page

// Add SignalR hub endpoint
app.MapHub<ActivityHub>("/activityHub");

// Add a test endpoint for debugging
app.MapGet("/test-logging", async (context) =>
{
    var serviceProvider = context.RequestServices;
    try
    {
        // Test activity logging
        var activityHelper = serviceProvider.GetRequiredService<ActivityHelper>();
        await activityHelper.LogActivityAsync("Test", "Testing logging system from Program.cs");

        await context.Response.WriteAsync("Test logging completed successfully!");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
    }
});

// Add another test endpoint for ActivityLogController
app.MapGet("/test-activity-controller", async (context) =>
{
    var serviceProvider = context.RequestServices;
    try
    {
        using (var scope = serviceProvider.CreateScope())
        {
            var controller = scope.ServiceProvider.GetRequiredService<SeniorManagement.Controllers.ActivityLogController>();
            await controller.LogActivityAsync("Test Controller", "Testing ActivityLogController directly");
        }

        await context.Response.WriteAsync("ActivityLogController test completed!");
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
    }
});

app.Run();