using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using SeniorManagement.Models;
using SeniorManagement.Helpers;
using MySql.Data.MySqlClient;
using System.ComponentModel.DataAnnotations;

namespace SeniorManagement.Controllers
{
    public class AccountController : BaseController
    {
        private readonly AuthHelper _authHelper;
        private readonly DatabaseHelper _dbHelper;
        private readonly ActivityHelper _activityHelper;

        public AccountController(AuthHelper authHelper, DatabaseHelper dbHelper, ActivityHelper activityHelper)
        {
            _authHelper = authHelper;
            _dbHelper = dbHelper;
            _activityHelper = activityHelper;
        }

        public IActionResult Login()
        {
            // If user is already logged in, redirect based on role
            if (User.Identity.IsAuthenticated)
            {
                var isAdmin = User.Claims.FirstOrDefault(c => c.Type == "IsAdmin")?.Value == "True";

                // Debug: Show where it's trying to go
                if (isAdmin)
                    return RedirectToAction("Dashboard", "Admin");  // Correct
                else
                    return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            System.Diagnostics.Debug.WriteLine($"Login attempt: {model.Username}");

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fill in all required fields.";
                return View(model);
            }

            // Additional validation
            if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
            {
                TempData["ErrorMessage"] = "Username and password are required.";
                return View(model);
            }

            // NEW: Validate UserRole
            if (string.IsNullOrEmpty(model.UserRole))
            {
                TempData["ErrorMessage"] = "Please select an access level.";
                return View(model);
            }

            // NEW: Validate user type based on selected role
            var validationError = ValidateUserType(model.Username, model.UserRole);
            if (!string.IsNullOrEmpty(validationError))
            {
                TempData["ErrorMessage"] = validationError;
                return View(model);
            }

            try
            {
                // Check if user exists and is active
                var user = CheckUserStatus(model.Username);

                if (user == null)
                {
                    // Log failed login attempt
                    await _activityHelper.LogActivityAsync(
                        "Failed Login",
                        $"Failed login attempt for username: {model.Username}"
                    );

                    ModelState.AddModelError("", "Invalid username or password");
                    TempData["ErrorMessage"] = "Invalid username or password.";
                    return View(model);
                }

                if (!user.IsActive)
                {
                    // Log inactive account login attempt
                    await _activityHelper.LogActivityAsync(
                        "Failed Login",
                        $"Inactive account login attempt: {model.Username}"
                    );

                    ModelState.AddModelError("", "This account is deactivated");
                    TempData["ErrorMessage"] = "Your account has been deactivated. Please contact an administrator.";
                    return View(model);
                }

                // NEW: Additional role validation
                if (!IsValidRole(user, model.UserRole))
                {
                    // Log role mismatch attempt
                    await _activityHelper.LogActivityAsync(
                        "Failed Login",
                        $"Role mismatch for {model.Username}. Selected: {model.UserRole}, Actual: {user.Role}"
                    );

                    ModelState.AddModelError("", "Invalid access level selected");
                    TempData["ErrorMessage"] = "The selected access level does not match your account type.";
                    return View(model);
                }

                // Authenticate user
                user = _authHelper.AuthenticateUser(model.Username, model.Password);

                if (user != null)
                {
                    // NEW: Final role validation
                    if (!IsValidRole(user, model.UserRole))
                    {
                        ModelState.AddModelError("", "Invalid access level selected");
                        TempData["ErrorMessage"] = "The selected access level does not match your account type.";
                        return View(model);
                    }

                    System.Diagnostics.Debug.WriteLine($"Login SUCCESS: {model.Username} - Role: {user.Role} - IsAdmin: {user.IsAdmin}");

                    // Log successful login
                    await _activityHelper.LogUserLoginAsync(model.Username);

                    // Create claims
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.GivenName, user.Name),
                        new Claim(ClaimTypes.Role, user.Role),
                        new Claim("IsAdmin", user.IsAdmin.ToString()),
                        new Claim("UserId", user.Id.ToString()),
                        new Claim("FullName", user.Name),
                        new Claim("UserRole", user.Role)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8), // 8-hour session
                        AllowRefresh = true
                    };

