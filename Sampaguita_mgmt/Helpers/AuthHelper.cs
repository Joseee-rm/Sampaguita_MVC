using SeniorManagement.Models;
using MySql.Data.MySqlClient;

namespace SeniorManagement.Helpers
{
    public class AuthHelper
    {
        private readonly DatabaseHelper _dbHelper;

        public AuthHelper(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        // Add this public method to access the database connection
        public MySqlConnection GetConnection()
        {
            return _dbHelper.GetConnection();
        }

        public User AuthenticateUser(string username, string password)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"AuthHelper: Attempting to authenticate {username}");

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT Id, Name, Username, Password, Role, IsAdmin, IsActive 
                                   FROM users 
                                   WHERE Username = @Username AND IsActive = TRUE";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string hashedPassword = reader["Password"].ToString();
                                bool isActive = reader.GetBoolean("IsActive");
                                string dbUsername = reader["Username"].ToString();

                                System.Diagnostics.Debug.WriteLine($"AuthHelper: Found user {dbUsername}");
                                System.Diagnostics.Debug.WriteLine($"AuthHelper: User active status: {isActive}");

                                // Check if user is active
                                if (!isActive)
                                {
                                    System.Diagnostics.Debug.WriteLine($"AuthHelper: User {username} is inactive");
                                    return null;
                                }

                                // Verify password using BCrypt
                                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, hashedPassword);
                                System.Diagnostics.Debug.WriteLine($"AuthHelper: Password valid: {isPasswordValid}");

                                if (isPasswordValid)
                                {
                                    return new User
                                    {
                                        Id = reader.GetInt32("Id"),
                                        Name = reader.GetString("Name"),
                                        Username = dbUsername,
                                        Password = hashedPassword,
                                        Role = reader.GetString("Role"),
                                        IsAdmin = reader.GetBoolean("IsAdmin"),
                                        IsActive = isActive
                                    };
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"AuthHelper: User {username} not found or inactive");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AuthHelper: Error - {ex.Message}");
            }
            return null;
        }

        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool IsUserAdmin(User user)
        {
            return user?.IsAdmin == true;
        }

        public User AuthenticateUserWithRole(string username, string password, string requestedRole)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"AuthHelper: Attempting to authenticate {username} with role: {requestedRole}");

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT Id, Name, Username, Password, Role, IsAdmin, IsActive 
                           FROM users 
                           WHERE Username = @Username AND IsActive = TRUE";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string hashedPassword = reader["Password"].ToString();
                                bool isActive = reader.GetBoolean("IsActive");
                                string dbUsername = reader["Username"].ToString();
                                bool isAdmin = reader.GetBoolean("IsAdmin");
                                string userRole = reader["Role"].ToString();

                                System.Diagnostics.Debug.WriteLine($"AuthHelper: Found user {dbUsername}");
                                System.Diagnostics.Debug.WriteLine($"AuthHelper: User active status: {isActive}");
                                System.Diagnostics.Debug.WriteLine($"AuthHelper: User is admin: {isAdmin}");
                                System.Diagnostics.Debug.WriteLine($"AuthHelper: User role: {userRole}");

                                // Check if user is active
                                if (!isActive)
                                {
                                    System.Diagnostics.Debug.WriteLine($"AuthHelper: User {username} is inactive");
                                    return null;
                                }

                                // Validate role
                                if (requestedRole == "admin" && !isAdmin)
                                {
                                    System.Diagnostics.Debug.WriteLine($"AuthHelper: User {username} attempted admin access without admin privileges");
                                    return null;
                                }

                                if (requestedRole == "staff" && isAdmin)
                                {
                                    System.Diagnostics.Debug.WriteLine($"AuthHelper: Admin user {username} attempted staff access");
                                    // Allow admin to login as staff if needed, or return null to restrict
                                    // return null; // Uncomment to restrict admin from logging in as staff
                                }

                                // Verify password using BCrypt
                                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, hashedPassword);
                                System.Diagnostics.Debug.WriteLine($"AuthHelper: Password valid: {isPasswordValid}");

                                if (isPasswordValid)
                                {
                                    return new User
                                    {
                                        Id = reader.GetInt32("Id"),
                                        Name = reader.GetString("Name"),
                                        Username = dbUsername,
                                        Password = hashedPassword,
                                        Role = userRole,
                                        IsAdmin = isAdmin,
                                        IsActive = isActive
                                    };
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"AuthHelper: User {username} not found or inactive");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AuthHelper: Error - {ex.Message}");
            }
            return null;
        }

        // Check if user exists and is active
        public bool UserExistsAndActive(string username)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM users WHERE Username = @Username AND IsActive = TRUE";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UserExistsAndActive error: {ex.Message}");
                return false;
            }
        }
    }
}