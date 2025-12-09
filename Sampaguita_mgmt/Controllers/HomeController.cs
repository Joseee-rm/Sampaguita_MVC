// Complete HomeController.cs with correct column names
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

                // Check if user is admin or staff
                if (ViewBag.IsAdmin)
                {
                    // Admin sees the regular dashboard using DashboardViewModel
                    var viewModel = new DashboardViewModel
                    {
                        SeniorStats = GetSeniorStatsForDashboard(),
                        EventStats = GetEventStatsForDashboard(),
                        RecentActivities = GetRecentActivities()
                    };

                    return View(viewModel);
                }
                else
                {
                    // Staff sees the visualization dashboard
                    return RedirectToAction("StaffDashboard");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Home/Index: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading dashboard data.";
                return View(new DashboardViewModel());
            }
        }

        // STAFF DASHBOARD - Senior & Event Visualizations
        public IActionResult StaffDashboard()
        {
            try
            {
                // Get staff user info
                var staffName = HttpContext.Session.GetString("UserName") ?? "Staff User";

                // Get staff-specific data using existing models
                var seniorStats = GetSeniorStatsForStaff();
                var eventStats = GetEventStatsForStaff();

                var viewModel = new StaffVisualizationViewModel
                {
                    StaffName = staffName,
                    CurrentDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy"),

                    // Senior statistics from SeniorStats model
                    TotalSeniors = seniorStats.TotalSeniors,
                    ActiveSeniors = seniorStats.ActiveSeniors,
                    MaleCount = seniorStats.MaleCount,
                    FemaleCount = seniorStats.FemaleCount,
                    RecentRegistrations = seniorStats.RecentRegistrations,
                    Zone1Count = seniorStats.ZoneDistribution.ContainsKey(1) ? seniorStats.ZoneDistribution[1] : 0,
                    Zone2Count = seniorStats.ZoneDistribution.ContainsKey(2) ? seniorStats.ZoneDistribution[2] : 0,
                    Zone3Count = seniorStats.ZoneDistribution.ContainsKey(3) ? seniorStats.ZoneDistribution[3] : 0,
                    Zone4Count = seniorStats.ZoneDistribution.ContainsKey(4) ? seniorStats.ZoneDistribution[4] : 0,
                    Zone5Count = seniorStats.ZoneDistribution.ContainsKey(5) ? seniorStats.ZoneDistribution[5] : 0,
                    Zone6Count = seniorStats.ZoneDistribution.ContainsKey(6) ? seniorStats.ZoneDistribution[6] : 0,
                    Zone7Count = seniorStats.ZoneDistribution.ContainsKey(7) ? seniorStats.ZoneDistribution[7] : 0,
                    Age60_69 = seniorStats.Age60_69,
                    Age70_79 = seniorStats.Age70_79,
                    Age80_89 = seniorStats.Age80_89,
                    Age90plus = seniorStats.Age90plus,

                    // Event statistics from EventStats model
                    TotalEvents = eventStats.TotalEvents,
                    UpcomingEvents = eventStats.UpcomingEvents,
                    TodayEvents = eventStats.TodayEvents,
                    MedicalEvents = eventStats.MedicalCount,
                    AssistanceEvents = eventStats.AssistanceCount,
                    CommunityEvents = eventStats.CommunityCount,
                    WellnessEvents = eventStats.WellnessCount,

                    // Get civil status distribution
                    CivilStatusSingle = seniorStats.CivilStatusSingle,
                    CivilStatusMarried = seniorStats.CivilStatusMarried,
                    CivilStatusWidowed = seniorStats.CivilStatusWidowed,
                    CivilStatusSeparated = seniorStats.CivilStatusSeparated,
                    CivilStatusDivorced = seniorStats.CivilStatusDivorced,

                    // Get charts data
                    GenderDistribution = GetGenderDistribution(),
                    ZoneDistribution = GetZoneDistribution(),
                    AgeDistribution = GetAgeDistribution(),
                    EventTypeDistribution = GetEventTypeDistribution(),
                    CivilStatusDistribution = GetCivilStatusDistribution(),

                    // Get quick lists
                    RecentSeniors = GetRecentSeniors(10),
                    UpcomingEventsList = GetUpcomingEventsList(5)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StaffDashboard: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading staff dashboard data.";
                return View(new StaffVisualizationViewModel());
            }
        }

        // AJAX METHODS FOR STAFF DASHBOARD
        [HttpGet]
        public JsonResult GetStaffDashboardStats()
        {
            try
            {
                var seniorStats = GetSeniorStatsForStaff();
                var eventStats = GetEventStatsForStaff();

                var stats = new
                {
                    SeniorStats = seniorStats,
                    EventStats = eventStats,
                    CivilStatusDistribution = GetCivilStatusDistribution()
                };

                return Json(new { success = true, stats = stats });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // HELPER METHODS FOR STAFF DASHBOARD
        private SeniorStats GetSeniorStatsForStaff()
        {
            var stats = new SeniorStats();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Get total seniors (no IsDeleted column in your table)
                    string totalQuery = "SELECT COUNT(*) FROM seniors";
                    stats.TotalSeniors = GetCount(connection, totalQuery);

                    // Get active seniors
                    string activeQuery = "SELECT COUNT(*) FROM seniors WHERE Status = 'Active'";
                    stats.ActiveSeniors = GetCount(connection, activeQuery);

                    // Get archived seniors
                    string archivedQuery = "SELECT COUNT(*) FROM seniors WHERE Status = 'Archived'";
                    stats.ArchivedSeniors = GetCount(connection, archivedQuery);

                    // Get gender counts - CORRECTED: your column is 'Gender' not 's_sex'
                    string maleQuery = "SELECT COUNT(*) FROM seniors WHERE Gender = 'Male'";
                    stats.MaleCount = GetCount(connection, maleQuery);

                    string femaleQuery = "SELECT COUNT(*) FROM seniors WHERE Gender = 'Female'";
                    stats.FemaleCount = GetCount(connection, femaleQuery);

                    // Get recent registrations (last 7 days)
                    string recentQuery = @"
                        SELECT COUNT(*) 
                        FROM seniors 
                        WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY)";
                    stats.RecentRegistrations = GetCount(connection, recentQuery);

                    // Get age distribution
                    stats.Age60_69 = GetCount(connection,
                        "SELECT COUNT(*) FROM seniors WHERE Age >= 60 AND Age <= 69");
                    stats.Age70_79 = GetCount(connection,
                        "SELECT COUNT(*) FROM seniors WHERE Age >= 70 AND Age <= 79");
                    stats.Age80_89 = GetCount(connection,
                        "SELECT COUNT(*) FROM seniors WHERE Age >= 80 AND Age <= 89");
                    stats.Age90plus = GetCount(connection,
                        "SELECT COUNT(*) FROM seniors WHERE Age >= 90");

                    // Get zone distribution
                    stats.ZoneDistribution = new Dictionary<int, int>();
                    for (int i = 1; i <= 7; i++)
                    {
                        string zoneQuery = $"SELECT COUNT(*) FROM seniors WHERE Zone = {i}";
                        stats.ZoneDistribution[i] = GetCount(connection, zoneQuery);
                    }

                    // Get civil status
                    stats.CivilStatusSingle = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE CivilStatus = 'Single'");
                    stats.CivilStatusMarried = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE CivilStatus = 'Married'");
                    stats.CivilStatusWidowed = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE CivilStatus = 'Widowed'");
                    stats.CivilStatusSeparated = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE CivilStatus = 'Separated'");
                    stats.CivilStatusDivorced = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE CivilStatus = 'Divorced'");

                    // Get contact information
                    stats.WithContact = GetCount(connection,
                        "SELECT COUNT(*) FROM seniors WHERE ContactNumber IS NOT NULL AND ContactNumber != ''");
                    stats.WithoutContact = GetCount(connection,
                        "SELECT COUNT(*) FROM seniors WHERE ContactNumber IS NULL OR ContactNumber = ''");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting senior stats for staff: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            return stats;
        }

        private EventStats GetEventStatsForStaff()
        {
            var stats = new EventStats();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Check if IsDeleted column exists
                    bool hasIsDeletedColumn = CheckColumnExists(connection, "events", "IsDeleted");

                    // Base query condition
                    string condition = hasIsDeletedColumn ? " WHERE IsDeleted = 0" : "";

                    // Get total events
                    stats.TotalEvents = GetCount(connection, $"SELECT COUNT(*) FROM events{condition}");

                    // Get upcoming events (next 30 days)
                    string upcomingCondition = hasIsDeletedColumn ?
                        " WHERE EventDate >= CURDATE() AND EventDate <= DATE_ADD(CURDATE(), INTERVAL 30 DAY) AND IsDeleted = 0" :
                        " WHERE EventDate >= CURDATE() AND EventDate <= DATE_ADD(CURDATE(), INTERVAL 30 DAY)";
                    stats.UpcomingEvents = GetCount(connection, $"SELECT COUNT(*) FROM events{upcomingCondition}");

                    // Get today's events
                    string todayCondition = hasIsDeletedColumn ?
                        " WHERE DATE(EventDate) = CURDATE() AND IsDeleted = 0" :
                        " WHERE DATE(EventDate) = CURDATE()";
                    stats.TodayEvents = GetCount(connection, $"SELECT COUNT(*) FROM events{todayCondition}");

                    // Get event type counts
                    string typeBase = $"SELECT COUNT(*) FROM events WHERE EventType = '{{0}}'";
                    if (hasIsDeletedColumn)
                    {
                        typeBase += " AND IsDeleted = 0";
                    }

                    stats.MedicalCount = GetCount(connection, string.Format(typeBase, "Medical Mission"));
                    stats.AssistanceCount = GetCount(connection, string.Format(typeBase, "Assistance Program"));
                    stats.CommunityCount = GetCount(connection, string.Format(typeBase, "Community Gathering"));
                    stats.WellnessCount = GetCount(connection, string.Format(typeBase, "Wellness Activity"));
                    stats.EducationalCount = GetCount(connection, string.Format(typeBase, "Educational"));
                    stats.SocialCount = GetCount(connection, string.Format(typeBase, "Social"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting event stats for staff: {ex.Message}");
            }

            return stats;
        }

        private List<GenderData> GetGenderDistribution()
        {
            var distribution = new List<GenderData>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Get male count - CORRECTED: your column is 'Gender' not 's_sex'
                    string maleQuery = "SELECT COUNT(*) FROM seniors WHERE Gender = 'Male'";
                    int maleCount = GetCount(connection, maleQuery);

                    // Get female count - CORRECTED: your column is 'Gender' not 's_sex'
                    string femaleQuery = "SELECT COUNT(*) FROM seniors WHERE Gender = 'Female'";
                    int femaleCount = GetCount(connection, femaleQuery);

                    int total = maleCount + femaleCount;

                    if (total > 0)
                    {
                        distribution.Add(new GenderData
                        {
                            Gender = "Male",
                            Count = maleCount,
                            Percentage = Math.Round((maleCount / (double)total) * 100, 1)
                        });

                        distribution.Add(new GenderData
                        {
                            Gender = "Female",
                            Count = femaleCount,
                            Percentage = Math.Round((femaleCount / (double)total) * 100, 1)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting gender distribution: {ex.Message}");
            }

            return distribution;
        }

        private List<ZoneData> GetZoneDistribution()
        {
            var distribution = new List<ZoneData>();
            var colors = new[] { "#4e73df", "#1cc88a", "#36b9cc", "#f6c23e", "#e74a3b", "#858796", "#5a5c69" };

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    for (int i = 1; i <= 7; i++)
                    {
                        string query = $"SELECT COUNT(*) FROM seniors WHERE Zone = {i}";
                        int count = GetCount(connection, query);

                        distribution.Add(new ZoneData
                        {
                            Zone = $"Zone {i}",
                            Count = count,
                            Color = colors[i - 1]
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting zone distribution: {ex.Message}");
            }

            return distribution;
        }

        private List<AgeGroupData> GetAgeDistribution()
        {
            var distribution = new List<AgeGroupData>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    distribution.Add(new AgeGroupData
                    {
                        AgeGroup = "60-69",
                        Count = GetCount(connection,
                            "SELECT COUNT(*) FROM seniors WHERE Age >= 60 AND Age <= 69")
                    });

                    distribution.Add(new AgeGroupData
                    {
                        AgeGroup = "70-79",
                        Count = GetCount(connection,
                            "SELECT COUNT(*) FROM seniors WHERE Age >= 70 AND Age <= 79")
                    });

                    distribution.Add(new AgeGroupData
                    {
                        AgeGroup = "80-89",
                        Count = GetCount(connection,
                            "SELECT COUNT(*) FROM seniors WHERE Age >= 80 AND Age <= 89")
                    });

                    distribution.Add(new AgeGroupData
                    {
                        AgeGroup = "90+",
                        Count = GetCount(connection,
                            "SELECT COUNT(*) FROM seniors WHERE Age >= 90")
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting age distribution: {ex.Message}");
            }

            return distribution;
        }

        private List<EventTypeData> GetEventTypeDistribution()
        {
            var distribution = new List<EventTypeData>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Check if IsDeleted column exists
                    bool hasIsDeletedColumn = CheckColumnExists(connection, "events", "IsDeleted");
                    string condition = hasIsDeletedColumn ? " AND IsDeleted = 0" : "";

                    var eventTypes = new[]
                    {
                        new { Type = "Medical Mission", Icon = "fa-stethoscope" },
                        new { Type = "Assistance Program", Icon = "fa-hand-holding-heart" },
                        new { Type = "Community Gathering", Icon = "fa-users" },
                        new { Type = "Wellness Activity", Icon = "fa-heartbeat" },
                        new { Type = "Educational", Icon = "fa-graduation-cap" },
                        new { Type = "Social", Icon = "fa-glass-cheers" }
                    };

                    foreach (var eventType in eventTypes)
                    {
                        string query = $"SELECT COUNT(*) FROM events WHERE EventType = '{eventType.Type}'{condition}";
                        int count = GetCount(connection, query);

                        distribution.Add(new EventTypeData
                        {
                            EventType = eventType.Type,
                            Count = count,
                            Icon = eventType.Icon
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting event type distribution: {ex.Message}");
            }

            return distribution;
        }

        private List<CivilStatusData> GetCivilStatusDistribution()
        {
            var distribution = new List<CivilStatusData>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    var statuses = new[] { "Single", "Married", "Widowed", "Separated", "Divorced" };

                    foreach (var status in statuses)
                    {
                        string query = $"SELECT COUNT(*) FROM seniors WHERE CivilStatus = '{status}'";
                        int count = GetCount(connection, query);

                        distribution.Add(new CivilStatusData
                        {
                            Status = status,
                            Count = count
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting civil status distribution: {ex.Message}");
            }

            return distribution;
        }

        private List<SeniorBasicInfo> GetRecentSeniors(int limit = 10)
        {
            var seniors = new List<SeniorBasicInfo>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // CORRECTED: Using your actual column names
                    string query = @"
                        SELECT Id, CONCAT(FirstName, ' ', LastName) as FullName, Age, Zone, Status, CreatedAt 
                        FROM seniors 
                        ORDER BY CreatedAt DESC 
                        LIMIT @Limit";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Limit", limit);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                seniors.Add(new SeniorBasicInfo
                                {
                                    Id = reader.GetInt32("Id"),
                                    Name = reader.GetString("FullName"),
                                    Age = reader.GetInt32("Age"),
                                    Zone = reader.GetString("Zone"),
                                    Status = reader.GetString("Status"),
                                    RegisteredDate = reader.GetDateTime("CreatedAt")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting recent seniors: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            return seniors;
        }

        private List<EventBasicInfo> GetUpcomingEventsList(int limit = 5)
        {
            var events = new List<EventBasicInfo>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Check if IsDeleted column exists
                    bool hasIsDeletedColumn = CheckColumnExists(connection, "events", "IsDeleted");
                    string condition = hasIsDeletedColumn ? " AND IsDeleted = 0" : "";

                    string query = @"
                        SELECT Id, Title, EventType, EventDate, Location, Status
                        FROM events 
                        WHERE EventDate >= CURDATE()" + condition + @"
                        ORDER BY EventDate ASC 
                        LIMIT @Limit";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Limit", limit);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                events.Add(new EventBasicInfo
                                {
                                    Id = reader.GetInt32("Id"),
                                    Title = reader.GetString("Title"),
                                    EventType = reader.GetString("EventType"),
                                    EventDate = reader.GetDateTime("EventDate"),
                                    Location = reader.GetString("Location"),
                                    Status = reader.GetString("Status")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting upcoming events: {ex.Message}");
            }

            return events;
        }

        // ADMIN DASHBOARD METHODS (using DashboardViewModel)
        private SeniorStats GetSeniorStatsForDashboard()
        {
            return GetSeniorStatsForStaff(); // Same method for both dashboards
        }

        private EventStats GetEventStatsForDashboard()
        {
            var stats = new EventStats();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Check if IsDeleted column exists
                    bool hasIsDeletedColumn = CheckColumnExists(connection, "events", "IsDeleted");

                    // Base query condition
                    string condition = hasIsDeletedColumn ? " WHERE IsDeleted = 0" : "";

                    // Get total events
                    stats.TotalEvents = GetCount(connection, $"SELECT COUNT(*) FROM events{condition}");

                    // Get upcoming events (next 30 days)
                    string upcomingCondition = hasIsDeletedColumn ?
                        " WHERE EventDate >= CURDATE() AND EventDate <= DATE_ADD(CURDATE(), INTERVAL 30 DAY) AND IsDeleted = 0" :
                        " WHERE EventDate >= CURDATE() AND EventDate <= DATE_ADD(CURDATE(), INTERVAL 30 DAY)";
                    stats.UpcomingEvents = GetCount(connection, $"SELECT COUNT(*) FROM events{upcomingCondition}");

                    // Get today's events
                    string todayCondition = hasIsDeletedColumn ?
                        " WHERE DATE(EventDate) = CURDATE() AND IsDeleted = 0" :
                        " WHERE DATE(EventDate) = CURDATE()";
                    stats.TodayEvents = GetCount(connection, $"SELECT COUNT(*) FROM events{todayCondition}");

                    // Get event status counts
                    string statusBase = $"SELECT COUNT(*) FROM events WHERE Status = '{{0}}'";
                    if (hasIsDeletedColumn)
                    {
                        statusBase += " AND IsDeleted = 0";
                    }

                    stats.ScheduledEvents = GetCount(connection, string.Format(statusBase, "Scheduled"));
                    stats.OngoingEvents = GetCount(connection, string.Format(statusBase, "Ongoing"));
                    stats.CompletedEvents = GetCount(connection, string.Format(statusBase, "Completed"));
                    stats.CancelledEvents = GetCount(connection, string.Format(statusBase, "Cancelled"));

                    // Get event type counts
                    string typeBase = $"SELECT COUNT(*) FROM events WHERE EventType = '{{0}}'";
                    if (hasIsDeletedColumn)
                    {
                        typeBase += " AND IsDeleted = 0";
                    }

                    stats.MedicalCount = GetCount(connection, string.Format(typeBase, "Medical Mission"));
                    stats.AssistanceCount = GetCount(connection, string.Format(typeBase, "Assistance Program"));
                    stats.CommunityCount = GetCount(connection, string.Format(typeBase, "Community Gathering"));
                    stats.WellnessCount = GetCount(connection, string.Format(typeBase, "Wellness Activity"));
                    stats.EducationalCount = GetCount(connection, string.Format(typeBase, "Educational"));
                    stats.SocialCount = GetCount(connection, string.Format(typeBase, "Social"));

                    // Get attendance and capacity
                    string attendanceQuery = "SELECT SUM(AttendanceCount) FROM events";
                    string capacityQuery = "SELECT SUM(MaxCapacity) FROM events";

                    if (hasIsDeletedColumn)
                    {
                        attendanceQuery += " WHERE IsDeleted = 0";
                        capacityQuery += " WHERE IsDeleted = 0";
                    }

                    stats.TotalAttendance = SafeGetInt(connection, attendanceQuery);
                    stats.TotalCapacity = SafeGetInt(connection, capacityQuery);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting event stats for dashboard: {ex.Message}");
            }

            return stats;
        }

        // EXISTING AJAX METHODS (for both dashboards)
        [HttpGet]
        public JsonResult GetDashboardStats()
        {
            try
            {
                var viewModel = new DashboardViewModel
                {
                    SeniorStats = GetSeniorStatsForDashboard(),
                    EventStats = GetEventStatsForDashboard(),
                    RecentActivities = GetRecentActivities()
                };
                return Json(new { success = true, stats = viewModel });
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

        // HELPER METHODS
        private List<ActivityLog> GetRecentActivities()
        {
            var activities = new List<ActivityLog>();

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

        private int GetCount(MySqlConnection connection, string query)
        {
            try
            {
                using (var cmd = new MySqlCommand(query, connection))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCount for query: {query} - {ex.Message}");
                return 0;
            }
        }

        private int SafeGetInt(MySqlConnection connection, string query)
        {
            try
            {
                using (var cmd = new MySqlCommand(query, connection))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SafeGetInt for query: {query} - {ex.Message}");
            }
            return 0;
        }

        private bool CheckColumnExists(MySqlConnection connection, string tableName, string columnName)
        {
            try
            {
                string query = @"
                    SELECT COUNT(*) 
                    FROM information_schema.columns 
                    WHERE table_name = @TableName 
                    AND column_name = @ColumnName
                    AND table_schema = DATABASE()";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@TableName", tableName);
                    cmd.Parameters.AddWithValue("@ColumnName", columnName);
                    var result = cmd.ExecuteScalar();
                    return Convert.ToInt32(result) > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        // EXISTING METHODS (keep as is)
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
    }

    // HELPER CLASSES
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