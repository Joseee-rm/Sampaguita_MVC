using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using SeniorManagement.Helpers;
using SeniorManagement.Models;

namespace SeniorManagement.Controllers
{
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ActivityHelper _activityHelper;

        public HomeController(DatabaseHelper dbHelper, ActivityHelper activityHelper)
        {
            _dbHelper = dbHelper;
            _activityHelper = activityHelper;
        }

        public IActionResult Index()
        {
            try
            {
                ViewBag.Name = HttpContext.Session.GetString("UserName") ?? "User";
                ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "User";
                ViewBag.IsAdmin = ViewBag.UserRole == "Administrator";
                ViewBag.UserId = HttpContext.Session.GetString("UserId");

                // Get dashboard statistics
                var dashboardStats = GetDashboardStatistics();
                ViewBag.DashboardStats = dashboardStats;

                // Get recent activities
                var activities = GetRecentActivities();
                ViewBag.RecentActivities = activities;

                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Home/Index: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading dashboard data.";
                return View();
            }
        }

        [HttpGet]
        public JsonResult GetDashboardStats()
        {
            try
            {
                var stats = GetDashboardStatistics();
                return Json(new { success = true, stats = stats });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetRecentActivitiesAjax()
        {
            try
            {
                var activities = GetRecentActivities();
                return Json(new { success = true, activities = activities });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetNotifications()
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                var userRole = HttpContext.Session.GetString("UserRole") ?? "Staff";

                var notifications = GetUserNotifications(userId, userRole);
                var unreadCount = notifications.Count(n => !n.IsRead);

                return Json(new
                {
                    success = true,
                    notifications = notifications,
                    unreadCount = unreadCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult MarkNotificationAsRead([FromBody] NotificationRequest request)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = "UPDATE notifications SET IsRead = TRUE WHERE Id = @Id";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", request.Id);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult MarkAllNotificationsAsRead()
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = "UPDATE notifications SET IsRead = TRUE WHERE UserId = @UserId";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteNotification([FromBody] NotificationRequest request)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = "DELETE FROM notifications WHERE Id = @Id";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", request.Id);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetSystemStatus()
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    var status = new
                    {
                        Database = "Connected",
                        ServerTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Uptime = "24h",
                        MemoryUsage = "Normal",
                        ActiveConnections = GetCount(connection, "SELECT COUNT(*) FROM information_schema.processlist")
                    };

                    return Json(new { success = true, data = status });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult Profile()
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Index");
                }

                var user = GetUserProfile(userId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User profile not found.";
                    return RedirectToAction("Index");
                }

                return View(user);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading profile: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(UserProfile model)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Profile");
                }

                // Basic validation
                if (string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.Email))
                {
                    TempData["ErrorMessage"] = "Name and Email are required.";
                    return View("Profile", model);
                }

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Check if email already exists (excluding current user)
                    string checkQuery = "SELECT COUNT(*) FROM users WHERE Email = @Email AND Id != @Id";
                    using (var checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@Email", model.Email);
                        checkCmd.Parameters.AddWithValue("@Id", userId);
                        int emailCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (emailCount > 0)
                        {
                            TempData["ErrorMessage"] = "Email already exists. Please choose a different email.";
                            return View("Profile", model);
                        }
                    }

                    // Update profile
                    string updateQuery = @"UPDATE users 
                                         SET Name = @Name, 
                                             Email = @Email,
                                             Phone = @Phone,
                                             UpdatedAt = @UpdatedAt
                                         WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(updateQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@Name", model.Name);
                        cmd.Parameters.AddWithValue("@Email", model.Email);
                        cmd.Parameters.AddWithValue("@Phone", model.Phone ?? "");
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Id", userId);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Update session
                            HttpContext.Session.SetString("UserName", model.Name);

                            // Log the activity
                            await _activityHelper.LogActivityAsync(
                                "Update Profile",
                                $"Updated profile information"
                            );

                            TempData["SuccessMessage"] = "Profile updated successfully!";
                            return RedirectToAction("Profile");
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Error updating profile.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogActivityAsync("Error", $"Update Profile: {ex.Message}");
                TempData["ErrorMessage"] = $"Error updating profile: {ex.Message}";
            }

            return View("Profile", model);
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Profile");
                }

                // Validate passwords
                if (string.IsNullOrEmpty(model.CurrentPassword) ||
                    string.IsNullOrEmpty(model.NewPassword) ||
                    string.IsNullOrEmpty(model.ConfirmPassword))
                {
                    TempData["ErrorMessage"] = "All password fields are required.";
                    return RedirectToAction("Profile");
                }

                if (model.NewPassword != model.ConfirmPassword)
                {
                    TempData["ErrorMessage"] = "New password and confirmation do not match.";
                    return RedirectToAction("Profile");
                }

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Get current password
                    string getQuery = "SELECT Password FROM users WHERE Id = @Id";
                    string currentHashedPassword = "";

                    using (var getCmd = new MySqlCommand(getQuery, connection))
                    {
                        getCmd.Parameters.AddWithValue("@Id", userId);
                        var result = getCmd.ExecuteScalar();
                        if (result != null)
                        {
                            currentHashedPassword = result.ToString();
                        }
                    }

                    // Verify current password
                    if (!AuthHelper.VerifyPassword(model.CurrentPassword, currentHashedPassword))
                    {
                        TempData["ErrorMessage"] = "Current password is incorrect.";
                        return RedirectToAction("Profile");
                    }

                    // Hash new password
                    string newHashedPassword = AuthHelper.HashPassword(model.NewPassword);

                    // Update password
                    string updateQuery = "UPDATE users SET Password = @Password, UpdatedAt = @UpdatedAt WHERE Id = @Id";
                    using (var cmd = new MySqlCommand(updateQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@Password", newHashedPassword);
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Id", userId);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Log the activity
                            await _activityHelper.LogActivityAsync(
                                "Change Password",
                                "Changed account password"
                            );

                            TempData["SuccessMessage"] = "Password changed successfully!";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Error changing password.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogActivityAsync("Error", $"Change Password: {ex.Message}");
                TempData["ErrorMessage"] = $"Error changing password: {ex.Message}";
            }

            return RedirectToAction("Profile");
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        // Helper Methods

        private DashboardStatistics GetDashboardStatistics()
        {
            var stats = new DashboardStatistics();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // 1. Total Seniors (excluding deleted)
                    string totalSeniorsQuery = "SELECT COUNT(*) FROM seniors WHERE IsDeleted = 0";
                    using (var cmd = new MySqlCommand(totalSeniorsQuery, connection))
                    {
                        stats.TotalSeniors = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // 2. Active Seniors (active status and not deleted)
                    string activeQuery = "SELECT COUNT(*) FROM seniors WHERE Status = 'Active' AND IsDeleted = 0";
                    using (var cmd = new MySqlCommand(activeQuery, connection))
                    {
                        stats.ActiveSeniors = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // 3. Male Count (not deleted)
                    string maleQuery = "SELECT COUNT(*) FROM seniors WHERE s_sex = 'Male' AND IsDeleted = 0";
                    using (var cmd = new MySqlCommand(maleQuery, connection))
                    {
                        stats.MaleCount = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // 4. Female Count (not deleted)
                    string femaleQuery = "SELECT COUNT(*) FROM seniors WHERE s_sex = 'Female' AND IsDeleted = 0";
                    using (var cmd = new MySqlCommand(femaleQuery, connection))
                    {
                        stats.FemaleCount = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // 5. Recent registrations (last 7 days, not deleted)
                    string recentRegQuery = @"
                        SELECT COUNT(*) 
                        FROM seniors 
                        WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY) 
                        AND IsDeleted = 0";
                    using (var cmd = new MySqlCommand(recentRegQuery, connection))
                    {
                        var result = cmd.ExecuteScalar();
                        stats.RecentRegistrations = result != null ? Convert.ToInt32(result) : 0;
                    }

                    // 6. Total Users
                    string usersQuery = "SELECT COUNT(*) FROM users";
                    using (var cmd = new MySqlCommand(usersQuery, connection))
                    {
                        stats.TotalUsers = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // 7. Active Users
                    string activeUsersQuery = "SELECT COUNT(*) FROM users WHERE IsActive = TRUE";
                    using (var cmd = new MySqlCommand(activeUsersQuery, connection))
                    {
                        stats.ActiveUsers = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // 8. Upcoming Events (next 30 days, not deleted)
                    string eventsQuery = @"
                        SELECT COUNT(*) 
                        FROM events 
                        WHERE EventDate >= CURDATE() 
                        AND EventDate <= DATE_ADD(CURDATE(), INTERVAL 30 DAY)
                        AND IsDeleted = 0";
                    using (var cmd = new MySqlCommand(eventsQuery, connection))
                    {
                        var result = cmd.ExecuteScalar();
                        stats.UpcomingEvents = result != null ? Convert.ToInt32(result) : 0;
                    }

                    // 9. Pending Actions
                    stats.PendingActions = GetPendingActionsCount(connection);

                    // 10. Today's date for display
                    stats.CurrentDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting dashboard statistics: {ex.Message}");
                Debug.WriteLine($"DEBUG - Error: {ex.Message}");
                Debug.WriteLine($"DEBUG - Stack trace: {ex.StackTrace}");
            }

            return stats;
        }

        private List<Models.ActivityLog> GetRecentActivities()
        {
            var activities = new List<Models.ActivityLog>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"SELECT Id, UserName, UserRole, Action, Details, IpAddress, CreatedAt
                                   FROM activity_logs 
                                   ORDER BY CreatedAt DESC 
                                   LIMIT 10";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            activities.Add(new Models.ActivityLog
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
                Console.WriteLine($"Error getting activities: {ex.Message}");
                Debug.WriteLine($"DEBUG - Activities Error: {ex.Message}");
            }

            return activities;
        }

        private List<Notification> GetUserNotifications(string userId, string userRole)
        {
            var notifications = new List<Notification>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Get notifications for this user OR notifications for all users (if user is staff/admin)
                    string query = @"
                        SELECT Id, UserId, UserName, UserRole, Type, Title, Message, Url, IsRead, CreatedAt
                        FROM notifications 
                        WHERE (UserId = @UserId OR UserId = 'all')
                        ORDER BY CreatedAt DESC 
                        LIMIT 20";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                notifications.Add(new Notification
                                {
                                    Id = reader.GetInt32("Id"),
                                    UserId = reader.GetString("UserId"),
                                    UserName = reader.GetString("UserName"),
                                    UserRole = reader.GetString("UserRole"),
                                    Type = reader.GetString("Type"),
                                    Title = reader.GetString("Title"),
                                    Message = reader.GetString("Message"),
                                    Url = reader.IsDBNull(reader.GetOrdinal("Url")) ? "" : reader.GetString("Url"),
                                    IsRead = reader.GetBoolean("IsRead"),
                                    CreatedAt = reader.GetDateTime("CreatedAt")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting notifications: {ex.Message}");
            }

            return notifications;
        }

        private UserProfile GetUserProfile(string userId)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT Id, Name, Username, Email, Phone, Role, IsAdmin, IsActive, CreatedAt FROM users WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", userId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new UserProfile
                                {
                                    Id = reader.GetInt32("Id"),
                                    Name = reader.GetString("Name"),
                                    Username = reader.GetString("Username"),
                                    Email = reader.GetString("Email"),
                                    Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? "" : reader.GetString("Phone"),
                                    Role = reader.GetString("Role"),
                                    IsAdmin = reader.GetBoolean("IsAdmin"),
                                    IsActive = reader.GetBoolean("IsActive"),
                                    CreatedAt = reader.GetDateTime("CreatedAt")
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user profile: {ex.Message}");
            }

            return null;
        }

        private int GetPendingActionsCount(MySqlConnection connection)
        {
            string query = @"
                SELECT (
                    (SELECT COUNT(*) FROM seniors WHERE NeedsReview = 1 AND IsDeleted = 0) +
                    (SELECT COUNT(*) FROM events WHERE NeedsReview = 1 AND IsDeleted = 0)
                ) as PendingCount";

            using (var cmd = new MySqlCommand(query, connection))
            {
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0;
            }
        }

        private int GetCount(MySqlConnection connection, string query)
        {
            using (var cmd = new MySqlCommand(query, connection))
            {
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0;
            }
        }
    }

    // Helper Classes
    public class DashboardStatistics
    {
        public int TotalSeniors { get; set; }
        public int ActiveSeniors { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int RecentRegistrations { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int UpcomingEvents { get; set; }
        public int PendingActions { get; set; }
        public string CurrentDate { get; set; }
    }

    public class NotificationRequest
    {
        public int Id { get; set; }
    }

    public class UserProfile
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Role { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ChangePasswordModel
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }

    public class ErrorViewModel
    {
        public string RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}