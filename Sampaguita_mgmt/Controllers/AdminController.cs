
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SeniorManagement.Models;
using SeniorManagement.Helpers;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SeniorManagement.Controllers
{
    [Authorize]
    public class AdminController : BaseController
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly AuthHelper _authHelper;
        private readonly ActivityHelper _activityHelper;

        public AdminController(DatabaseHelper dbHelper, AuthHelper authHelper, ActivityHelper activityHelper)
        {
            _dbHelper = dbHelper;
            _authHelper = authHelper;
            _activityHelper = activityHelper;
        }

        [HttpGet]
        public IActionResult TestConnection()
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    var cmd = new MySqlCommand("SELECT COUNT(*) FROM users", connection);
                    var count = cmd.ExecuteScalar();
                    return Content($"Connection successful! User count: {count}");
                }
            }
            catch (Exception ex)
            {
                return Content($"Connection failed: {ex.Message}\n\nStack Trace: {ex.StackTrace}");
            }
        }

        public IActionResult ManageUsers()
        {
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            var users = GetAllUsers();
            return View(users);
        }

        private List<User> GetAllUsers()
        {
            var users = new List<User>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT Id, Name, Username, Role, IsAdmin, IsActive, CreatedAt FROM users ORDER BY CreatedAt DESC";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new User
                            {
                                Id = reader.GetInt32("Id"),
                                Name = reader.GetString("Name"),
                                Username = reader.GetString("Username"),
                                Role = reader.GetString("Role"),
                                IsAdmin = reader.GetBoolean("IsAdmin"),
                                IsActive = reader.GetBoolean("IsActive"),
                                CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ?
                                           DateTime.Now : reader.GetDateTime("CreatedAt")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting users: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading users from database.";
            }

            return users;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            try
            {
                if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
                {
                    TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                    return RedirectToAction("Index", "Home");
                }

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string getQuery = "SELECT IsActive FROM users WHERE Id = @Id";
                    bool currentStatus = false;

                    using (var getCmd = new MySqlCommand(getQuery, connection))
                    {
                        getCmd.Parameters.AddWithValue("@Id", id);
                        var result = getCmd.ExecuteScalar();
                        if (result != null)
                        {
                            currentStatus = Convert.ToBoolean(result);
                        }
                    }

                    string updateQuery = "UPDATE users SET IsActive = @IsActive WHERE Id = @Id";
                    using (var updateCmd = new MySqlCommand(updateQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@IsActive", !currentStatus);
                        updateCmd.Parameters.AddWithValue("@Id", id);
                        updateCmd.ExecuteNonQuery();
                    }

                    var user = GetUserById(id);
                    if (user != null)
                    {
                        await _activityHelper.LogActivityAsync(
                            "Toggle User Status",
                            $"User '{user.Name}' ({user.Username}) {(currentStatus ? "deactivated" : "activated")}"
                        );
                    }

                    TempData["SuccessMessage"] = $"User {(currentStatus ? "deactivated" : "activated")} successfully!";
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogActivityAsync("Error", ex.Message);
                Debug.WriteLine($"Error toggling user status: {ex.Message}");
                TempData["ErrorMessage"] = "Error updating user status.";
            }

            return RedirectToAction("ManageUsers");
        }

        [HttpGet]
        public IActionResult EditUser(int id)
        {
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            var user = GetUserById(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("ManageUsers");
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User model)
        {
            Debug.WriteLine($"EditUser POST called with ID: {model.Id}");

            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.Username))
            {
                TempData["ErrorMessage"] = "All required fields must be filled.";
                return View(model);
            }

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    Debug.WriteLine("Database connection opened");

                    string checkQuery = "SELECT COUNT(*) FROM users WHERE Username = @Username AND Id != @Id";
                    using (var checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@Username", model.Username);
                        checkCmd.Parameters.AddWithValue("@Id", model.Id);
                        int userCount = Convert.ToInt32(checkCmd.ExecuteScalar());
                        Debug.WriteLine($"Duplicate check found {userCount} users");

                        if (userCount > 0)
                        {
                            TempData["ErrorMessage"] = "Username already exists. Please choose a different username.";
                            return View(model);
                        }
                    }

                    string updateQuery = @"UPDATE users 
                                         SET Name = @Name, 
                                             Username = @Username
                                         WHERE Id = @Id";

                    Debug.WriteLine($"Executing update query: {updateQuery}");

                    using (var cmd = new MySqlCommand(updateQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@Name", model.Name);
                        cmd.Parameters.AddWithValue("@Username", model.Username);
                        cmd.Parameters.AddWithValue("@Id", model.Id);

                        Debug.WriteLine($"Parameters set - Name: {model.Name}, Username: {model.Username}, Id: {model.Id}");

                        int rowsAffected = cmd.ExecuteNonQuery();
                        Debug.WriteLine($"Rows affected: {rowsAffected}");

                        if (rowsAffected > 0)
                        {
                            await _activityHelper.LogActivityAsync(
                                "Edit User",
                                $"Updated user '{model.Name}' ({model.Username})"
                            );

                            TempData["SuccessMessage"] = "User updated successfully!";
                            return RedirectToAction("ManageUsers");
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "No changes were made. User may not exist.";
                            return View(model);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogActivityAsync("Error", $"Edit User: {ex.Message}");
                Debug.WriteLine($"Error in EditUser POST: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                TempData["ErrorMessage"] = $"Error updating user: {ex.Message}";
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult ResetPassword(int id)
        {
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            var user = GetUserById(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("ManageUsers");
            }

            var viewModel = new ResetPasswordViewModel
            {
                UserId = user.Id,
                Username = user.Username,
                Name = user.Name
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            try
            {
                if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
                {
                    TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                    return RedirectToAction("Index", "Home");
                }

                if (string.IsNullOrEmpty(model.NewPassword))
                {
                    ModelState.AddModelError("NewPassword", "New password is required.");
                    return View(model);
                }

                if (model.NewPassword != model.ConfirmPassword)
                {
                    ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
                    return View(model);
                }

                if (model.NewPassword.Length < 6)
                {
                    ModelState.AddModelError("NewPassword", "Password must be at least 6 characters long.");
                    return View(model);
                }

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string hashedPassword = AuthHelper.HashPassword(model.NewPassword);

                    string query = "UPDATE users SET Password = @Password WHERE Id = @Id";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Password", hashedPassword);
                        cmd.Parameters.AddWithValue("@Id", model.UserId);
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            var user = GetUserById(model.UserId);
                            if (user != null)
                            {
                                await _activityHelper.LogActivityAsync(
                                    "Reset Password",
                                    $"Password manually reset for user '{user.Name}' ({user.Username}) by administrator"
                                );
                            }

                            TempData["SuccessMessage"] = "Password reset successfully!";
                            return RedirectToAction("ManageUsers");
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Error resetting password. User not found.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogActivityAsync("Error", $"Reset Password: {ex.Message}");
                Debug.WriteLine($"Error resetting password: {ex.Message}");
                TempData["ErrorMessage"] = $"Error resetting password: {ex.Message}";
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickResetPassword(int id)
        {
            try
            {
                if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
                {
                    TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                    return RedirectToAction("Index", "Home");
                }

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    var user = GetUserById(id);
                    if (user == null)
                    {
                        TempData["ErrorMessage"] = "User not found.";
                        return RedirectToAction("ManageUsers");
                    }

                    string generatedPassword = GeneratePasswordFromName(user.Name);
                    string hashedPassword = AuthHelper.HashPassword(generatedPassword);

                    string query = "UPDATE users SET Password = @Password WHERE Id = @Id";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Password", hashedPassword);
                        cmd.Parameters.AddWithValue("@Id", id);
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            await _activityHelper.LogActivityAsync(
                                "Quick Reset Password",
                                $"Password quickly reset for user '{user.Name}' ({user.Username})"
                            );

                            TempData["SuccessMessage"] = $"Password reset successfully! New password: <strong>{generatedPassword}</strong>";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Error resetting password. User not found.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogActivityAsync("Error", $"Quick Reset Password: {ex.Message}");
                Debug.WriteLine($"Error resetting password: {ex.Message}");
                TempData["ErrorMessage"] = "Error resetting password.";
            }

            return RedirectToAction("ManageUsers");
        }

        private User GetUserById(int id)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT Id, Name, Username, Role, IsAdmin, IsActive, CreatedAt FROM users WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new User
                                {
                                    Id = reader.GetInt32("Id"),
                                    Name = reader.GetString("Name"),
                                    Username = reader.GetString("Username"),
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
                Debug.WriteLine($"Error getting user: {ex.Message}");
            }

            return null;
        }

        [HttpGet]
        public IActionResult AddUser()
        {
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            return View(new User
            {
                Role = "Staff",
                IsAdmin = false,
                IsActive = true
            });
        }

        [Authorize(Roles = "Administrator")]
        public IActionResult Dashboard()
        {
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var viewModel = new DashboardViewModel
                {
                    SeniorStats = GetSeniorStats(),
                    EventStats = GetEventStatsData(),
                    RecentActivities = GetRecentActivities()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in Dashboard: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading dashboard data.";
                return View(new DashboardViewModel());
            }
        }

        private SeniorStats GetSeniorStats()
        {
            var stats = new SeniorStats();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Simple queries without IsDeleted filter (since column doesn't exist)
                    stats.TotalSeniors = GetCount(connection, "SELECT COUNT(*) FROM seniors");
                    stats.ActiveSeniors = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE Status = 'Active'");
                    stats.ArchivedSeniors = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE Status = 'Archived'");

                    // Age groups
                    stats.Age60_69 = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE Age >= 60 AND Age <= 69");
                    stats.Age70_79 = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE Age >= 70 AND Age <= 79");
                    stats.Age80_89 = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE Age >= 80 AND Age <= 89");
                    stats.Age90plus = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE Age >= 90");

                    // Gender
                    stats.MaleCount = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE Gender = 'Male'");
                    stats.FemaleCount = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE Gender = 'Female'");

                    // Zone distribution
                    for (int i = 1; i <= 7; i++)
                    {
                        stats.ZoneDistribution[i] = GetCount(connection,
                            $"SELECT COUNT(*) FROM seniors WHERE Zone = {i}");
                    }

                    // Civil Status
                    stats.CivilStatusSingle = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE CivilStatus = 'Single'");
                    stats.CivilStatusMarried = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE CivilStatus = 'Married'");
                    stats.CivilStatusWidowed = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE CivilStatus = 'Widowed'");
                    stats.CivilStatusSeparated = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE CivilStatus = 'Separated'");
                    stats.CivilStatusDivorced = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE CivilStatus = 'Divorced'");

                    // Contact Information
                    stats.WithContact = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE ContactNumber IS NOT NULL AND ContactNumber != ''");
                    stats.WithoutContact = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE ContactNumber IS NULL OR ContactNumber = ''");

                    // Recent registrations (last 7 days) - FIXED
                    stats.RecentRegistrations = GetCount(connection,
                        "SELECT COUNT(*) FROM seniors WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting senior stats: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                // For debugging
                try
                {
                    using (var connection = _dbHelper.GetConnection())
                    {
                        connection.Open();

                        var cmd = new MySqlCommand("SELECT COUNT(*) FROM seniors", connection);
                        var count = cmd.ExecuteScalar();
                        Debug.WriteLine($"DEBUG: Raw count from seniors table: {count}");
                    }
                }
                catch (Exception debugEx)
                {
                    Debug.WriteLine($"DEBUG Error: {debugEx.Message}");
                }
            }

            return stats;
        }

        private EventStats GetEventStatsData()
        {
            var stats = new EventStats();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Check if IsDeleted column exists in events table
                    bool hasIsDeletedColumn = CheckColumnExists(connection, "events", "IsDeleted");

                    string baseQuery = hasIsDeletedColumn ?
                        "SELECT COUNT(*) FROM events WHERE IsDeleted = 0" :
                        "SELECT COUNT(*) FROM events";

                    stats.TotalEvents = GetCount(connection, baseQuery);

                    string upcomingQuery = "SELECT COUNT(*) FROM events WHERE EventDate >= CURDATE()";
                    string todayQuery = "SELECT COUNT(*) FROM events WHERE DATE(EventDate) = CURDATE()";

                    if (hasIsDeletedColumn)
                    {
                        upcomingQuery += " AND IsDeleted = 0";
                        todayQuery += " AND IsDeleted = 0";
                    }

                    stats.UpcomingEvents = GetCount(connection, upcomingQuery);
                    stats.TodayEvents = GetCount(connection, todayQuery);

                    // Event status counts
                    string statusBase = "SELECT COUNT(*) FROM events WHERE Status = '{0}'";
                    if (hasIsDeletedColumn)
                    {
                        statusBase += " AND IsDeleted = 0";
                    }

                    stats.ScheduledEvents = GetCount(connection, string.Format(statusBase, "Scheduled"));
                    stats.OngoingEvents = GetCount(connection, string.Format(statusBase, "Ongoing"));
                    stats.CompletedEvents = GetCount(connection, string.Format(statusBase, "Completed"));
                    stats.CancelledEvents = GetCount(connection, string.Format(statusBase, "Cancelled"));

                    // Event types
                    string typeBase = "SELECT COUNT(*) FROM events WHERE EventType = '{0}'";
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

                    // Attendance
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
                Debug.WriteLine($"Error getting event stats: {ex.Message}");
            }

            return stats;
        }

        // Helper method to check if a column exists
        private bool CheckColumnExists(MySqlConnection connection, string tableName, string columnName)
        {
            try
            {
                string query = $@"
                    SELECT COUNT(*) 
                    FROM information_schema.columns 
                    WHERE table_name = '{tableName}' 
                    AND column_name = '{columnName}'
                    AND table_schema = DATABASE()";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    var result = cmd.ExecuteScalar();
                    return Convert.ToInt32(result) > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        // New helper method to safely get integer values
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
                Debug.WriteLine($"Error in SafeGetInt for query: {query} - {ex.Message}");
            }
            return 0;
        }

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
                Debug.WriteLine($"Error getting activities: {ex.Message}");
            }

            return activities;
        }

        private int GetCount(MySqlConnection connection, string query)
        {
            try
            {
                using (var cmd = new MySqlCommand(query, connection))
                {
                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetCount for query: {query} - {ex.Message}");
                return 0;
            }
        }

        private int GetSum(MySqlConnection connection, string query)
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
                Debug.WriteLine($"Error getting sum: {ex.Message}");
            }
            return 0;
        }

        private string GeneratePasswordFromName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "password123";

            try
            {
                string cleanedName = Regex.Replace(fullName, @"[^a-zA-Z\s]", "", RegexOptions.None, TimeSpan.FromMilliseconds(100));
                var nameParts = cleanedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (nameParts.Length == 0)
                    return "password123";

                string basePassword;

                if (nameParts.Length == 1)
                {
                    basePassword = nameParts[0].ToLower();
                }
                else
                {
                    string firstName = nameParts[0].ToLower();
                    string lastName = nameParts[nameParts.Length - 1].ToLower();
                    basePassword = firstName + lastName;
                }

                string password = basePassword + "123";

                if (password.Length < 8)
                {
                    password = password.PadRight(8, '1');
                }

                return password;
            }
            catch (Exception)
            {
                return "password123";
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(User model)
        {
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            model.Role = "Staff";
            model.IsAdmin = false;

            if (string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.Username))
            {
                TempData["ErrorMessage"] = "Name and Username are required.";
                return View(model);
            }

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string checkQuery = "SELECT COUNT(*) FROM users WHERE Username = @Username";
                    using (var checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@Username", model.Username);
                        int userCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (userCount > 0)
                        {
                            TempData["ErrorMessage"] = "Username already exists. Please choose a different username.";
                            return View(model);
                        }
                    }

                    string generatedPassword = GeneratePasswordFromName(model.Name);
                    string hashedPassword = AuthHelper.HashPassword(generatedPassword);

                    string insertQuery = @"INSERT INTO users (Name, Username, Password, Role, IsAdmin, IsActive, CreatedAt) 
                                         VALUES (@Name, @Username, @Password, @Role, @IsAdmin, @IsActive, @CreatedAt)";

                    using (var cmd = new MySqlCommand(insertQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@Name", model.Name);
                        cmd.Parameters.AddWithValue("@Username", model.Username);
                        cmd.Parameters.AddWithValue("@Password", hashedPassword);
                        cmd.Parameters.AddWithValue("@Role", model.Role);
                        cmd.Parameters.AddWithValue("@IsAdmin", model.IsAdmin);
                        cmd.Parameters.AddWithValue("@IsActive", true);
                        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            await _activityHelper.LogActivityAsync(
                                "Add User",
                                $"Added new user '{model.Name}' ({model.Username}) as {model.Role}"
                            );

                            TempData["SuccessMessage"] = $"User added successfully! Generated password: <strong>{generatedPassword}</strong>";
                            return RedirectToAction("ManageUsers");
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Error adding user to database.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogActivityAsync("Error", $"Add User: {ex.Message}");
                Debug.WriteLine($"Error adding user: {ex.Message}");
                TempData["ErrorMessage"] = $"Error adding user: {ex.Message}";
            }

            return View(model);
        }

        [HttpGet]
        public JsonResult GetSystemHealth()
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    var healthStatus = new
                    {
                        Database = "Connected",
                        Users = GetCount(connection, "SELECT COUNT(*) FROM users"),
                        ActiveSessions = 1,
                        ServerTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Uptime = "24h"
                    };

                    return Json(new { success = true, data = healthStatus });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetEventStats()
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    var eventStats = new
                    {
                        TotalEvents = SafeGetInt(connection, "SELECT COUNT(*) FROM events WHERE IsDeleted = 0"),
                        UpcomingEvents = SafeGetInt(connection, "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND EventDate >= CURDATE()"),
                        TodayEvents = SafeGetInt(connection, "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND DATE(EventDate) = CURDATE()"),

                        ScheduledEvents = SafeGetInt(connection, "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND Status = 'Scheduled'"),
                        OngoingEvents = SafeGetInt(connection, "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND Status = 'Ongoing'"),
                        CompletedEvents = SafeGetInt(connection, "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND Status = 'Completed'"),
                        CancelledEvents = SafeGetInt(connection, "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND Status = 'Cancelled'"),

                        MedicalCount = SafeGetInt(connection, "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND EventType = 'Medical Mission'"),
                        AssistanceCount = SafeGetInt(connection, "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND EventType = 'Assistance Program'"),
                        CommunityCount = SafeGetInt(connection, "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND EventType = 'Community Gathering'"),

                        TotalAttendance = SafeGetInt(connection, "SELECT SUM(AttendanceCount) FROM events WHERE IsDeleted = 0"),
                        TotalCapacity = SafeGetInt(connection, "SELECT SUM(MaxCapacity) FROM events WHERE IsDeleted = 0 AND MaxCapacity IS NOT NULL"),

                        RecentEvents = SafeGetInt(connection, "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY)")
                    };

                    return Json(new { success = true, data = eventStats });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult CheckSeniorsData()
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    var cmd = new MySqlCommand("SELECT COUNT(*) FROM seniors", connection);
                    var total = cmd.ExecuteScalar();

                    var activeCmd = new MySqlCommand("SELECT COUNT(*) FROM seniors WHERE Status = 'Active'", connection);
                    var active = activeCmd.ExecuteScalar();

                    var archivedCmd = new MySqlCommand("SELECT COUNT(*) FROM seniors WHERE Status = 'Archived'", connection);
                    var archived = archivedCmd.ExecuteScalar();

                    var maleCmd = new MySqlCommand("SELECT COUNT(*) FROM seniors WHERE Gender = 'Male'", connection);
                    var male = maleCmd.ExecuteScalar();

                    var femaleCmd = new MySqlCommand("SELECT COUNT(*) FROM seniors WHERE Gender = 'Female'", connection);
                    var female = femaleCmd.ExecuteScalar();

                    var recentCmd = new MySqlCommand("SELECT COUNT(*) FROM seniors WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY)", connection);
                    var recent = recentCmd.ExecuteScalar();

                    return Content($"Total: {total}, Active: {active}, Archived: {archived}, Male: {male}, Female: {female}, Recent (7 days): {recent}");
                }
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}\n\nStack Trace: {ex.StackTrace}");
            }
        }
    }
}