                    // Sign in
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    // Store user details in session
                    HttpContext.Session.SetInt32("UserId", user.Id);
                    HttpContext.Session.SetString("Username", user.Username);
                    HttpContext.Session.SetString("UserRole", user.Role);
                    HttpContext.Session.SetString("UserName", user.Name);
                    HttpContext.Session.SetString("IsAdmin", user.IsAdmin.ToString());

                    TempData["SuccessMessage"] = $"Welcome back, {user.Name}!";

                    // Check if user is trying to access admin area without admin privileges
                    if (!string.IsNullOrEmpty(returnUrl) && returnUrl.Contains("/Admin") && !user.IsAdmin)
                    {
                        await _activityHelper.LogActivityAsync(
                            "Access Denied",
                            $"{user.Username} attempted to access admin area without privileges"
                        );

                        TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                        return RedirectToAction("Index", "Home");
                    }

                    // Redirect based on role
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    else
                    {
                        if (user.IsAdmin)
                        {
                            return RedirectToAction("Dashboard", "Admin");
                        }
                        else
                        {
                            return RedirectToAction("Index", "Home");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Login FAILED: {model.Username}");

                    // Log failed login attempt with wrong password
                    await _activityHelper.LogActivityAsync(
                        "Failed Login",
                        $"Wrong password for username: {model.Username}"
                    );

                    ModelState.AddModelError("", "Invalid username or password");
                    TempData["ErrorMessage"] = "Invalid username or password.";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");

                // Log login error
                await _activityHelper.LogErrorAsync(ex.Message, "Login");

                ModelState.AddModelError("", "An error occurred during login. Please try again.");
                TempData["ErrorMessage"] = "An error occurred during login. Please try again.";
            }
            return View(model);
        }

        // NEW: Validate user type based on selected role
        private string ValidateUserType(string username, string selectedRole)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT IsAdmin, Role, IsActive FROM users WHERE Username = @Username";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                bool isAdmin = reader.GetBoolean("IsAdmin");
                                string actualRole = reader.GetString("Role");
                                bool isActive = reader.GetBoolean("IsActive");

                                // Check if account is active
                                if (!isActive)
                                {
                                    return "Your account has been deactivated. Please contact an administrator.";
                                }

                                // Validate role selection
                                if (selectedRole == "admin" && !isAdmin)
                                {
                                    return "You do not have administrator privileges.";
                                }
                                else if (selectedRole == "staff" && isAdmin)
                                {
                                    return "Please select 'Administrator' access level for admin accounts.";
                                }

