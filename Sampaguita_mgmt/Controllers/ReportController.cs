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
            string birthDate = null, string seniorSearch = null,
            string pensionType = null,

            // Event filters
            string eventStatus = null, string eventType = null,
            string eventDateFilter = null, string eventSearch = null,
            string fromDate = null, string toDate = null)
        {
            try
            {
                // Get filtered seniors
                var seniors = GetFilteredSeniors(status, zone, gender, civilStatus, ageRange, birthDate, seniorSearch, pensionType);

                // Get filtered events
                var events = await GetFilteredEvents(eventStatus, eventType, eventDateFilter, eventSearch, fromDate, toDate);

                // Get distinct birth dates for filter dropdown
                var allSeniors = GetAllSeniors();
                var availableBirthDates = allSeniors
                    .Where(s => s.BirthDate.HasValue)
                    .Select(s => s.BirthDate.Value.ToString("yyyy-MM-dd"))
                    .Distinct()
                    .OrderByDescending(x => x)
                    .ToList();

                // Get distinct pension types for filter including "NoPension"
                var pensionTypes = allSeniors
                    .Select(s => string.IsNullOrEmpty(s.PensionType) ? "NoPension" : s.PensionType)
                    .Distinct()
                    .OrderBy(p => p)
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
                    SelectedBirthDate = birthDate,
                    SelectedPensionType = pensionType,
                    SeniorSearchTerm = seniorSearch,
                    AvailableBirthDates = availableBirthDates,
                    AvailablePensionTypes = pensionTypes,

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
                                              string birthDate = null, string seniorSearch = null,
                                              string pensionType = null)
        {
            try
            {
                var seniors = GetFilteredSeniors(status, zone, gender, civilStatus, ageRange, birthDate, seniorSearch, pensionType);

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

                return File(bytes, "text/csv", $"Senior_Basic_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error exporting senior table: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Report/ExportSeniorDetailed
        public IActionResult ExportSeniorDetailed(string status = null, string zone = null, string gender = null,
                                                 string civilStatus = null, string ageRange = null,
                                                 string birthDate = null, string seniorSearch = null,
                                                 string pensionType = null)
        {
            try
            {
                var seniors = GetFilteredSeniors(status, zone, gender, civilStatus, ageRange, birthDate, seniorSearch, pensionType);

                if (!seniors.Any())
                {
                    TempData["ErrorMessage"] = "No senior data available for export.";
                    return RedirectToAction(nameof(Index));
                }

                var csvContent = GenerateSeniorDetailedCsvContent(seniors);
                var bytes = Encoding.UTF8.GetBytes(csvContent);

                _activityHelper.LogActivityAsync(
                    "Export Senior Detailed",
                    $"Exported detailed senior data with {seniors.Count} records"
                ).Wait();

                return File(bytes, "text/csv", $"Senior_Detailed_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error exporting detailed senior data: {ex.Message}";
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
                                                string ageRange, string birthDate, string searchTerm, string pensionType)
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
                // Parse custom age range (e.g., "80-83")
                var ageParts = ageRange.Split('-');
                if (ageParts.Length == 2 && int.TryParse(ageParts[0], out int minAge) && int.TryParse(ageParts[1], out int maxAge))
                {
                    seniors = seniors.Where(s => s.Age >= minAge && s.Age <= maxAge).ToList();
                }
                else if (ageRange.Contains("+"))
                {
                    // Handle "90+" case
                    if (int.TryParse(ageRange.Replace("+", ""), out int minAgeOnly))
                    {
                        seniors = seniors.Where(s => s.Age >= minAgeOnly).ToList();
                    }
                }
            }

            // Filter by pension type (including "NoPension")
            if (!string.IsNullOrEmpty(pensionType))
            {
                if (pensionType == "NoPension")
                {
                    seniors = seniors.Where(s => string.IsNullOrEmpty(s.PensionType)).ToList();
                }
                else
                {
                    seniors = seniors.Where(s => s.PensionType == pensionType).ToList();
                }
            }

            // Filter by birth date
            if (!string.IsNullOrEmpty(birthDate) && DateTime.TryParse(birthDate, out DateTime birthDateValue))
            {
                seniors = seniors.Where(s => s.BirthDate.HasValue &&
                    s.BirthDate.Value.Date == birthDateValue.Date).ToList();
            }

            // Search by name, SCCN, or other fields
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                seniors = seniors.Where(s =>
                    s.CompleteName.ToLower().Contains(searchTerm) ||
                    s.FormattedSCCN.ToLower().Contains(searchTerm) ||
                    s.SeniorId.Contains(searchTerm) ||
                    (s.ContactNumber != null && s.ContactNumber.Contains(searchTerm)) ||
                    (s.Email != null && s.Email.ToLower().Contains(searchTerm)) ||
                    (s.PensionType != null && s.PensionType.ToLower().Contains(searchTerm))
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

                    // Status filter
                    if (!string.IsNullOrEmpty(status))
                    {
                        query += " AND Status = @EventStatus";
                        parameters.Add(new MySqlParameter("@EventStatus", status));
                    }

                    // Type filter
                    if (!string.IsNullOrEmpty(type))
                    {
                        query += " AND EventType = @EventType";
                        parameters.Add(new MySqlParameter("@EventType", type));
                    }

                    // Date filter
                    if (!string.IsNullOrEmpty(dateFilter))
                    {
                        var today = DateTime.Today;
                        switch (dateFilter)
                        {
                            case "today":
                                query += " AND DATE(EventDate) = @Today";
                                parameters.Add(new MySqlParameter("@Today", today));
                                break;
                            case "tomorrow":
                                query += " AND DATE(EventDate) = @Tomorrow";
                                parameters.Add(new MySqlParameter("@Tomorrow", today.AddDays(1)));
                                break;
                            case "thisweek":
                                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                                var endOfWeek = startOfWeek.AddDays(6);
                                query += " AND EventDate BETWEEN @StartOfWeek AND @EndOfWeek";
                                parameters.Add(new MySqlParameter("@StartOfWeek", startOfWeek));
                                parameters.Add(new MySqlParameter("@EndOfWeek", endOfWeek));
                                break;
                            case "nextweek":
                                var nextWeekStart = today.AddDays(7 - (int)today.DayOfWeek);
                                var nextWeekEnd = nextWeekStart.AddDays(6);
                                query += " AND EventDate BETWEEN @NextWeekStart AND @NextWeekEnd";
                                parameters.Add(new MySqlParameter("@NextWeekStart", nextWeekStart));
                                parameters.Add(new MySqlParameter("@NextWeekEnd", nextWeekEnd));
                                break;
                            case "thismonth":
                                query += " AND MONTH(EventDate) = MONTH(@Today) AND YEAR(EventDate) = YEAR(@Today)";
                                parameters.Add(new MySqlParameter("@Today", today));
                                break;
                            case "nextmonth":
                                var nextMonth = today.AddMonths(1);
                                query += " AND MONTH(EventDate) = MONTH(@NextMonth) AND YEAR(EventDate) = YEAR(@NextMonth)";
                                parameters.Add(new MySqlParameter("@NextMonth", nextMonth));
                                break;
                            case "past":
                                query += " AND EventDate < @Today";
                                parameters.Add(new MySqlParameter("@Today", today));
                                break;
                            case "upcoming":
                                query += " AND EventDate >= @Today";
                                parameters.Add(new MySqlParameter("@Today", today));
                                break;
                        }
                    }

                    // Date range filter
                    if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out DateTime fromDateValue))
                    {
                        query += " AND EventDate >= @FromDate";
                        parameters.Add(new MySqlParameter("@FromDate", fromDateValue));
                    }

                    if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out DateTime toDateValue))
                    {
                        query += " AND EventDate <= @ToDate";
                        parameters.Add(new MySqlParameter("@ToDate", toDateValue));
                    }

                    // Search filter
                    if (!string.IsNullOrEmpty(search))
                    {
                        query += " AND (EventTitle LIKE @Search OR EventLocation LIKE @Search OR OrganizedBy LIKE @Search)";
                        parameters.Add(new MySqlParameter("@Search", $"%{search}%"));
                    }

                    query += " ORDER BY EventDate DESC, EventTime DESC";

                    await using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddRange(parameters.ToArray());

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                TimeSpan eventTime;

                                try
                                {
                                    eventTime = reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("EventTime"));
                                }
                                catch (InvalidCastException)
                                {
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
                                        else
                                        {
                                            eventTime = TimeSpan.Zero;
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
                    string query = @"SELECT * FROM seniors ORDER BY LastName, FirstName";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            seniors.Add(MapSeniorFromReader(reader));
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

        private Senior MapSeniorFromReader(MySqlDataReader reader)
        {
            return new Senior
            {
                Id = reader.GetInt32("Id"),
                SeniorId = reader.GetString("SeniorId"),
                FirstName = reader.GetString("FirstName"),
                LastName = reader.GetString("LastName"),
                MiddleInitial = reader.IsDBNull(reader.GetOrdinal("MiddleInitial")) ? "" : reader.GetString("MiddleInitial"),
                Extension = reader.IsDBNull(reader.GetOrdinal("Extension")) ? "" : reader.GetString("Extension"),
                Gender = reader.GetString("Gender"),
                Age = reader.GetInt32("Age"),
                BirthDate = reader.IsDBNull(reader.GetOrdinal("BirthDate")) ? (DateTime?)null : reader.GetDateTime("BirthDate"),
                Citizenship = reader.IsDBNull(reader.GetOrdinal("Citizenship")) ? "Filipino" : reader.GetString("Citizenship"),
                ContactNumber = reader.IsDBNull(reader.GetOrdinal("ContactNumber")) ? "" : reader.GetString("ContactNumber"),
                Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? "" : reader.GetString("Email"),
                Zone = reader.GetInt32("Zone"),
                Barangay = reader.GetString("Barangay"),
                CivilStatus = reader.IsDBNull(reader.GetOrdinal("CivilStatus")) ? "" : reader.GetString("CivilStatus"),
                PensionType = reader.IsDBNull(reader.GetOrdinal("PensionType")) ? "" : reader.GetString("PensionType"),
                HouseNumber = reader.IsDBNull(reader.GetOrdinal("HouseNumber")) ? "" : reader.GetString("HouseNumber"),
                CityMunicipality = reader.IsDBNull(reader.GetOrdinal("CityMunicipality")) ? "General Trias" : reader.GetString("CityMunicipality"),
                Province = reader.IsDBNull(reader.GetOrdinal("Province")) ? "Cavite" : reader.GetString("Province"),
                ZipCode = reader.IsDBNull(reader.GetOrdinal("ZipCode")) ? "4107" : reader.GetString("ZipCode"),
                SpouseFirstName = reader.IsDBNull(reader.GetOrdinal("SpouseFirstName")) ? "" : reader.GetString("SpouseFirstName"),
                SpouseLastName = reader.IsDBNull(reader.GetOrdinal("SpouseLastName")) ? "" : reader.GetString("SpouseLastName"),
                SpouseMiddleName = reader.IsDBNull(reader.GetOrdinal("SpouseMiddleName")) ? "" : reader.GetString("SpouseMiddleName"),
                SpouseExtension = reader.IsDBNull(reader.GetOrdinal("SpouseExtension")) ? "" : reader.GetString("SpouseExtension"),
                SpouseCitizenship = reader.IsDBNull(reader.GetOrdinal("SpouseCitizenship")) ? "" : reader.GetString("SpouseCitizenship"),
                ChildrenInfo = reader.IsDBNull(reader.GetOrdinal("ChildrenInfo")) ? "" : reader.GetString("ChildrenInfo"),
                AuthorizedRepInfo = reader.IsDBNull(reader.GetOrdinal("AuthorizedRepInfo")) ? "" : reader.GetString("AuthorizedRepInfo"),
                PrimaryBeneficiaryFirstName = reader.IsDBNull(reader.GetOrdinal("PrimaryBeneficiaryFirstName")) ? "" : reader.GetString("PrimaryBeneficiaryFirstName"),
                PrimaryBeneficiaryLastName = reader.IsDBNull(reader.GetOrdinal("PrimaryBeneficiaryLastName")) ? "" : reader.GetString("PrimaryBeneficiaryLastName"),
                PrimaryBeneficiaryMiddleName = reader.IsDBNull(reader.GetOrdinal("PrimaryBeneficiaryMiddleName")) ? "" : reader.GetString("PrimaryBeneficiaryMiddleName"),
                PrimaryBeneficiaryExtension = reader.IsDBNull(reader.GetOrdinal("PrimaryBeneficiaryExtension")) ? "" : reader.GetString("PrimaryBeneficiaryExtension"),
                PrimaryBeneficiaryRelationship = reader.IsDBNull(reader.GetOrdinal("PrimaryBeneficiaryRelationship")) ? "" : reader.GetString("PrimaryBeneficiaryRelationship"),
                ContingentBeneficiaryFirstName = reader.IsDBNull(reader.GetOrdinal("ContingentBeneficiaryFirstName")) ? "" : reader.GetString("ContingentBeneficiaryFirstName"),
                ContingentBeneficiaryLastName = reader.IsDBNull(reader.GetOrdinal("ContingentBeneficiaryLastName")) ? "" : reader.GetString("ContingentBeneficiaryLastName"),
                ContingentBeneficiaryMiddleName = reader.IsDBNull(reader.GetOrdinal("ContingentBeneficiaryMiddleName")) ? "" : reader.GetString("ContingentBeneficiaryMiddleName"),
                ContingentBeneficiaryExtension = reader.IsDBNull(reader.GetOrdinal("ContingentBeneficiaryExtension")) ? "" : reader.GetString("ContingentBeneficiaryExtension"),
                ContingentBeneficiaryRelationship = reader.IsDBNull(reader.GetOrdinal("ContingentBeneficiaryRelationship")) ? "" : reader.GetString("ContingentBeneficiaryRelationship"),
                Status = reader.GetString("Status"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedAt = reader.GetDateTime("UpdatedAt")
            };
        }

        private string GenerateSeniorCsvContent(List<Senior> seniors)
        {
            var sb = new StringBuilder();

            // Headers - Basic information (with BirthDate instead of Registered On)
            sb.AppendLine("SCCN Number,Formatted SCCN,Full Name,Gender,Age,Birth Date,Zone,Barangay,Civil Status,Contact Number,Email,Citizenship,Pension Type,House Number,City/Municipality,Province,Zip Code,Status,Last Updated");

            // Data
            foreach (var senior in seniors)
            {
                sb.AppendLine($"\"{senior.SeniorId}\",\"{senior.FormattedSCCN}\",\"{senior.CompleteName}\",\"{senior.Gender}\",\"{senior.Age}\",\"{senior.BirthDate?.ToString("yyyy-MM-dd")}\",\"Zone {senior.Zone}\",\"{senior.Barangay}\",\"{senior.CivilStatus}\",\"{senior.ContactNumber}\",\"{senior.Email}\",\"{senior.Citizenship}\",\"{senior.PensionType}\",\"{senior.HouseNumber}\",\"{senior.CityMunicipality}\",\"{senior.Province}\",\"{senior.ZipCode}\",\"{senior.Status}\",\"{senior.UpdatedAt:yyyy-MM-dd}\"");
            }

            return sb.ToString();
        }

        private string GenerateSeniorDetailedCsvContent(List<Senior> seniors)
        {
            var sb = new StringBuilder();

            // Headers - Complete data export (all 40+ fields)
            sb.AppendLine("ID,SCCN Number,Formatted SCCN,First Name,Last Name,Middle Initial,Extension,Gender,Age,Birth Date,Citizenship,Contact Number,Email,Zone,Barangay,Civil Status,Pension Type,House Number,City/Municipality,Province,Zip Code,Spouse First Name,Spouse Last Name,Spouse Middle Name,Spouse Extension,Spouse Citizenship,Children Info,Authorized Representative Info,Primary Beneficiary First Name,Primary Beneficiary Last Name,Primary Beneficiary Middle Name,Primary Beneficiary Extension,Primary Beneficiary Relationship,Contingent Beneficiary First Name,Contingent Beneficiary Last Name,Contingent Beneficiary Middle Name,Contingent Beneficiary Extension,Contingent Beneficiary Relationship,Status,Created At,Updated At");

            // Data
            foreach (var senior in seniors)
            {
                sb.AppendLine($"\"{senior.Id}\",\"{senior.SeniorId}\",\"{senior.FormattedSCCN}\",\"{senior.FirstName}\",\"{senior.LastName}\",\"{senior.MiddleInitial}\",\"{senior.Extension}\",\"{senior.Gender}\",\"{senior.Age}\",\"{senior.BirthDate?.ToString("yyyy-MM-dd")}\",\"{senior.Citizenship}\",\"{senior.ContactNumber}\",\"{senior.Email}\",\"{senior.Zone}\",\"{senior.Barangay}\",\"{senior.CivilStatus}\",\"{senior.PensionType}\",\"{senior.HouseNumber}\",\"{senior.CityMunicipality}\",\"{senior.Province}\",\"{senior.ZipCode}\",\"{senior.SpouseFirstName}\",\"{senior.SpouseLastName}\",\"{senior.SpouseMiddleName}\",\"{senior.SpouseExtension}\",\"{senior.SpouseCitizenship}\",\"{senior.ChildrenInfo}\",\"{senior.AuthorizedRepInfo}\",\"{senior.PrimaryBeneficiaryFirstName}\",\"{senior.PrimaryBeneficiaryLastName}\",\"{senior.PrimaryBeneficiaryMiddleName}\",\"{senior.PrimaryBeneficiaryExtension}\",\"{senior.PrimaryBeneficiaryRelationship}\",\"{senior.ContingentBeneficiaryFirstName}\",\"{senior.ContingentBeneficiaryLastName}\",\"{senior.ContingentBeneficiaryMiddleName}\",\"{senior.ContingentBeneficiaryExtension}\",\"{senior.ContingentBeneficiaryRelationship}\",\"{senior.Status}\",\"{senior.CreatedAt:yyyy-MM-dd HH:mm:ss}\",\"{senior.UpdatedAt:yyyy-MM-dd HH:mm:ss}\"");
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