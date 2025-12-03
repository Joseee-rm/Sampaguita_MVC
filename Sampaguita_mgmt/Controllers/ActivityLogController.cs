using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MySql.Data.MySqlClient;
using SeniorManagement.Helpers;
using SeniorManagement.Hubs;
using SeniorManagement.Models;

namespace SeniorManagement.Controllers
{
    [Authorize]
    public class ActivityLogController : BaseController
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly IHubContext<ActivityHub> _hubContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ActivityLogController> _logger;

        public ActivityLogController(
            DatabaseHelper dbHelper,
            IHubContext<ActivityHub> hubContext,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ActivityLogController> logger)
        {
            _dbHelper = dbHelper;
            _hubContext = hubContext;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetActivityLogs(int page = 1, int pageSize = 50,
            string dateFrom = "", string dateTo = "",
            string userRole = "", string action = "", string search = "")
        {
            try
            {
                var activities = await GetActivityLogsPaginated(page, pageSize, dateFrom, dateTo, userRole, action, search);
                var totalCount = await GetTotalActivityCount(dateFrom, dateTo, userRole, action, search);

                return Json(new
                {
                    success = true,
                    data = activities,
                    total = totalCount,
                    page = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading activity logs");
                return Json(new
                {
                    success = false,
                    message = "Error loading activity logs"
                });
            }
        }

        private async Task<List<ActivityLog>> GetActivityLogsPaginated(int page, int pageSize,
            string dateFrom, string dateTo, string userRole, string action, string search)
        {
            var activities = new List<ActivityLog>();
            var offset = (page - 1) * pageSize;

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    var query = @"SELECT Id, UserName, UserRole, Action, Details, 
                                IpAddress, CreatedAt
                                FROM activity_logs 
                                WHERE 1=1";

                    var conditions = new List<string>();
                    var parameters = new Dictionary<string, object>();

                    if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var fromDate))
                    {
                        conditions.Add("DATE(CreatedAt) >= @DateFrom");
                        parameters.Add("@DateFrom", fromDate.ToString("yyyy-MM-dd"));
                    }

                    if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var toDate))
                    {
                        conditions.Add("DATE(CreatedAt) <= @DateTo");
                        parameters.Add("@DateTo", toDate.ToString("yyyy-MM-dd"));
                    }

                    if (!string.IsNullOrEmpty(userRole))
                    {
                        conditions.Add("UserRole = @UserRole");
                        parameters.Add("@UserRole", userRole);
                    }

                    if (!string.IsNullOrEmpty(action))
                    {
                        conditions.Add("Action = @Action");
                        parameters.Add("@Action", action);
                    }

                    if (!string.IsNullOrEmpty(search))
                    {
                        conditions.Add(@"(UserName LIKE @Search OR 
                                        Details LIKE @Search OR 
                                        Action LIKE @Search OR 
                                        IpAddress LIKE @Search)");
                        parameters.Add("@Search", $"%{search}%");
                    }

                    if (conditions.Count > 0)
                    {
                        query += " AND " + string.Join(" AND ", conditions);
                    }

                    query += " ORDER BY CreatedAt DESC LIMIT @PageSize OFFSET @Offset";

