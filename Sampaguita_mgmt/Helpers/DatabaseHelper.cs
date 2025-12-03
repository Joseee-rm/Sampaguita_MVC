using MySql.Data.MySqlClient;
using SeniorManagement.Models;

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
            string connectionString = _configuration.GetConnectionString("MySqlConnection");
            return new MySqlConnection(connectionString);
        }
    }
}