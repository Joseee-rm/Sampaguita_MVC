using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SeniorManagement.Models;
using SeniorManagement.Helpers;
using MySql.Data.MySqlClient;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System;
using System.Threading.Tasks;
using System.Data;

namespace SeniorManagement.Controllers
{
    [Authorize]
    public class ReportController : BaseController
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ActivityHelper _activityHelper;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public ReportController(DatabaseHelper dbHelper, ActivityHelper activityHelper, IConfiguration configuration)
        {
            _dbHelper = dbHelper;
            _activityHelper = activityHelper;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        // GET: /Report
        public async Task<IActionResult> Index(
            // Senior filters
            string status = null, string zone = null, string gender = null,
            string civilStatus = null, string ageRange = null,
            string monthYear = null, string seniorSearch = null,

            // Event filters
            string eventStatus = null, string eventType = null,
            string eventDateFilter = null, string eventSearch = null,
            string fromDate = null, string toDate = null)
        {
            try
            {
                // Get filtered seniors
                var seniors = GetFilteredSeniors(status, zone, gender, civilStatus, ageRange, monthYear, seniorSearch);

                // Get filtered events
                var events = await GetFilteredEvents(eventStatus, eventType, eventDateFilter, eventSearch, fromDate, toDate);

                // Get distinct months/years for senior filter dropdown
                var allSeniors = GetAllSeniors();
                var availableMonthsYears = allSeniors
                    .Select(s => new { Year = s.CreatedAt.Year, Month = s.CreatedAt.Month })
                    .Distinct()
                    .OrderByDescending(x => x.Year)
                    .ThenByDescending(x => x.Month)
                    .Select(x => $"{x.Year}-{x.Month:D2}")
                    .ToList();

                var viewModel = new ReportViewModel
                {
                    // Senior Data
                    TotalSeniors = seniors.Count,
                    SeniorList = seniors,
                    ReportDate = DateTime.Now,

                    // Senior Filters
                    SelectedStatus = status,
                    SelectedZoneFilter = zone,
                    SelectedGender = gender,
                    SelectedCivilStatus = civilStatus,
                    SelectedAgeRange = ageRange,
                    SelectedMonthYear = monthYear,
                    SeniorSearchTerm = seniorSearch,
                    AvailableMonthsYears = availableMonthsYears,

                    // Event Data
                    TotalEvents = events.Count,
                    EventList = events,

                    // Event Filters
                    SelectedEventStatus = eventStatus,
                    SelectedEventType = eventType,
                    SelectedEventDateFilter = eventDateFilter,
                    EventSearchTerm = eventSearch,
                    SelectedFromDate = fromDate,
                    SelectedToDate = toDate
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading reports: {ex.Message}";
                return View(new ReportViewModel());
            }
        }

        // GET: /Report/ExportSeniorTable
        public IActionResult ExportSeniorTable(string status = null, string zone = null, string gender = null,
                                              string civilStatus = null, string ageRange = null,
                                              string monthYear = null, string seniorSearch = null)
        {
            try
            {
                var seniors = GetFilteredSeniors(status, zone, gender, civilStatus, ageRange, monthYear, seniorSearch);

                if (!seniors.Any())
                {
                    TempData["ErrorMessage"] = "No senior data available for export.";
                    return RedirectToAction(nameof(Index));
                }

                var csvContent = GenerateSeniorCsvContent(seniors);
                var bytes = Encoding.UTF8.GetBytes(csvContent);

                _activityHelper.LogActivityAsync(
                    "Export Senior Table",
                    $"Exported senior table with {seniors.Count} records"
                ).Wait();

                return File(bytes, "text/csv", $"Senior_Table_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error exporting senior table: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Report/ExportEventTable
        public async Task<IActionResult> ExportEventTable(string eventStatus = null, string eventType = null,
                                                         string eventDateFilter = null, string eventSearch = null,
                                                         string fromDate = null, string toDate = null)
        {
            try
            {
                var events = await GetFilteredEvents(eventStatus, eventType, eventDateFilter, eventSearch, fromDate, toDate);

                if (!events.Any())
                {
                    TempData["ErrorMessage"] = "No event data available for export.";
                    return RedirectToAction(nameof(Index));
                }

                var csvContent = GenerateEventCsvContent(events);
                var bytes = Encoding.UTF8.GetBytes(csvContent);

                await _activityHelper.LogActivityAsync(
                    "Export Event Table",
                    $"Exported event table with {events.Count} records"
                );

                return File(bytes, "text/csv", $"Event_Table_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error exporting event table: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        #region Private Helper Methods

        private List<Senior> GetFilteredSeniors(string status, string zone, string gender, string civilStatus,
                                                string ageRange, string monthYear, string searchTerm)
        {
            var seniors = GetAllSeniors();

            // Apply filters
            if (!string.IsNullOrEmpty(status))
            {
                seniors = seniors.Where(s => s.Status == status).ToList();
            }

            if (!string.IsNullOrEmpty(zone) && int.TryParse(zone, out int zoneNum))
            {
                seniors = seniors.Where(s => s.Zone == zoneNum).ToList();
            }

            if (!string.IsNullOrEmpty(gender))
            {
                seniors = seniors.Where(s => s.Gender == gender).ToList();
            }

            if (!string.IsNullOrEmpty(civilStatus))
            {
                seniors = seniors.Where(s => s.CivilStatus == civilStatus).ToList();
            }

            if (!string.IsNullOrEmpty(ageRange))
            {
                switch (ageRange)
                {
                    case "60-69":
                        seniors = seniors.Where(s => s.Age >= 60 && s.Age <= 69).ToList();
                        break;
                    case "70-79":
                        seniors = seniors.Where(s => s.Age >= 70 && s.Age <= 79).ToList();
                        break;
                    case "80-89":
                        seniors = seniors.Where(s => s.Age >= 80 && s.Age <= 89).ToList();
                        break;
                    case "90+":
                        seniors = seniors.Where(s => s.Age >= 90).ToList();
                        break;
                }
            }

            // Filter by registration month/year
            if (!string.IsNullOrEmpty(monthYear))
            {
                try
                {
                    var dateParts = monthYear.Split('-');
                    if (dateParts.Length == 2 &&
                        int.TryParse(dateParts[0], out int year) &&
                        int.TryParse(dateParts[1], out int month))
                    {
                        seniors = seniors.Where(s =>
                            s.CreatedAt.Year == year &&
                            s.CreatedAt.Month == month).ToList();
                    }
                }
                catch { /* Ignore parsing errors */ }
            }

            // Search by name or SCCN
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                seniors = seniors.Where(s =>
                    s.CompleteName.ToLower().Contains(searchTerm) ||
                    s.FormattedSCCN.ToLower().Contains(searchTerm) ||
                    s.SeniorId.Contains(searchTerm)
                ).ToList();
            }

            return seniors;
        }

        private async Task<List<EventReportItem>> GetFilteredEvents(string status, string type, string dateFilter,
                                                          string search, string fromDate, string toDate)
        {
            var events = new List<EventReportItem>();

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                SELECT Id, EventTitle, EventDescription, EventType, EventDate, EventTime, 
                       EventLocation, OrganizedBy, MaxCapacity, AttendanceCount, 
                       Status, CreatedAt
                FROM events
                WHERE IsDeleted = 0";

                    var parameters = new List<MySqlParameter>();

                    // ... (same filter logic as before) ...

                    query += " ORDER BY EventDate DESC, EventTime DESC";

                    await using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddRange(parameters.ToArray());

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                // CORRECT WAY: Use GetFieldValue<TimeSpan>() or GetValue()
                                TimeSpan eventTime;

                                try
                                {
                                    // Method 1: GetFieldValue<TimeSpan> (most modern)
                                    eventTime = reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("EventTime"));
                                }
                                catch (InvalidCastException)
                                {
                                    // Method 2: Get as object and convert
                                    try
                                    {
                                        var timeValue = reader.GetValue(reader.GetOrdinal("EventTime"));

                                        if (timeValue is TimeSpan ts)
                                        {
                                            eventTime = ts;
                                        }
                                        else if (timeValue is DateTime dt)
                                        {
                                            eventTime = dt.TimeOfDay;
                                        }
                                        else if (timeValue is string timeString)
                                        {
                                            eventTime = TimeSpan.Parse(timeString);
                                        }
                                        else if (timeValue is TimeOnly timeOnly) // If using newer .NET
                                        {
                                            eventTime = timeOnly.ToTimeSpan();
                                        }
                                        else
                                        {
                                            // Try to convert to string and parse
                                            var stringValue = timeValue.ToString();
                                            eventTime = TimeSpan.Parse(stringValue);
                                        }
                                    }
                                    catch
                                    {
                                        eventTime = TimeSpan.Zero;
                                    }
                                }

                                var eventItem = new EventReportItem
                                {
                                    Id = reader.GetInt32("Id"),
                                    EventTitle = reader.GetString("EventTitle"),
                                    EventType = reader.GetString("EventType"),
                                    EventDate = reader.GetDateTime("EventDate"),
                                    EventTime = eventTime,
                                    EventLocation = reader.GetString("EventLocation"),
                                    OrganizedBy = reader.GetString("OrganizedBy"),
                                    MaxCapacity = reader.IsDBNull(reader.GetOrdinal("MaxCapacity"))
                                        ? null : (int?)reader.GetInt32("MaxCapacity"),
                                    AttendanceCount = reader.GetInt32("AttendanceCount"),
                                    Status = reader.GetString("Status"),
                                    CreatedAt = reader.GetDateTime("CreatedAt")
                                };

                                events.Add(eventItem);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting events: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }

            return events;
        }
        private List<Senior> GetAllSeniors()
        {
            var seniors = new List<Senior>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT Id, SeniorId, FirstName, LastName, MiddleInitial, Gender, Age, 
                                   BirthDate, ContactNumber, Zone, Barangay, CivilStatus, Status, 
                                   CreatedAt, UpdatedAt
                                   FROM seniors ORDER BY LastName, FirstName";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            seniors.Add(new Senior
                            {
                                Id = reader.GetInt32("Id"),
                                SeniorId = reader.GetString("SeniorId"),
                                FirstName = reader.GetString("FirstName"),
                                LastName = reader.GetString("LastName"),
                                MiddleInitial = reader.IsDBNull(reader.GetOrdinal("MiddleInitial")) ? "" : reader.GetString("MiddleInitial"),
                                Gender = reader.GetString("Gender"),
                                Age = reader.GetInt32("Age"),
                                BirthDate = reader.IsDBNull(reader.GetOrdinal("BirthDate")) ? (DateTime?)null : reader.GetDateTime("BirthDate"),
                                ContactNumber = reader.IsDBNull(reader.GetOrdinal("ContactNumber")) ? "" : reader.GetString("ContactNumber"),
                                Zone = reader.GetInt32("Zone"),
                                Barangay = reader.GetString("Barangay"),
                                CivilStatus = reader.IsDBNull(reader.GetOrdinal("CivilStatus")) ? "" : reader.GetString("CivilStatus"),
                                Status = reader.GetString("Status"),
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                UpdatedAt = reader.GetDateTime("UpdatedAt")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting seniors: {ex.Message}");
            }

            return seniors;
        }

        private string GenerateSeniorCsvContent(List<Senior> seniors)
        {
            var sb = new StringBuilder();

            // Headers
            sb.AppendLine("SCCN Number,Formatted SCCN,Full Name,Gender,Age,Zone,Civil Status,Contact Number,Status,Registered On");

            // Data
            foreach (var senior in seniors)
            {
                sb.AppendLine($"\"{senior.SeniorId}\",\"{senior.FormattedSCCN}\",\"{senior.CompleteName}\",\"{senior.Gender}\",\"{senior.Age}\",\"Zone {senior.Zone}\",\"{senior.CivilStatus}\",\"{senior.ContactNumber}\",\"{senior.Status}\",\"{senior.CreatedAt:yyyy-MM-dd}\"");
            }

            return sb.ToString();
        }

        private string GenerateEventCsvContent(List<EventReportItem> events)
        {
            var sb = new StringBuilder();

            // Headers
            sb.AppendLine("Event Title,Event Type,Date,Time,Location,Organized By,Max Capacity,Attendance,Attendance %,Available Slots,Status,Attendance Status,Created Date");

            // Data
            foreach (var eventItem in events)
            {
                sb.AppendLine($"\"{eventItem.EventTitle}\",\"{eventItem.EventType}\",\"{eventItem.EventDate:yyyy-MM-dd}\",\"{eventItem.EventTime:hh\\:mm}\",\"{eventItem.EventLocation}\",\"{eventItem.OrganizedBy}\",\"{eventItem.MaxCapacity}\",\"{eventItem.AttendanceCount}\",\"{eventItem.AttendancePercentage}\",\"{eventItem.AvailableSlots}\",\"{eventItem.Status}\",\"{eventItem.AttendanceStatus}\",\"{eventItem.CreatedAt:yyyy-MM-dd}\"");
            }

            return sb.ToString();
        }

        #endregion
    }
}