                    parameters.Add("@PageSize", pageSize);
                    parameters.Add("@Offset", offset);

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value);
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                activities.Add(new ActivityLog
                                {
                                    Id = reader.GetInt32("Id"),
                                    UserName = reader.GetString("UserName"),
                                    UserRole = reader.GetString("UserRole"),
                                    Action = reader.GetString("Action"),
                                    Details = reader.IsDBNull(reader.GetOrdinal("Details")) ? "" : reader.GetString("Details"),
                                    IpAddress = reader.IsDBNull(reader.GetOrdinal("IpAddress")) ? "" : reader.GetString("IpAddress"),
                                    CreatedAt = reader.GetDateTime("CreatedAt")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting activity logs");
            }

            return activities;
        }

        private async Task<int> GetTotalActivityCount(string dateFrom, string dateTo,
            string userRole, string action, string search)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    var query = "SELECT COUNT(*) FROM activity_logs WHERE 1=1";
                    var conditions = new List<string>();
                    var parameters = new Dictionary<string, object>();

                    if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var fromDate))
                    {
                        conditions.Add("DATE(CreatedAt) >= @DateFrom");
                        parameters.Add("@DateFrom", fromDate.ToString("yyyy-MM-dd"));
                    }

                    if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var toDate))
                    {
                        conditions.Add("DATE(CreatedAt) <= @DateTo");
                        parameters.Add("@DateTo", toDate.ToString("yyyy-MM-dd"));
                    }

                    if (!string.IsNullOrEmpty(userRole))
                    {
                        conditions.Add("UserRole = @UserRole");
                        parameters.Add("@UserRole", userRole);
                    }

                    if (!string.IsNullOrEmpty(action))
                    {
                        conditions.Add("Action = @Action");
                        parameters.Add("@Action", action);
                    }

                    if (!string.IsNullOrEmpty(search))
                    {
                        conditions.Add(@"(UserName LIKE @Search OR 
                                        Details LIKE @Search OR 
                                        Action LIKE @Search OR 
                                        IpAddress LIKE @Search)");
                        parameters.Add("@Search", $"%{search}%");
                    }

                    if (conditions.Count > 0)
                    {
                        query += " AND " + string.Join(" AND ", conditions);
                    }

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value);
                        }

                        var result = await cmd.ExecuteScalarAsync();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        [HttpPost]
        public async Task<IActionResult> ClearLogs()
        {
            try
            {
                // Check if user is admin
                if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
                {
                    return Json(new { success = false, message = "Access denied. Admin privileges required." });
                }

                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    // Keep last 1000 records
                    string query = @"DELETE FROM activity_logs 
                                   WHERE Id NOT IN (
                                       SELECT Id FROM (
                                           SELECT Id FROM activity_logs 
                                           ORDER BY CreatedAt DESC 
                                           LIMIT 1000
                                       ) AS temp
                                   )";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        // Log the clearance action
                        await LogActivityAsync("Clear Logs", $"Cleared {rowsAffected} activity logs");

                        // Send notification
                        if (_hubContext != null)
                        {
                            await _hubContext.Clients.All.SendAsync("LogsCleared", rowsAffected);
                        }

                        return Json(new
                        {
                            success = true,
                            message = $"Cleared {rowsAffected} activity logs. Kept last 1000 records."
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing logs");
                return Json(new { success = false, message = "Error clearing logs." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportLogs()
        {
            try
            {
                var activities = await GetAllActivityLogsForExport();

                // Generate CSV content
                var csv = "ID,UserName,UserRole,Action,Details,IP Address,CreatedAt\n";
                foreach (var log in activities)
                {
                    csv += $"\"{log.Id}\",\"{log.UserName}\",\"{log.UserRole}\",\"{log.Action}\",\"{log.Details.Replace("\"", "\"\"")}\",\"{log.IpAddress}\",\"{log.CreatedAt:yyyy-MM-dd HH:mm:ss}\"\n";
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);

                // Log export action
                await LogActivityAsync("Export", "Exported activity logs to CSV");

                return File(bytes, "text/csv", $"activity_logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting logs");
                return Json(new { success = false, message = "Error exporting logs." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var stats = await GetActivityStatistics();
                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics");
                return Json(new { success = false, message = "Error loading statistics." });
            }
        }

        private async Task<object> GetActivityStatistics()
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    var stats = new
                    {
                        TotalActivities = await GetCountAsync(connection, "SELECT COUNT(*) FROM activity_logs"),
                        TodayActivities = await GetCountAsync(connection, "SELECT COUNT(*) FROM activity_logs WHERE DATE(CreatedAt) = CURDATE()"),
                        ThisWeekActivities = await GetCountAsync(connection, "SELECT COUNT(*) FROM activity_logs WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY)"),
                        MostActiveUser = await GetMostActiveUserAsync(connection),
                        ActivitiesByRole = await GetActivitiesByRoleAsync(connection),
                        RecentActivityTypes = await GetRecentActivityTypesAsync(connection)
                    };

                    return stats;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting activity statistics");
                return new
                {
                    TotalActivities = 0,
                    TodayActivities = 0,
                    ThisWeekActivities = 0,
                    MostActiveUser = "N/A",
                    ActivitiesByRole = new { },
                    RecentActivityTypes = new { }
                };
            }
        }

        private async Task<int> GetCountAsync(MySqlConnection connection, string query)
        {
            using (var cmd = new MySqlCommand(query, connection))
            {
                var result = await cmd.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 0;
            }
        }

        private async Task<string> GetMostActiveUserAsync(MySqlConnection connection)
        {
            string query = @"SELECT UserName, COUNT(*) as ActivityCount 
                           FROM activity_logs 
                           WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY)
                           GROUP BY UserName 
                           ORDER BY ActivityCount DESC 
                           LIMIT 1";

            using (var cmd = new MySqlCommand(query, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return reader.GetString("UserName");
                }
            }
            return "N/A";
        }

        private async Task<Dictionary<string, int>> GetActivitiesByRoleAsync(MySqlConnection connection)
        {
            var dict = new Dictionary<string, int>();
            string query = @"SELECT UserRole, COUNT(*) as Count 
                           FROM activity_logs 
                           WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 30 DAY)
                           GROUP BY UserRole";

            using (var cmd = new MySqlCommand(query, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    dict[reader.GetString("UserRole")] = reader.GetInt32("Count");
                }
            }
            return dict;
        }

        private async Task<Dictionary<string, int>> GetRecentActivityTypesAsync(MySqlConnection connection)
        {
            var dict = new Dictionary<string, int>();
            string query = @"SELECT Action, COUNT(*) as Count 
                           FROM activity_logs 
                           WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY)
                           GROUP BY Action 
                           ORDER BY Count DESC 
                           LIMIT 10";

            using (var cmd = new MySqlCommand(query, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    dict[reader.GetString("Action")] = reader.GetInt32("Count");
                }
            }
            return dict;
        }


        private async Task<List<ActivityLog>> GetAllActivityLogsForExport()
        {
            var activities = new List<ActivityLog>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();
                    string query = @"SELECT Id, UserName, UserRole, Action, Details, 
                                   IpAddress, CreatedAt
                                   FROM activity_logs 
                                   ORDER BY CreatedAt DESC";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            activities.Add(new ActivityLog
                            {
                                Id = reader.GetInt32("Id"),
                                UserName = reader.GetString("UserName"),
                                UserRole = reader.GetString("UserRole"),
                                Action = reader.GetString("Action"),
                                Details = reader.IsDBNull(reader.GetOrdinal("Details")) ? "" : reader.GetString("Details"),
                                IpAddress = reader.IsDBNull(reader.GetOrdinal("IpAddress")) ? "" : reader.GetString("IpAddress"),
                                CreatedAt = reader.GetDateTime("CreatedAt")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting activity logs for export");
            }

            return activities;
        }

        // Method to log activities (can be called from anywhere) - FIXED VERSION
        public async Task LogActivityAsync(string action, string details)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var userName = httpContext?.Session?.GetString("UserName") ?? "System";
                var userRole = httpContext?.Session?.GetString("UserRole") ?? "System";
                var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";

                _logger.LogInformation("Logging activity: {Action} by {User}", action, userName);

                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    string query = @"INSERT INTO activity_logs 
                                   (UserName, UserRole, Action, Details, IpAddress, CreatedAt) 
                                   VALUES (@UserName, @UserRole, @Action, @Details, @IpAddress, @CreatedAt)";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserName", userName);
                        cmd.Parameters.AddWithValue("@UserRole", userRole);
                        cmd.Parameters.AddWithValue("@Action", action);
                        cmd.Parameters.AddWithValue("@Details", details ?? "");
                        cmd.Parameters.AddWithValue("@IpAddress", ipAddress);
                        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                        await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("Activity logged successfully: {Action}", action);
                    }
                }

                // Create activity log object for real-time update
                var newLog = new ActivityLog
                {
                    UserName = userName,
                    UserRole = userRole,
                    Action = action,
                    Details = details ?? "",
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.Now
                };

                // Notify all connected clients about the new activity
                if (_hubContext != null)
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveNewActivity", newLog);
                    _logger.LogInformation("SignalR notification sent for activity: {Action}", action);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging activity: {Action}", action);
                // Don't throw - we don't want logging failures to break the application
            }
        }

        // Static method for easy access from anywhere
        public static async Task LogActivity(IServiceProvider serviceProvider, string action, string details)
        {
            try
            {
                using (var scope = serviceProvider.CreateScope())
                {
                    var controller = scope.ServiceProvider.GetRequiredService<ActivityLogController>();
                    await controller.LogActivityAsync(action, details);
                }
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ActivityLogController>>();
                logger.LogError(ex, "Error in static LogActivity");
            }
        }
    }
}