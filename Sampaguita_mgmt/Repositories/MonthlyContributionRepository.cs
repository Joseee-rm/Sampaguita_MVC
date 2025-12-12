using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using SeniorManagement.Helpers;
using SeniorManagement.Models;

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
                                Month = reader.GetString("Month"),
                                Year = reader.GetInt32("Year"),
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
                    cmd.Parameters.AddWithValue("@Month", month);
                    cmd.Parameters.AddWithValue("@Year", year);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new ContributionLog
                            {
                                Id = reader.GetInt32("Id"),
                                Month = reader.GetString("Month"),
                                Year = reader.GetInt32("Year"),
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
                    cmd.Parameters.AddWithValue("@Month", month);
                    cmd.Parameters.AddWithValue("@Year", year);
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
                           s.FirstName, s.LastName, s.MiddleInitial, s.Zone, s.Status,
                           s.SeniorId as SCCN
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

        // ==================== UPDATED PENSION METHODS ====================

        public async Task<List<PensionContribution>> GetMonthlyPensionsAsync(int month, int year, string pensionType = null)
        {
            var pensions = new List<PensionContribution>();

            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                // Create pension entries for current month if they don't exist
                await CreateMonthlyPensionEntriesAsync(month, year);

                string query = @"
                    SELECT 
                        pc.Id,
                        pc.SeniorId,
                        pc.Month,
                        pc.Year,
                        pc.IsClaimed,
                        pc.ClaimedDate,
                        pc.CreatedAt,
                        CONCAT(s.LastName, ', ', s.FirstName, ' ', COALESCE(s.MiddleInitial, '')) as FullName,
                        s.FirstName,
                        s.LastName,
                        s.MiddleInitial,
                        s.Zone,
                        s.Status,
                        COALESCE(s.PensionType, '') as PensionType,
                        COALESCE(s.Age, 0) as Age,
                        s.SeniorId as SCCN
                    FROM pension_contributions pc
                    INNER JOIN seniors s ON pc.SeniorId = s.Id
                    WHERE pc.Month = @Month 
                        AND pc.Year = @Year
                        AND s.Status = 'Active'";

                // Add pension type filter if specified
                if (!string.IsNullOrEmpty(pensionType))
                {
                    if (pensionType == "No Pension")
                    {
                        query += " AND (s.PensionType IS NULL OR s.PensionType = '')";
                    }
                    else
                    {
                        query += " AND s.PensionType = @PensionType";
                    }
                }

                query += " ORDER BY s.LastName, s.FirstName";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Month", month);
                    cmd.Parameters.AddWithValue("@Year", year);

                    if (!string.IsNullOrEmpty(pensionType) && pensionType != "No Pension")
                    {
                        cmd.Parameters.AddWithValue("@PensionType", pensionType);
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            pensions.Add(new PensionContribution
                            {
                                Id = reader.GetInt32("Id"),
                                SeniorId = reader.GetInt32("SeniorId"),
                                Month = reader.GetInt32("Month"),
                                Year = reader.GetInt32("Year"),
                                IsClaimed = reader.GetBoolean("IsClaimed"),
                                ClaimedDate = reader.IsDBNull("ClaimedDate") ? null : reader.GetDateTime("ClaimedDate"),
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                FirstName = reader.GetString("FirstName"),
                                LastName = reader.GetString("LastName"),
                                MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                Zone = reader.GetInt32("Zone"),
                                Status = reader.GetString("Status"),
                                PensionType = reader["PensionType"]?.ToString() ?? "",
                                Age = reader.GetInt32("Age"),
                                FullName = reader.GetString("FullName").Trim()
                            });
                        }
                    }
                }
            }

            return pensions;
        }

        public async Task<List<PensionContribution>> GetPensionsForExportAsync(int month, int year, string pensionType = null)
        {
            var pensions = new List<PensionContribution>();

            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        pc.Id,
                        pc.SeniorId,
                        pc.Month,
                        pc.Year,
                        pc.IsClaimed,
                        pc.ClaimedDate,
                        pc.CreatedAt,
                        CONCAT(s.LastName, ', ', s.FirstName, ' ', COALESCE(s.MiddleInitial, '')) as FullName,
                        s.FirstName,
                        s.LastName,
                        s.MiddleInitial,
                        s.Zone,
                        s.Status,
                        COALESCE(s.PensionType, '') as PensionType,
                        COALESCE(s.Age, 0) as Age,
                        s.SeniorId as SCCN
                    FROM pension_contributions pc
                    INNER JOIN seniors s ON pc.SeniorId = s.Id
                    WHERE pc.Month = @Month 
                        AND pc.Year = @Year";

                // Add pension type filter if specified
                if (!string.IsNullOrEmpty(pensionType))
                {
                    if (pensionType == "No Pension")
                    {
                        query += " AND (s.PensionType IS NULL OR s.PensionType = '')";
                    }
                    else
                    {
                        query += " AND s.PensionType = @PensionType";
                    }
                }

                query += " ORDER BY s.Zone, s.LastName, s.FirstName";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Month", month);
                    cmd.Parameters.AddWithValue("@Year", year);

                    if (!string.IsNullOrEmpty(pensionType) && pensionType != "No Pension")
                    {
                        cmd.Parameters.AddWithValue("@PensionType", pensionType);
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            pensions.Add(new PensionContribution
                            {
                                Id = reader.GetInt32("Id"),
                                SeniorId = reader.GetInt32("SeniorId"),
                                Month = reader.GetInt32("Month"),
                                Year = reader.GetInt32("Year"),
                                IsClaimed = reader.GetBoolean("IsClaimed"),
                                ClaimedDate = reader.IsDBNull("ClaimedDate") ? null : reader.GetDateTime("ClaimedDate"),
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                FirstName = reader.GetString("FirstName"),
                                LastName = reader.GetString("LastName"),
                                MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                Zone = reader.GetInt32("Zone"),
                                Status = reader.GetString("Status"),
                                PensionType = reader["PensionType"]?.ToString() ?? "",
                                Age = reader.GetInt32("Age"),
                                FullName = reader.GetString("FullName").Trim()
                            });
                        }
                    }
                }
            }

            return pensions;
        }

        public async Task<bool> TogglePensionClaimAsync(int id)
        {
            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = @"
                    UPDATE pension_contributions 
                    SET IsClaimed = NOT IsClaimed,
                        ClaimedDate = CASE 
                            WHEN IsClaimed = 0 THEN NOW() 
                            ELSE NULL 
                        END
                    WHERE Id = @Id";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
        }

        public async Task<PensionLog> SavePensionLogAsync(string month, int year, string filePath, string notes)
        {
            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                // Check if log already exists
                var existingLog = await GetPensionLogAsync(month, year);

                if (existingLog != null)
                {
                    string updateQuery = @"
                        UPDATE pension_logs 
                        SET FilePath = @FilePath, 
                            Notes = @Notes,
                            CreatedAt = NOW()
                        WHERE Month = @Month AND Year = @Year";

                    using (var cmd = new MySqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Month", month);
                        cmd.Parameters.AddWithValue("@Year", year);
                        cmd.Parameters.AddWithValue("@FilePath", filePath);
                        cmd.Parameters.AddWithValue("@Notes", notes);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    string insertQuery = @"
                        INSERT INTO pension_logs (Month, Year, FilePath, Notes, CreatedAt)
                        VALUES (@Month, @Year, @FilePath, @Notes, NOW())";

                    using (var cmd = new MySqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Month", month);
                        cmd.Parameters.AddWithValue("@Year", year);
                        cmd.Parameters.AddWithValue("@FilePath", filePath);
                        cmd.Parameters.AddWithValue("@Notes", notes);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                return await GetPensionLogAsync(month, year);
            }
        }

        public async Task<PensionLog> GetPensionLogAsync(string month, int year)
        {
            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = "SELECT * FROM pension_logs WHERE Month = @Month AND Year = @Year";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Month", month);
                    cmd.Parameters.AddWithValue("@Year", year);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new PensionLog
                            {
                                Id = reader.GetInt32("Id"),
                                Month = reader.GetString("Month"),
                                Year = reader.GetInt32("Year"),
                                FilePath = reader["FilePath"]?.ToString() ?? "",
                                Notes = reader["Notes"]?.ToString() ?? "",
                                CreatedAt = reader.GetDateTime("CreatedAt")
                            };
                        }
                    }
                }
            }

            return null;
        }

        public async Task<List<PensionLog>> GetPensionLogsAsync()
        {
            var logs = new List<PensionLog>();

            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT * FROM pension_logs 
                    ORDER BY Year DESC, 
                    FIELD(Month, 'January', 'February', 'March', 'April', 'May', 'June', 
                          'July', 'August', 'September', 'October', 'November', 'December') DESC";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            logs.Add(new PensionLog
                            {
                                Id = reader.GetInt32("Id"),
                                Month = reader.GetString("Month"),
                                Year = reader.GetInt32("Year"),
                                FilePath = reader["FilePath"]?.ToString() ?? "",
                                Notes = reader["Notes"]?.ToString() ?? "",
                                CreatedAt = reader.GetDateTime("CreatedAt")
                            });
                        }
                    }
                }
            }

            return logs;
        }

        public async Task<int> GetNewPensionSeniorsCountAsync(int month, int year)
        {
            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT COUNT(DISTINCT pc.SeniorId) 
                    FROM pension_contributions pc
                    INNER JOIN seniors s ON pc.SeniorId = s.Id
                    WHERE pc.Month = @Month 
                        AND pc.Year = @Year
                        AND s.Status = 'Active'
                        AND NOT EXISTS (
                            SELECT 1 
                            FROM pension_contributions pc2 
                            WHERE pc2.SeniorId = pc.SeniorId 
                                AND ((pc2.Year < @Year) OR (pc2.Year = @Year AND pc2.Month < @Month))
                        )";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Month", month);
                    cmd.Parameters.AddWithValue("@Year", year);

                    var result = await cmd.ExecuteScalarAsync();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
        }

        public async Task<List<string>> GetDistinctPensionTypesAsync()
        {
            var pensionTypes = new List<string>();

            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT DISTINCT 
                        CASE 
                            WHEN PensionType IS NULL OR PensionType = '' THEN 'No Pension'
                            ELSE PensionType 
                        END as PensionType
                    FROM seniors 
                    WHERE Status = 'Active'
                    ORDER BY 
                        CASE 
                            WHEN PensionType IS NULL OR PensionType = '' THEN 1
                            ELSE 0 
                        END, 
                        PensionType";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            pensionTypes.Add(reader.GetString("PensionType"));
                        }
                    }
                }
            }

            // Add common pension types if they don't exist
            var commonTypes = new List<string>
            {
                "Social Security",
                "Defined Benefit Plan",
                "Annuity",
                "Government/Military Pension",
                "Defined Contribution Plan (401k/403b)",
                "IRA (Traditional/Roth)"
            };

            foreach (var type in commonTypes)
            {
                if (!pensionTypes.Contains(type))
                {
                    pensionTypes.Add(type);
                }
            }

            return pensionTypes;
        }

        public async Task<int> CreateMonthlyPensionEntriesAsync(int month, int year)
        {
            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                // Get active seniors with pension type
                string getActiveSeniorsQuery = @"
                    SELECT s.Id, s.PensionType
                    FROM seniors s
                    WHERE s.Status = 'Active'
                    AND NOT EXISTS (
                        SELECT 1 
                        FROM pension_contributions pc 
                        WHERE pc.SeniorId = s.Id 
                        AND pc.Month = @Month 
                        AND pc.Year = @Year
                    )";

                var seniorsToAdd = new List<(int SeniorId, string PensionType)>();

                using (var getCmd = new MySqlCommand(getActiveSeniorsQuery, conn))
                {
                    getCmd.Parameters.AddWithValue("@Month", month);
                    getCmd.Parameters.AddWithValue("@Year", year);

                    using (var reader = await getCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int seniorId = reader.GetInt32("Id");
                            string pensionType = reader.IsDBNull(reader.GetOrdinal("PensionType"))
                                ? ""
                                : reader.GetString("PensionType");

                            seniorsToAdd.Add((seniorId, pensionType));
                        }
                    }
                }

                // Insert pension entries
                if (seniorsToAdd.Count > 0)
                {
                    string insertQuery = @"
                        INSERT INTO pension_contributions (SeniorId, Month, Year, IsClaimed)
                        VALUES (@SeniorId, @Month, @Year, FALSE)";

                    int rowsInserted = 0;

                    foreach (var senior in seniorsToAdd)
                    {
                        using (var insertCmd = new MySqlCommand(insertQuery, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@SeniorId", senior.SeniorId);
                            insertCmd.Parameters.AddWithValue("@Month", month);
                            insertCmd.Parameters.AddWithValue("@Year", year);

                            rowsInserted += await insertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    return rowsInserted;
                }
            }

            return 0;
        }

        public async Task<PensionContribution> GetPensionContributionByIdAsync(int id)
        {
            using (var conn = _dbHelper.GetConnection())
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        pc.Id,
                        pc.SeniorId,
                        pc.Month,
                        pc.Year,
                        pc.IsClaimed,
                        pc.ClaimedDate,
                        pc.CreatedAt,
                        CONCAT(s.LastName, ', ', s.FirstName, ' ', COALESCE(s.MiddleInitial, '')) as FullName,
                        s.FirstName,
                        s.LastName,
                        s.MiddleInitial,
                        s.Zone,
                        s.Status,
                        COALESCE(s.PensionType, '') as PensionType,
                        COALESCE(s.Age, 0) as Age,
                        s.SeniorId as SCCN
                    FROM pension_contributions pc
                    INNER JOIN seniors s ON pc.SeniorId = s.Id
                    WHERE pc.Id = @Id";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new PensionContribution
                            {
                                Id = reader.GetInt32("Id"),
                                SeniorId = reader.GetInt32("SeniorId"),
                                Month = reader.GetInt32("Month"),
                                Year = reader.GetInt32("Year"),
                                IsClaimed = reader.GetBoolean("IsClaimed"),
                                ClaimedDate = reader.IsDBNull("ClaimedDate") ? null : reader.GetDateTime("ClaimedDate"),
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                FirstName = reader.GetString("FirstName"),
                                LastName = reader.GetString("LastName"),
                                MiddleInitial = reader["MiddleInitial"]?.ToString() ?? "",
                                Zone = reader.GetInt32("Zone"),
                                Status = reader.GetString("Status"),
                                PensionType = reader["PensionType"]?.ToString() ?? "",
                                Age = reader.GetInt32("Age"),
                                FullName = reader.GetString("FullName").Trim()
                            };
                        }
                    }
                }
            }

            return null;
        }

        // Helper method for creating pension entries (backwards compatibility)
        private async Task<int> CreateMonthlyPensionEntriesPrivateAsync(int month, int year)
        {
            return await CreateMonthlyPensionEntriesAsync(month, year);
        }
    }
}