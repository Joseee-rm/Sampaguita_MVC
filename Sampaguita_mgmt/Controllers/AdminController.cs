using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SeniorManagement.Models;
using SeniorManagement.Helpers;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Diagnostics;

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

        // Test connection endpoint
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

        // Manage Users Page
        public IActionResult ManageUsers()
        {
            // Check if user is admin
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            var users = GetAllUsers();
            return View(users);
        }

        // Get all users from database
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

        // Toggle user active status
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            try
            {
                // Check if user is admin
                if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
                {
                    TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                    return RedirectToAction("Index", "Home");
                }

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Get current status
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

                    // Toggle status
                    string updateQuery = "UPDATE users SET IsActive = @IsActive WHERE Id = @Id";
                    using (var updateCmd = new MySqlCommand(updateQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@IsActive", !currentStatus);
                        updateCmd.Parameters.AddWithValue("@Id", id);
                        updateCmd.ExecuteNonQuery();
                    }

                    // Log the activity
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

        // Edit User - GET
        [HttpGet]
        public IActionResult EditUser(int id)
        {
            // Check if user is admin
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

        // Edit User - POST (Fixed Version)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User model)
        {
            Debug.WriteLine($"EditUser POST called with ID: {model.Id}");
            Debug.WriteLine($"Name: {model.Name}, Username: {model.Username}, Role: {model.Role}, IsAdmin: {model.IsAdmin}");

            // Check if user is admin
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            // Basic validation
            if (string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Role))
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

                    // Check for duplicate username (excluding current user)
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

                    // Ensure Role and IsAdmin are in sync
                    if (model.Role == "Administrator")
                    {
                        model.IsAdmin = true;
                    }
                    else if (model.Role == "Staff")
                    {
                        model.IsAdmin = false;
                    }

                    // Update user
                    string updateQuery = @"UPDATE users 
                                         SET Name = @Name, 
                                             Username = @Username, 
                                             Role = @Role, 
                                             IsAdmin = @IsAdmin
                                         WHERE Id = @Id";

                    Debug.WriteLine($"Executing update query: {updateQuery}");

                    using (var cmd = new MySqlCommand(updateQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@Name", model.Name);
                        cmd.Parameters.AddWithValue("@Username", model.Username);
                        cmd.Parameters.AddWithValue("@Role", model.Role);
                        cmd.Parameters.AddWithValue("@IsAdmin", model.IsAdmin);
                        cmd.Parameters.AddWithValue("@Id", model.Id);

                        Debug.WriteLine($"Parameters set - Name: {model.Name}, Username: {model.Username}, Role: {model.Role}, IsAdmin: {model.IsAdmin}, Id: {model.Id}");

                        int rowsAffected = cmd.ExecuteNonQuery();
                        Debug.WriteLine($"Rows affected: {rowsAffected}");

                        if (rowsAffected > 0)
                        {
                            // Log the activity
                            await _activityHelper.LogActivityAsync(
                                "Edit User",
                                $"Updated user '{model.Name}' ({model.Username}) - Role: {model.Role}, Admin: {model.IsAdmin}"
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

        // Reset User Password - GET (Manual password entry form)
        [HttpGet]
        public IActionResult ResetPassword(int id)
        {
            // Check if user is admin
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

        // Reset User Password - POST (Process manual password entry)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            try
            {
                // Check if user is admin
                if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
                {
                    TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                    return RedirectToAction("Index", "Home");
                }

                // Validate input
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

                // Optional: Add password strength validation
                if (model.NewPassword.Length < 6)
                {
                    ModelState.AddModelError("NewPassword", "Password must be at least 6 characters long.");
                    return View(model);
                }

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Hash the new password
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

        // Quick Reset User Password (Sets to default password)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickResetPassword(int id)
        {
            try
            {
                // Check if user is admin
                if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
                {
                    TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                    return RedirectToAction("Index", "Home");
                }

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Default password: "password123"
                    string defaultPassword = AuthHelper.HashPassword("password123");

                    string query = "UPDATE users SET Password = @Password WHERE Id = @Id";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Password", defaultPassword);
                        cmd.Parameters.AddWithValue("@Id", id);
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            var user = GetUserById(id);
                            if (user != null)
                            {
                                await _activityHelper.LogActivityAsync(
                                    "Quick Reset Password",
                                    $"Password quickly reset to default for user '{user.Name}' ({user.Username})"
                                );
                            }

                            TempData["SuccessMessage"] = "Password reset to default successfully! Default password: password123";
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

        // Get user by ID
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

        // Add New User - GET
        [HttpGet]
        public IActionResult AddUser()
        {
            // Check if user is admin
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

        // Dashboard Action - Add this method to AdminController
        [Authorize(Roles = "Administrator")]
        public IActionResult Dashboard()
        {
            // Check if user is admin
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            // Get dashboard statistics
            var stats = GetAdminDashboardStats();
            ViewBag.DashboardStats = stats;

            // Get recent activities
            var activities = GetRecentAdminActivities();
            ViewBag.RecentActivities = activities;

            // Get recent users
            var recentUsers = GetRecentUsers();
            ViewBag.RecentUsers = recentUsers;

            return View();
        }

        private dynamic GetAdminDashboardStats()
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    var stats = new
                    {
                        TotalUsers = GetCount(connection, "SELECT COUNT(*) FROM users"),
                        ActiveUsers = GetCount(connection, "SELECT COUNT(*) FROM users WHERE IsActive = TRUE"),
                        AdminUsers = GetCount(connection, "SELECT COUNT(*) FROM users WHERE IsAdmin = TRUE"),
                        StaffUsers = GetCount(connection, "SELECT COUNT(*) FROM users WHERE IsAdmin = FALSE"),
                        TotalSeniors = GetCount(connection, "SELECT COUNT(*) FROM seniors WHERE IsDeleted = 0"),
                        RecentRegistrations = GetCount(connection, "SELECT COUNT(*) FROM users WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 7 DAY)"),
                        SystemStatus = "Operational",
                        LastBackup = DateTime.Now.AddDays(-1).ToString("MMM dd, yyyy")
                    };

                    return stats;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting admin stats: {ex.Message}");
                return new
                {
                    TotalUsers = 0,
                    ActiveUsers = 0,
                    AdminUsers = 0,
                    StaffUsers = 0,
                    TotalSeniors = 0,
                    RecentRegistrations = 0,
                    SystemStatus = "Error",
                    LastBackup = "N/A"
                };
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

        private List<Models.ActivityLog> GetRecentAdminActivities()
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
                Debug.WriteLine($"Error getting activities: {ex.Message}");
            }

            return activities;
        }

        private List<User> GetRecentUsers()
        {
            var users = new List<User>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"SELECT Id, Name, Username, Role, IsAdmin, IsActive, CreatedAt
                           FROM users 
                           ORDER BY CreatedAt DESC 
                           LIMIT 5";

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
                                CreatedAt = reader.GetDateTime("CreatedAt")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting recent users: {ex.Message}");
            }

            return users;
        }

        // Add New User - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(User model)
        {
            // Check if user is admin
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            // Force new users to be Staff only
            model.Role = "Staff";
            model.IsAdmin = false;

            // Basic validation
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

                    // Check if username already exists
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

                    // Default password: "password123"
                    string hashedPassword = AuthHelper.HashPassword("password123");

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
                            // Log the activity
                            await _activityHelper.LogActivityAsync(
                                "Add User",
                                $"Added new user '{model.Name}' ({model.Username}) as {model.Role}"
                            );

                            TempData["SuccessMessage"] = "User added successfully! Default password: password123";
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

        // Method to get system health status
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
                        ActiveSessions = 1, // You can implement session tracking
                        ServerTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Uptime = "24h" // You can implement uptime tracking
                    };

                    return Json(new { success = true, data = healthStatus });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}