using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
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
            ViewBag.Name = HttpContext.Session.GetString("UserName") ?? "User";
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "User";
            ViewBag.IsAdmin = ViewBag.UserRole == "Administrator";

            // Get dashboard statistics
            var dashboardStats = GetDashboardStatistics();
            ViewBag.DashboardStats = dashboardStats;

            // Get recent announcements
            var announcements = GetRecentAnnouncements();
            ViewBag.RecentAnnouncements = announcements;

            // Get recent activities
            var activities = GetRecentActivities();
            ViewBag.RecentActivities = activities;

            return View();
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

        private DashboardStatistics GetDashboardStatistics()
        {
            var stats = new DashboardStatistics();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // 1. Total Seniors and Active Seniors - Modified query to include IsDeleted check
                    string seniorQuery = @"
                        SELECT 
                            COUNT(CASE WHEN IsDeleted = 0 THEN 1 END) as TotalSeniors,
                            COUNT(CASE WHEN Status = 'Active' AND IsDeleted = 0 THEN 1 END) as ActiveSeniors,
                            COUNT(CASE WHEN s_sex = 'Male' AND IsDeleted = 0 THEN 1 END) as MaleCount,
                            COUNT(CASE WHEN s_sex = 'Female' AND IsDeleted = 0 THEN 1 END) as FemaleCount
                        FROM seniors";

                    using (var cmd = new MySqlCommand(seniorQuery, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                stats.TotalSeniors = reader.GetInt32("TotalSeniors");
                                stats.ActiveSeniors = reader.GetInt32("ActiveSeniors");
                                stats.MaleCount = reader.GetInt32("MaleCount");
                                stats.FemaleCount = reader.GetInt32("FemaleCount");
                            }
                        }
                    }

                    // 2. Recent registrations (last 7 days)
                    string recentRegQuery = @"
                        SELECT COUNT(*) as RecentCount 
                        FROM seniors 
                        WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY) 
                        AND IsDeleted = 0";

                    using (var cmd = new MySqlCommand(recentRegQuery, connection))
                    {
                        var result = cmd.ExecuteScalar();
                        stats.RecentRegistrations = result != null ? Convert.ToInt32(result) : 0;
                    }

                    // 3. Total Users and Active Users
                    string userQuery = @"
                        SELECT 
                            COUNT(*) as TotalUsers,
                            COUNT(CASE WHEN IsActive = 1 THEN 1 END) as ActiveUsers
                        FROM users";

                    using (var cmd = new MySqlCommand(userQuery, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                stats.TotalUsers = reader.GetInt32("TotalUsers");
                                stats.ActiveUsers = reader.GetInt32("ActiveUsers");
                            }
                        }
                    }

                    // 4. Upcoming Events (next 30 days)
                    string eventsQuery = @"
                        SELECT COUNT(*) as UpcomingEvents 
                        FROM events 
                        WHERE EventDate >= CURDATE() 
                        AND EventDate <= DATE_ADD(CURDATE(), INTERVAL 30 DAY)
                        AND IsDeleted = 0";

                    using (var cmd = new MySqlCommand(eventsQuery, connection))
                    {
                        var result = cmd.ExecuteScalar();
                        stats.UpcomingEvents = result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting dashboard statistics: {ex.Message}");
            }

            return stats;
        }

        private List<Announcement> GetRecentAnnouncements()
        {
            var announcements = new List<Announcement>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Updated query to include Type for badge colors and icons
                    string query = @"
                        SELECT 
                            Id, Title, Message, Type, CreatedBy, CreatedAt,
                            CASE 
                                WHEN Type = 'Important' THEN 'bg-danger'
                                WHEN Type = 'Warning' THEN 'bg-warning'
                                WHEN Type = 'Info' THEN 'bg-info'
                                WHEN Type = 'Success' THEN 'bg-success'
                                ELSE 'bg-secondary'
                            END as BadgeColor,
                            CASE 
                                WHEN Type = 'Important' THEN 'fa-exclamation-triangle'
                                WHEN Type = 'Warning' THEN 'fa-exclamation-circle'
                                WHEN Type = 'Info' THEN 'fa-info-circle'
                                WHEN Type = 'Success' THEN 'fa-check-circle'
                                ELSE 'fa-bullhorn'
                            END as Icon
                        FROM announcements 
                        ORDER BY CreatedAt DESC 
                        LIMIT 5";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            announcements.Add(new Announcement
                            {
                                Id = reader.GetInt32("Id"),
                                Title = reader.GetString("Title"),
                                Message = reader.GetString("Message"),
                                Type = reader.GetString("Type"),
                                CreatedBy = reader.GetString("CreatedBy"),
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                BadgeColor = reader.GetString("BadgeColor"),
                                Icon = reader.GetString("Icon"),
                                TimeAgo = GetTimeAgo(reader.GetDateTime("CreatedAt"))
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting announcements: {ex.Message}");
            }

            return announcements;
        }

        private List<ActivityLog> GetRecentActivities()
        {
            var activities = new List<ActivityLog>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"SELECT Id, UserName, UserRole, Action, Details, CreatedAt
                                   FROM activity_logs 
                                   ORDER BY CreatedAt DESC 
                                   LIMIT 10";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            activities.Add(new ActivityLog
                            {
                                Id = reader.GetInt32("Id"),
                                UserName = reader.GetString("UserName"),
                                UserRole = reader.GetString("UserRole"),
                                Action = reader.GetString("Action"),
                                Details = reader.IsDBNull(reader.GetOrdinal("Details")) ? "" : reader.GetString("Details"),
                                CreatedAt = reader.GetDateTime("CreatedAt")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting activities: {ex.Message}");
            }

            return activities;
        }

        private async Task<List<ActivityLog>> GetRecentActivitiesForDashboard()
        {
            var activities = new List<ActivityLog>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    string query = @"SELECT Id, UserName, UserRole, Action, Details, CreatedAt
                           FROM activity_logs 
                           ORDER BY CreatedAt DESC 
                           LIMIT 10";

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
                                CreatedAt = reader.GetDateTime("CreatedAt")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting activities for dashboard: {ex.Message}");
            }

            return activities;
        }

        // API endpoint for dashboard to get real-time activities
        [HttpGet]
        public async Task<IActionResult> GetDashboardActivities()
        {
            try
            {
                var activities = await GetRecentActivitiesForDashboard();
                return Json(new { success = true, data = activities });
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Get Dashboard Activities");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Helper method to calculate time ago
        private string GetTimeAgo(DateTime date)
        {
            var timeSpan = DateTime.Now.Subtract(date);

            if (timeSpan <= TimeSpan.FromSeconds(60))
                return $"{timeSpan.Seconds} seconds ago";

            if (timeSpan <= TimeSpan.FromMinutes(60))
                return timeSpan.Minutes > 1 ? $"{timeSpan.Minutes} minutes ago" : "a minute ago";

            if (timeSpan <= TimeSpan.FromHours(24))
                return timeSpan.Hours > 1 ? $"{timeSpan.Hours} hours ago" : "an hour ago";

            if (timeSpan <= TimeSpan.FromDays(30))
                return timeSpan.Days > 1 ? $"{timeSpan.Days} days ago" : "yesterday";

            if (timeSpan <= TimeSpan.FromDays(365))
            {
                var months = Convert.ToInt32(Math.Floor(timeSpan.Days / 30.0));
                return months > 1 ? $"{months} months ago" : "a month ago";
            }

            var years = Convert.ToInt32(Math.Floor(timeSpan.Days / 365.0));
            return years > 1 ? $"{years} years ago" : "a year ago";
        }
    }

    public class DashboardStatistics
    {
        public int TotalSeniors { get; set; }
        public int ActiveSeniors { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int TotalEvents { get; set; }
        public int UpcomingEvents { get; set; }
        public string CurrentDate { get; set; }
        public int RecentRegistrations { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
    }

    // Make sure these model classes match what's in your view
    public class Announcement
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string BadgeColor { get; set; }
        public string Icon { get; set; }
        public string TimeAgo { get; set; }
    }

    public class ActivityLog
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string UserRole { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
        public string IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}