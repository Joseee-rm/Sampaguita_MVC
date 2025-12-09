using MySql.Data.MySqlClient;
using SeniorManagement.Helpers;
using SeniorManagement.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace SeniorManagement.Repositories
{
    public class MonthlyContributionRepository : IMonthlyContributionRepository
    {
        private readonly DatabaseHelper _dbHelper;

        public MonthlyContributionRepository(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        public async Task<List<MonthlyContribution>> GetMonthlyContributionsAsync(int month, int year)
        {
            var contributions = new List<MonthlyContribution>();

            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                // Create entries for current month if they don't exist
                await CreateMonthlyEntriesAsync(month, year);

                string query = @"
                    SELECT mc.Id, mc.SeniorId, mc.Month, mc.Year, mc.IsPaid, mc.PaidDate, mc.CreatedAt,
                           s.FirstName, s.LastName, s.MiddleInitial, s.Zone, s.Status
                    FROM monthly_contributions mc
                    JOIN seniors s ON mc.SeniorId = s.Id
                    WHERE mc.Month = @Month AND mc.Year = @Year
                    ORDER BY s.LastName, s.FirstName";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Month", month);
                    cmd.Parameters.AddWithValue("@Year", year);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            contributions.Add(new MonthlyContribution
                            {
                                Id = reader.GetInt32("Id"),
                                SeniorId = reader.GetInt32("SeniorId"),
                                Month = reader.GetInt32("Month"),
                                Year = reader.GetInt32("Year"),
                                IsPaid = reader.GetBoolean("IsPaid"),
                                PaidDate = reader.IsDBNull("PaidDate") ? null : reader.GetDateTime("PaidDate"),
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                FirstName = reader.GetString("FirstName"),
                                LastName = reader.GetString("LastName"),
                                MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                Zone = reader.GetInt32("Zone"),
                                Status = reader.GetString("Status"),
                                FullName = $"{reader.GetString("LastName")}, {reader.GetString("FirstName")} {reader["MiddleInitial"]?.ToString()}".Trim()
                            });
                        }
                    }
                }
            }

            return contributions;
        }

        public async Task<bool> TogglePaymentAsync(int contributionId)
        {
            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = @"
                    UPDATE monthly_contributions 
                    SET IsPaid = NOT IsPaid,
                        PaidDate = IF(IsPaid = FALSE, NOW(), NULL)
                    WHERE Id = @Id";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", contributionId);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
        }

        public async Task<int> CreateMonthlyEntriesAsync(int month, int year)
        {
            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                // First, check if we have active seniors
                string checkActiveSeniors = "SELECT COUNT(*) FROM seniors WHERE Status = 'Active'";
                int activeSeniorsCount;

                using (var checkCmd = new MySqlCommand(checkActiveSeniors, conn))
                {
                    activeSeniorsCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                }

                if (activeSeniorsCount == 0)
                {
                    return 0; // No active seniors to create entries for
                }

                // Create entries for all active seniors
                string insertQuery = @"
                    INSERT INTO monthly_contributions (SeniorId, Month, Year, IsPaid)
                    SELECT s.Id, @Month, @Year, FALSE
                    FROM seniors s
                    WHERE s.Status = 'Active'
                    AND NOT EXISTS (
                        SELECT 1 
                        FROM monthly_contributions mc 
                        WHERE mc.SeniorId = s.Id 
                        AND mc.Month = @Month 
                        AND mc.Year = @Year
                    )";

                using (var cmd = new MySqlCommand(insertQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Month", month);
                    cmd.Parameters.AddWithValue("@Year", year);

                    return await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<int> GetNewSeniorsCountAsync(int month, int year)
        {
            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT COUNT(DISTINCT s.Id) 
                    FROM seniors s
                    LEFT JOIN monthly_contributions mc ON s.Id = mc.SeniorId 
                        AND mc.Year < @Year 
                        OR (mc.Year = @Year AND mc.Month < @Month)
                    WHERE s.Status = 'Active' 
                    AND mc.SeniorId IS NULL";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Month", month);
                    cmd.Parameters.AddWithValue("@Year", year);

                    var result = await cmd.ExecuteScalarAsync();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
        }

        public async Task<List<ContributionLog>> GetContributionLogsAsync()
        {
            var logs = new List<ContributionLog>();

            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT * FROM contribution_logs 
                    ORDER BY Year DESC, 
                    FIELD(Month, 'January', 'February', 'March', 'April', 'May', 'June', 
                          'July', 'August', 'September', 'October', 'November', 'December') DESC";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            logs.Add(new ContributionLog
                            {
                                Id = reader.GetInt32("Id"),
                                Month = reader.GetString("Month"),  // Fixed: "Month"
                                Year = reader.GetInt32("Year"),     // Fixed: "Year"
                                FilePath = reader["FilePath"]?.ToString() ?? "",
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                Notes = reader["Notes"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            return logs;
        }

        public async Task<ContributionLog> GetContributionLogAsync(string month, int year)
        {
            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = "SELECT * FROM contribution_logs WHERE Month = @Month AND Year = @Year";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Month", month);  // Fixed: "@Month"
                    cmd.Parameters.AddWithValue("@Year", year);    // Fixed: "@Year"

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new ContributionLog
                            {
                                Id = reader.GetInt32("Id"),
                                Month = reader.GetString("Month"),  // Fixed
                                Year = reader.GetInt32("Year"),     // Fixed
                                FilePath = reader["FilePath"]?.ToString() ?? "",
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                Notes = reader["Notes"]?.ToString() ?? ""
                            };
                        }
                    }
                }
            }

            return null;
        }

        public async Task<ContributionLog> SaveContributionLogAsync(string month, int year, string filePath, string notes)
        {
            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = @"
                    INSERT INTO contribution_logs (Month, Year, FilePath, Notes)
                    VALUES (@Month, @Year, @FilePath, @Notes)
                    ON DUPLICATE KEY UPDATE 
                    FilePath = @FilePath, 
                    Notes = @Notes";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Month", month);      // Fixed
                    cmd.Parameters.AddWithValue("@Year", year);        // Fixed
                    cmd.Parameters.AddWithValue("@FilePath", filePath);
                    cmd.Parameters.AddWithValue("@Notes", notes);

                    await cmd.ExecuteNonQueryAsync();

                    return await GetContributionLogAsync(month, year);
                }
            }
        }

        public async Task<List<MonthlyContribution>> GetContributionsForExportAsync(int month, int year)
        {
            var contributions = new List<MonthlyContribution>();

            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT mc.Id, mc.SeniorId, mc.Month, mc.Year, mc.IsPaid, mc.PaidDate,
                           s.FirstName, s.LastName, s.MiddleInitial, s.Zone, s.Status
                    FROM monthly_contributions mc
                    JOIN seniors s ON mc.SeniorId = s.Id
                    WHERE mc.Month = @Month AND mc.Year = @Year
                    ORDER BY s.Zone, s.LastName, s.FirstName";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Month", month);
                    cmd.Parameters.AddWithValue("@Year", year);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            contributions.Add(new MonthlyContribution
                            {
                                Id = reader.GetInt32("Id"),
                                SeniorId = reader.GetInt32("SeniorId"),
                                Month = reader.GetInt32("Month"),
                                Year = reader.GetInt32("Year"),
                                IsPaid = reader.GetBoolean("IsPaid"),
                                PaidDate = reader.IsDBNull("PaidDate") ? null : reader.GetDateTime("PaidDate"),
                                FirstName = reader.GetString("FirstName"),
                                LastName = reader.GetString("LastName"),
                                MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                Zone = reader.GetInt32("Zone"),
                                Status = reader.GetString("Status"),
                                FullName = $"{reader.GetString("LastName")}, {reader.GetString("FirstName")} {reader["MiddleInitial"]?.ToString()}".Trim()
                            });
                        }
                    }
                }
            }

            return contributions;
        }
    }
}