                                return string.Empty;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ValidateUserType error: {ex.Message}");
                return "Error validating user type. Please try again.";
            }
            return "Invalid username or access level.";
        }

        // NEW: Check if the selected role matches user's actual role
        private bool IsValidRole(User user, string selectedRole)
        {
            if (selectedRole == "admin" && !user.IsAdmin)
                return false;

            if (selectedRole == "staff" && user.IsAdmin)
                return false;

            return true;
        }

        // Check user status before authentication
        private User CheckUserStatus(string username)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT Id, Name, Username, Password, Role, IsAdmin, IsActive FROM users WHERE Username = @Username";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new User
                                {
                                    Id = reader.GetInt32("Id"),
                                    Name = reader.GetString("Name"),
                                    Username = reader.GetString("Username"),
                                    Password = reader.GetString("Password"),
                                    Role = reader.GetString("Role"),
                                    IsAdmin = reader.GetBoolean("IsAdmin"),
                                    IsActive = reader.GetBoolean("IsActive")
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckUserStatus error: {ex.Message}");
            }
            return null;
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userName = HttpContext.Session.GetString("Username") ?? "Unknown";

            // Log the logout activity FIRST
            await _activityHelper.LogUserLogoutAsync(userName);

            // Then sign out and clear session
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Login", "Account");
        }

        // GET: Account/Logout (for convenience)
        public async Task<IActionResult> LogoutGet()
        {
            var userName = HttpContext.Session.GetString("Username") ?? "Unknown";

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            // Log logout activity
            await _activityHelper.LogUserLogoutAsync(userName);

            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Login", "Account");
        }

        // Access Denied Page
        public IActionResult AccessDenied()
        {
            // Log access denied
            var userName = HttpContext.Session.GetString("Username") ?? "Unknown";
            var userRole = HttpContext.Session.GetString("UserRole") ?? "Unknown";
            var requestedPath = HttpContext.Request.Path;

            _ = _activityHelper.LogActivityAsync(
                "Access Denied",
                $"{userName} ({userRole}) attempted to access {requestedPath}"
            );

            return View();
        }

        // NEW: Method to track failed login attempts
        private async void LogFailedLoginAttempt(string username, string ipAddress)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"INSERT INTO login_attempts (Username, AttemptTime, Success, IpAddress) 
                                   VALUES (@Username, NOW(), FALSE, @IpAddress)";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);
                        cmd.Parameters.AddWithValue("@IpAddress", ipAddress);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogFailedLoginAttempt error: {ex.Message}");
                await _activityHelper.LogErrorAsync(ex.Message, "Log Failed Login Attempt");
            }
        }

        // NEW: Check if account is locked due to too many failed attempts
        private bool IsAccountLocked(string username)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT COUNT(*) FROM login_attempts 
                                   WHERE Username = @Username 
                                   AND Success = FALSE 
                                   AND AttemptTime > DATE_SUB(NOW(), INTERVAL 15 MINUTE)";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);
                        int failedAttempts = Convert.ToInt32(cmd.ExecuteScalar());

                        // Lock account after 5 failed attempts
                        return failedAttempts >= 5;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsAccountLocked error: {ex.Message}");
                return false;
            }
        }

        // TEMPORARY: Reset passwords method - call this URL to fix login issues
        [HttpGet]
        public async Task<IActionResult> ResetPasswords()
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Clear existing users
                    using (var cmd = new MySqlCommand("DELETE FROM users", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // Add admin user with correct hash
                    string adminHash = BCrypt.Net.BCrypt.HashPassword("admin123");
                    using (var cmd = new MySqlCommand(
                        @"INSERT INTO users (Name, Username, Password, Role, IsAdmin, IsActive, CreatedAt) 
                          VALUES ('Administrator', 'admin', @Password, 'Administrator', TRUE, TRUE, NOW())", connection))
                    {
                        cmd.Parameters.AddWithValue("@Password", adminHash);
                        cmd.ExecuteNonQuery();
                    }

                    // Add staff user with correct hash
                    string staffHash = BCrypt.Net.BCrypt.HashPassword("staff123");
                    using (var cmd = new MySqlCommand(
                        @"INSERT INTO users (Name, Username, Password, Role, IsAdmin, IsActive, CreatedAt) 
                          VALUES ('Staff User', 'staff', @Password, 'Staff', FALSE, TRUE, NOW())", connection))
                    {
                        cmd.Parameters.AddWithValue("@Password", staffHash);
                        cmd.ExecuteNonQuery();
                    }

                    // Add inactive user for testing
                    string inactiveHash = BCrypt.Net.BCrypt.HashPassword("inactive123");
                    using (var cmd = new MySqlCommand(
                        @"INSERT INTO users (Name, Username, Password, Role, IsAdmin, IsActive, CreatedAt) 
                          VALUES ('Inactive User', 'inactive', @Password, 'Staff', FALSE, FALSE, NOW())", connection))
                    {
                        cmd.Parameters.AddWithValue("@Password", inactiveHash);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Log this activity
                await _activityHelper.LogActivityAsync(
                    "System",
                    "Reset passwords - Created test users"
                );

                TempData["SuccessMessage"] = "Test users created successfully!<br>" +
                    "• Admin: admin/admin123 (Active)<br>" +
                    "• Staff: staff/staff123 (Active)<br>" +
                    "• Inactive: inactive/inactive123 (Inactive)";
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Reset Passwords");
                TempData["ErrorMessage"] = $"Error resetting passwords: {ex.Message}";
            }

            return RedirectToAction("Login");
        }
    }
}