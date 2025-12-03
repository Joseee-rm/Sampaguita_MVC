using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SeniorManagement.Controllers;

namespace SeniorManagement.Helpers
{
    public class ActivityHelper
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ActivityHelper> _logger;

        public ActivityHelper(IServiceProvider serviceProvider, ILogger<ActivityHelper> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task LogActivityAsync(string action, string details)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var controller = scope.ServiceProvider.GetRequiredService<ActivityLogController>();
                    await controller.LogActivityAsync(action, details);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging activity: {Action}", action);
            }
        }

        // Static method for easy access from anywhere
        public static async Task LogActivity(IServiceProvider serviceProvider, string action, string details)
        {
            try
            {
                using (var scope = serviceProvider.CreateScope())
                {
                    var activityHelper = scope.ServiceProvider.GetRequiredService<ActivityHelper>();
                    await activityHelper.LogActivityAsync(action, details);
                }
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ActivityHelper>>();
                logger.LogError(ex, "Error in static LogActivity");
            }
        }

        // Quick logging methods for common activities
        public async Task LogUserLoginAsync(string username)
        {
            await LogActivityAsync("Login", $"User {username} logged in");
        }

        public async Task LogUserLogoutAsync(string username)
        {
            await LogActivityAsync("Logout", $"User {username} logged out");
        }

        public async Task LogUserActionAsync(string action, string target, string details = "")
        {
            var message = $"{action}: {target}" + (string.IsNullOrEmpty(details) ? "" : $" - {details}");
            await LogActivityAsync(action, message);
        }

        public async Task LogErrorAsync(string errorMessage, string context = "")
        {
            var details = string.IsNullOrEmpty(context) ? errorMessage : $"{context}: {errorMessage}";
            await LogActivityAsync("Error", details);
        }

        public async Task LogSystemEventAsync(string eventName, string details)
        {
            await LogActivityAsync(eventName, details);
        }
    }
}