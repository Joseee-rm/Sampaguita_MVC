using MySql.Data.MySqlClient;
using SeniorManagement.Models;
using System.Diagnostics;

namespace SeniorManagement.Helpers
{
    public class DatabaseHelper
    {
        private readonly IConfiguration _configuration;

        public DatabaseHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public MySqlConnection GetConnection()
        {
            // Change this line to use "DefaultConnection" instead of "MySqlConnection"
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            Debug.WriteLine($"DatabaseHelper: Connection string: {connectionString}");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
            }

            return new MySqlConnection(connectionString);
        }
    }
}