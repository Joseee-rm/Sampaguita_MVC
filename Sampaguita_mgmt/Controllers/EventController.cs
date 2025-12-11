// Controllers/EventController.cs - Complete Updated Version
using Microsoft.AspNetCore.Mvc;
using SeniorManagement.Models;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

namespace SeniorManagement.Controllers
{
    [Authorize(Roles = "Administrator,Staff")]
    public class EventController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public EventController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        // GET: /Event
        public async Task<IActionResult> Index(string search = "", string status = "", string type = "", string dateFilter = "")
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");
            string userName = User.Identity?.Name;

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsStaff = isStaff;
            ViewBag.UserName = userName;

            // Store filter values for UI
            ViewBag.SearchQuery = search;
            ViewBag.SelectedStatus = status;
            ViewBag.SelectedType = type;
            ViewBag.SelectedDateFilter = dateFilter;

            var events = await GetFilteredEvents(search, status, type, dateFilter, false);

            // Get statistics for filter sidebar
            await LoadEventStatistics();

            return View(events);
        }

        // Helper method to get filtered events
        private async Task<List<Event>> GetFilteredEvents(string search = "", string status = "", string type = "", string dateFilter = "", bool includeArchived = false)
        {
            var events = new List<Event>();

            if (string.IsNullOrEmpty(_connectionString))
                return events;

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        SELECT Id, EventTitle, EventDescription, EventType, EventDate, EventTime, 
                               EventLocation, OrganizedBy, MaxCapacity, AttendanceCount, 
                               Status, CreatedAt, UpdatedAt, IsDeleted, DeletedAt
                        FROM events
                        WHERE IsDeleted = @IsDeleted";

                    var parameters = new List<MySqlParameter>
                    {
                        new MySqlParameter("@IsDeleted", includeArchived ? 1 : 0)
                    };

                    // Apply search filter
                    if (!string.IsNullOrEmpty(search))
                    {
                        query += " AND (EventTitle LIKE @Search OR EventDescription LIKE @Search OR EventLocation LIKE @Search OR OrganizedBy LIKE @Search)";
                        parameters.Add(new MySqlParameter("@Search", $"%{search}%"));
                    }

                    // Apply status filter
                    if (!string.IsNullOrEmpty(status) && status != "All")
                    {
                        query += " AND Status = @Status";
                        parameters.Add(new MySqlParameter("@Status", status));
                    }

                    // Apply type filter
                    if (!string.IsNullOrEmpty(type) && type != "All")
                    {
                        query += " AND EventType = @Type";
                        parameters.Add(new MySqlParameter("@Type", type));
                    }

                    // Apply date filter
                    if (!string.IsNullOrEmpty(dateFilter) && dateFilter != "All")
                    {
                        query += GetDateFilterQuery(dateFilter);
                    }

                    // Staff can only see events they organized
                    bool isStaff = User.IsInRole("Staff");
                    if (isStaff)
                    {
                        query += " AND OrganizedBy LIKE @StaffName";
                        parameters.Add(new MySqlParameter("@StaffName", $"%{User.Identity?.Name}%"));
                    }

                    query += " ORDER BY EventDate, EventTime";

                    await using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddRange(parameters.ToArray());

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var evt = new Event
                                {
                                    Id = reader.GetInt32("Id"),
                                    EventTitle = reader.GetString("EventTitle"),
                                    EventDescription = reader.GetString("EventDescription"),
                                    EventType = reader.GetString("EventType"),
                                    EventDate = reader.GetDateTime("EventDate"),
                                    EventTime = reader.GetTimeSpan("EventTime"),
                                    EventLocation = reader.GetString("EventLocation"),
                                    OrganizedBy = reader.GetString("OrganizedBy"),
                                    MaxCapacity = reader.IsDBNull(reader.GetOrdinal("MaxCapacity"))
                                        ? null : (int?)reader.GetInt32("MaxCapacity"),
                                    AttendanceCount = reader.GetInt32("AttendanceCount"),
                                    Status = reader.GetString("Status"),
                                    CreatedAt = reader.GetDateTime("CreatedAt"),
                                    UpdatedAt = reader.GetDateTime("UpdatedAt"),
                                    IsDeleted = reader.GetBoolean("IsDeleted"),
                                    DeletedAt = reader.IsDBNull(reader.GetOrdinal("DeletedAt"))
                                        ? null : (DateTime?)reader.GetDateTime("DeletedAt")
                                };

                                events.Add(evt);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading events: {ex.Message}");
                ViewBag.Error = $"Database error: {ex.Message}";
            }

            return events;
        }

        private string GetDateFilterQuery(string dateFilter)
        {
            return dateFilter switch
            {
                "today" => " AND DATE(EventDate) = CURDATE()",
                "tomorrow" => " AND DATE(EventDate) = DATE_ADD(CURDATE(), INTERVAL 1 DAY)",
                "thisweek" => " AND YEARWEEK(EventDate, 1) = YEARWEEK(CURDATE(), 1)",
                "nextweek" => " AND YEARWEEK(EventDate, 1) = YEARWEEK(CURDATE(), 1) + 1",
                "thismonth" => " AND MONTH(EventDate) = MONTH(CURDATE()) AND YEAR(EventDate) = YEAR(CURDATE())",
                "nextmonth" => " AND MONTH(EventDate) = MONTH(DATE_ADD(CURDATE(), INTERVAL 1 MONTH)) AND YEAR(EventDate) = YEAR(DATE_ADD(CURDATE(), INTERVAL 1 MONTH))",
                "past" => " AND EventDate < CURDATE()",
                "upcoming" => " AND EventDate >= CURDATE()",
                _ => ""
            };
        }

        // GET: /Event/Archived
        public async Task<IActionResult> Archived(string search = "", string status = "", string type = "", string dateFilter = "")
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");
            string userName = User.Identity?.Name;

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsStaff = isStaff;
            ViewBag.UserName = userName;

            // Store filter values for UI
            ViewBag.SearchQuery = search;
            ViewBag.SelectedStatus = status;
            ViewBag.SelectedType = type;
            ViewBag.SelectedDateFilter = dateFilter;

            var events = await GetFilteredEvents(search, status, type, dateFilter, true);

            // Get statistics for archived events
            await LoadArchivedStatistics();

            return View(events);
        }

        // GET: /Event/Calendar
        public async Task<IActionResult> Calendar()
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsStaff = isStaff;

            if (string.IsNullOrEmpty(_connectionString))
            {
                ViewBag.Error = "Database connection is not configured.";
                return View(new List<Event>());
            }

            var events = new List<Event>();

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
                        WHERE IsDeleted = 0 AND Status != 'Cancelled'";

                    // Staff can only see their events
                    if (isStaff)
                    {
                        query += " AND OrganizedBy LIKE @StaffName";
                    }

                    query += " ORDER BY EventDate, EventTime";

                    await using (var command = new MySqlCommand(query, connection))
                    {
                        if (isStaff)
                        {
                            command.Parameters.AddWithValue("@StaffName", $"%{User.Identity?.Name}%");
                        }

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var evt = new Event
                                {
                                    Id = reader.GetInt32("Id"),
                                    EventTitle = reader.GetString("EventTitle"),
                                    EventDescription = reader.GetString("EventDescription"),
                                    EventType = reader.GetString("EventType"),
                                    EventDate = reader.GetDateTime("EventDate"),
                                    EventTime = reader.GetTimeSpan("EventTime"),
                                    EventLocation = reader.GetString("EventLocation"),
                                    OrganizedBy = reader.GetString("OrganizedBy"),
                                    MaxCapacity = reader.IsDBNull(reader.GetOrdinal("MaxCapacity"))
                                        ? null : (int?)reader.GetInt32("MaxCapacity"),
                                    AttendanceCount = reader.GetInt32("AttendanceCount"),
                                    Status = reader.GetString("Status"),
                                    CreatedAt = reader.GetDateTime("CreatedAt")
                                };

                                events.Add(evt);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Database error: {ex.Message}";
            }

            return View(events);
        }

        // GET: /Event/Create
        public IActionResult Create()
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");
            string userName = User.Identity?.Name;

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsStaff = isStaff;
            ViewBag.UserName = userName;

            if (isStaff)
            {
                ViewBag.DefaultOrganizer = userName;
            }

            // Set default date to today
            ViewBag.DefaultDate = DateTime.Today.ToString("yyyy-MM-dd");

            // Set default time to next hour
            var nextHour = DateTime.Now.AddHours(1);
            ViewBag.DefaultTime = nextHour.ToString("HH:00");

            return View();
        }

        // POST: /Event/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Event eventModel)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");
            string userName = User.Identity?.Name;

            // Staff can only create events they organize
            if (isStaff && !string.IsNullOrEmpty(eventModel.OrganizedBy) &&
                !eventModel.OrganizedBy.Contains(userName))
            {
                ModelState.AddModelError("OrganizedBy", "As a staff member, you can only create events that you organize.");
            }

            // Validate date
            if (eventModel.EventDate < DateTime.Today)
            {
                ModelState.AddModelError("EventDate", "Event date cannot be in the past.");
            }

            // Always set status to "Upcoming" on creation
            eventModel.Status = "Upcoming";

            if (ModelState.IsValid)
            {
                try
                {
                    await using (var connection = new MySqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        // If staff user didn't specify organizer, use their name
                        if (isStaff && string.IsNullOrEmpty(eventModel.OrganizedBy))
                        {
                            eventModel.OrganizedBy = userName;
                        }

                        var query = @"
                            INSERT INTO events (
                                EventTitle, EventDescription, EventType, EventDate, EventTime,
                                EventLocation, OrganizedBy, MaxCapacity, AttendanceCount,
                                Status, CreatedAt, UpdatedAt
                            ) VALUES (
                                @EventTitle, @EventDescription, @EventType, @EventDate, @EventTime,
                                @EventLocation, @OrganizedBy, @MaxCapacity, @AttendanceCount,
                                @Status, @CreatedAt, @UpdatedAt
                            )";

                        await using (var command = new MySqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@EventTitle", eventModel.EventTitle ?? "");
                            command.Parameters.AddWithValue("@EventDescription", eventModel.EventDescription ?? "");
                            command.Parameters.AddWithValue("@EventType", eventModel.EventType ?? "Community Gathering");
                            command.Parameters.AddWithValue("@EventDate", eventModel.EventDate);
                            command.Parameters.AddWithValue("@EventTime", eventModel.EventTime);
                            command.Parameters.AddWithValue("@EventLocation", eventModel.EventLocation ?? "");
                            command.Parameters.AddWithValue("@OrganizedBy", eventModel.OrganizedBy ?? "");

                            if (eventModel.MaxCapacity.HasValue)
                                command.Parameters.AddWithValue("@MaxCapacity", eventModel.MaxCapacity.Value);
                            else
                                command.Parameters.AddWithValue("@MaxCapacity", DBNull.Value);

                            command.Parameters.AddWithValue("@AttendanceCount", 0);
                            command.Parameters.AddWithValue("@Status", "Upcoming");
                            command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                            command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    TempData["SuccessMessage"] = "Event created successfully with status 'Upcoming'!";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error creating event: {ex.Message}");
                }
            }

            return View(eventModel);
        }

        // GET: /Event/View/{id}
        public async Task<IActionResult> View(int id)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsStaff = isStaff;

            if (string.IsNullOrEmpty(_connectionString))
            {
                return NotFound("Database connection is not configured.");
            }

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        SELECT Id, EventTitle, EventDescription, EventType, EventDate, EventTime, 
                               EventLocation, OrganizedBy, MaxCapacity, AttendanceCount, 
                               Status, CreatedAt, UpdatedAt, IsDeleted, DeletedAt
                        FROM events
                        WHERE Id = @Id";

                    await using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var evt = new Event
                                {
                                    Id = reader.GetInt32("Id"),
                                    EventTitle = reader.GetString("EventTitle"),
                                    EventDescription = reader.GetString("EventDescription"),
                                    EventType = reader.GetString("EventType"),
                                    EventDate = reader.GetDateTime("EventDate"),
                                    EventTime = reader.GetTimeSpan("EventTime"),
                                    EventLocation = reader.GetString("EventLocation"),
                                    OrganizedBy = reader.GetString("OrganizedBy"),
                                    MaxCapacity = reader.IsDBNull(reader.GetOrdinal("MaxCapacity"))
                                        ? null : (int?)reader.GetInt32("MaxCapacity"),
                                    AttendanceCount = reader.GetInt32("AttendanceCount"),
                                    Status = reader.GetString("Status"),
                                    CreatedAt = reader.GetDateTime("CreatedAt"),
                                    UpdatedAt = reader.GetDateTime("UpdatedAt"),
                                    IsDeleted = reader.GetBoolean("IsDeleted"),
                                    DeletedAt = reader.IsDBNull(reader.GetOrdinal("DeletedAt"))
                                        ? null : (DateTime?)reader.GetDateTime("DeletedAt")
                                };

                                // Check permissions
                                if (isStaff && !evt.OrganizedBy.Contains(User.Identity?.Name))
                                {
                                    TempData["ErrorMessage"] = "You can only view events that you organized.";
                                    return RedirectToAction("Index");
                                }

                                ViewBag.CanEdit = isAdmin || (isStaff && evt.OrganizedBy.Contains(User.Identity?.Name));
                                return View(evt);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading event: {ex.Message}";
            }

            return NotFound();
        }

        // GET: /Event/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsStaff = isStaff;

            if (string.IsNullOrEmpty(_connectionString))
            {
                return NotFound("Database connection is not configured.");
            }

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        SELECT Id, EventTitle, EventDescription, EventType, EventDate, EventTime, 
                               EventLocation, OrganizedBy, MaxCapacity, AttendanceCount, 
                               Status, CreatedAt, UpdatedAt, IsDeleted, DeletedAt
                        FROM events
                        WHERE Id = @Id AND IsDeleted = 0";

                    await using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var evt = new Event
                                {
                                    Id = reader.GetInt32("Id"),
                                    EventTitle = reader.GetString("EventTitle"),
                                    EventDescription = reader.GetString("EventDescription"),
                                    EventType = reader.GetString("EventType"),
                                    EventDate = reader.GetDateTime("EventDate"),
                                    EventTime = reader.GetTimeSpan("EventTime"),
                                    EventLocation = reader.GetString("EventLocation"),
                                    OrganizedBy = reader.GetString("OrganizedBy"),
                                    MaxCapacity = reader.IsDBNull(reader.GetOrdinal("MaxCapacity"))
                                        ? null : (int?)reader.GetInt32("MaxCapacity"),
                                    AttendanceCount = reader.GetInt32("AttendanceCount"),
                                    Status = reader.GetString("Status"),
                                    CreatedAt = reader.GetDateTime("CreatedAt"),
                                    UpdatedAt = reader.GetDateTime("UpdatedAt"),
                                    IsDeleted = reader.GetBoolean("IsDeleted"),
                                    DeletedAt = reader.IsDBNull(reader.GetOrdinal("DeletedAt"))
                                        ? null : (DateTime?)reader.GetDateTime("DeletedAt")
                                };

                                // Check if staff can edit this event
                                if (isStaff && !evt.OrganizedBy.Contains(User.Identity?.Name))
                                {
                                    TempData["ErrorMessage"] = "You can only edit events that you organized.";
                                    return RedirectToAction("Index");
                                }

                                return View(evt);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading event: {ex.Message}";
            }

            return NotFound();
        }

        // POST: /Event/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Event eventModel)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            if (id != eventModel.Id)
                return BadRequest();

            // Check if staff can edit this event
            if (isStaff)
            {
                try
                {
                    await using (var connection = new MySqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        var checkQuery = @"
                            SELECT OrganizedBy, Status FROM events 
                            WHERE Id = @Id AND IsDeleted = 0";

                        await using (var checkCommand = new MySqlCommand(checkQuery, connection))
                        {
                            checkCommand.Parameters.AddWithValue("@Id", id);
                            using (var reader = await checkCommand.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    string organizer = reader.GetString("OrganizedBy");
                                    string currentStatus = reader.GetString("Status");

                                    if (!organizer.Contains(User.Identity?.Name))
                                    {
                                        TempData["ErrorMessage"] = "You can only edit events that you organized.";
                                        return RedirectToAction("Index");
                                    }

                                    // Prevent editing status if it's Completed
                                    if (currentStatus == "Completed" && eventModel.Status != "Completed")
                                    {
                                        ModelState.AddModelError("Status", "Cannot change status from Completed.");
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    TempData["ErrorMessage"] = "Error verifying event permissions.";
                    return RedirectToAction("Index");
                }
            }

            if (string.IsNullOrEmpty(_connectionString))
            {
                ModelState.AddModelError("", "Database connection is not configured.");
                return View(eventModel);
            }

            // Validate attendance vs capacity
            if (eventModel.MaxCapacity.HasValue && eventModel.AttendanceCount > eventModel.MaxCapacity.Value)
            {
                ModelState.AddModelError("AttendanceCount", "Attendance count cannot exceed maximum capacity.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await using (var connection = new MySqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        var query = @"
                            UPDATE events SET
                                EventTitle = @EventTitle,
                                EventDescription = @EventDescription,
                                EventType = @EventType,
                                EventDate = @EventDate,
                                EventTime = @EventTime,
                                EventLocation = @EventLocation,
                                OrganizedBy = @OrganizedBy,
                                MaxCapacity = @MaxCapacity,
                                AttendanceCount = @AttendanceCount,
                                Status = @Status,
                                UpdatedAt = @UpdatedAt
                            WHERE Id = @Id AND IsDeleted = 0";

                        await using (var command = new MySqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@Id", id);
                            command.Parameters.AddWithValue("@EventTitle", eventModel.EventTitle ?? "");
                            command.Parameters.AddWithValue("@EventDescription", eventModel.EventDescription ?? "");
                            command.Parameters.AddWithValue("@EventType", eventModel.EventType ?? "Community Gathering");
                            command.Parameters.AddWithValue("@EventDate", eventModel.EventDate);
                            command.Parameters.AddWithValue("@EventTime", eventModel.EventTime);
                            command.Parameters.AddWithValue("@EventLocation", eventModel.EventLocation ?? "");
                            command.Parameters.AddWithValue("@OrganizedBy", eventModel.OrganizedBy ?? "");

                            if (eventModel.MaxCapacity.HasValue)
                                command.Parameters.AddWithValue("@MaxCapacity", eventModel.MaxCapacity.Value);
                            else
                                command.Parameters.AddWithValue("@MaxCapacity", DBNull.Value);

                            command.Parameters.AddWithValue("@AttendanceCount", eventModel.AttendanceCount);
                            command.Parameters.AddWithValue("@Status", eventModel.Status ?? "Upcoming");
                            command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

                            var rowsAffected = await command.ExecuteNonQueryAsync();

                            if (rowsAffected > 0)
                            {
                                TempData["SuccessMessage"] = "Event updated successfully!";
                                return RedirectToAction("Index");
                            }
                            else
                            {
                                ModelState.AddModelError("", "Event not found or has been deleted.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error updating event: {ex.Message}");
                }
            }

            return View(eventModel);
        }

        // GET: /Event/Attendance/{id}
        public async Task<IActionResult> Attendance(int id)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsStaff = isStaff;

            if (string.IsNullOrEmpty(_connectionString))
            {
                TempData["ErrorMessage"] = "Database connection error";
                return RedirectToAction("Index");
            }

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Get event details
                    var eventQuery = @"
                        SELECT Id, EventTitle, EventDescription, EventType, EventDate, EventTime, 
                               EventLocation, OrganizedBy, MaxCapacity, AttendanceCount, Status
                        FROM events 
                        WHERE Id = @Id AND IsDeleted = 0";

                    Event eventDetails = null;

                    await using (var eventCommand = new MySqlCommand(eventQuery, connection))
                    {
                        eventCommand.Parameters.AddWithValue("@Id", id);
                        await using (var reader = await eventCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                eventDetails = new Event
                                {
                                    Id = reader.GetInt32("Id"),
                                    EventTitle = reader.GetString("EventTitle"),
                                    EventDescription = reader.GetString("EventDescription"),
                                    EventType = reader.GetString("EventType"),
                                    EventDate = reader.GetDateTime("EventDate"),
                                    EventTime = reader.GetTimeSpan("EventTime"),
                                    EventLocation = reader.GetString("EventLocation"),
                                    OrganizedBy = reader.GetString("OrganizedBy"),
                                    MaxCapacity = reader.IsDBNull(reader.GetOrdinal("MaxCapacity"))
                                        ? null : (int?)reader.GetInt32("MaxCapacity"),
                                    AttendanceCount = reader.GetInt32("AttendanceCount"),
                                    Status = reader.GetString("Status")
                                };
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "Event not found";
                                return RedirectToAction("Index");
                            }
                        }
                    }

                    // Check permissions for staff
                    if (isStaff && !eventDetails.OrganizedBy.Contains(User.Identity?.Name))
                    {
                        TempData["ErrorMessage"] = "You can only manage attendance for events you organized.";
                        return RedirectToAction("Index");
                    }

                    // Get all active seniors
                    var seniorsQuery = @"
                        SELECT Id, SeniorId, FirstName, LastName, MiddleInitial, 
                               Gender, Age, Zone, Barangay, ContactNumber, Status
                        FROM seniors 
                        WHERE Status = 'Active'
                        ORDER BY LastName, FirstName";

                    var seniors = new List<Senior>();

                    await using (var seniorsCommand = new MySqlCommand(seniorsQuery, connection))
                    {
                        await using (var reader = await seniorsCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var senior = new Senior
                                {
                                    Id = reader.GetInt32("Id"),
                                    SeniorId = reader.GetString("SeniorId"),
                                    FirstName = reader.GetString("FirstName"),
                                    LastName = reader.GetString("LastName"),
                                    MiddleInitial = reader.IsDBNull(reader.GetOrdinal("MiddleInitial"))
                                        ? "" : reader.GetString("MiddleInitial"),
                                    Gender = reader.GetString("Gender"),
                                    Age = reader.GetInt32("Age"),
                                    Zone = reader.GetInt32("Zone"),
                                    Barangay = reader.GetString("Barangay"),
                                    ContactNumber = reader.IsDBNull(reader.GetOrdinal("ContactNumber"))
                                        ? "" : reader.GetString("ContactNumber"),
                                    Status = reader.GetString("Status")
                                };
                                seniors.Add(senior);
                            }
                        }
                    }

                    // Get attendance records for this event
                    var attendanceQuery = @"
                        SELECT SeniorId, AttendanceStatus 
                        FROM event_attendance 
                        WHERE EventId = @EventId";

                    var attendanceStatus = new Dictionary<string, string>();

                    await using (var attendanceCommand = new MySqlCommand(attendanceQuery, connection))
                    {
                        attendanceCommand.Parameters.AddWithValue("@EventId", id);
                        await using (var reader = await attendanceCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string seniorId = reader.GetString("SeniorId");
                                string status = reader.GetString("AttendanceStatus");
                                attendanceStatus[seniorId] = status;
                            }
                        }
                    }

                    // Calculate present count from attendance records
                    int presentCount = attendanceStatus.Count(a => a.Value == "Present");

                    ViewBag.EventDetails = eventDetails;
                    ViewBag.AttendanceStatus = attendanceStatus;
                    ViewBag.PresentCount = presentCount;
                    ViewBag.AbsentCount = attendanceStatus.Count(a => a.Value == "Absent");
                    ViewBag.UnmarkedCount = seniors.Count - attendanceStatus.Count;

                    return View(seniors);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading attendance: {ex.Message}";
                return RedirectToAction("View", new { id = id });
            }
        }

        // POST: /Event/MarkAttendance/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAttendance(int id, [FromForm] string seniorId, [FromForm] string status)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check event exists and permissions
                    var checkQuery = @"
                        SELECT OrganizedBy, Status 
                        FROM events 
                        WHERE Id = @Id AND IsDeleted = 0";

                    string organizedBy = "";
                    string eventStatus = "";

                    await using (var checkCommand = new MySqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Id", id);
                        await using (var reader = await checkCommand.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                            {
                                return Json(new { success = false, message = "Event not found" });
                            }
                            organizedBy = reader.GetString("OrganizedBy");
                            eventStatus = reader.GetString("Status");
                        }
                    }

                    // Check permissions
                    if (isStaff && !organizedBy.Contains(User.Identity?.Name))
                    {
                        return Json(new { success = false, message = "You don't have permission to mark attendance for this event" });
                    }

                    // Check if event is cancelled
                    if (eventStatus == "Cancelled")
                    {
                        return Json(new { success = false, message = "Cannot mark attendance for cancelled events" });
                    }

                    // Check if senior exists
                    var seniorCheckQuery = "SELECT COUNT(*) FROM seniors WHERE SeniorId = @SeniorId AND Status = 'Active'";
                    await using (var seniorCheckCommand = new MySqlCommand(seniorCheckQuery, connection))
                    {
                        seniorCheckCommand.Parameters.AddWithValue("@SeniorId", seniorId);
                        var seniorExists = Convert.ToInt32(await seniorCheckCommand.ExecuteScalarAsync()) > 0;
                        if (!seniorExists)
                        {
                            return Json(new { success = false, message = "Senior not found or inactive" });
                        }
                    }

                    // Check existing attendance
                    var existingQuery = @"
                        SELECT AttendanceStatus 
                        FROM event_attendance 
                        WHERE EventId = @EventId AND SeniorId = @SeniorId";

                    string existingStatus = null;
                    await using (var existingCommand = new MySqlCommand(existingQuery, connection))
                    {
                        existingCommand.Parameters.AddWithValue("@EventId", id);
                        existingCommand.Parameters.AddWithValue("@SeniorId", seniorId);
                        var result = await existingCommand.ExecuteScalarAsync();
                        existingStatus = result?.ToString();
                    }

                    // If already present, cannot change to absent
                    if (existingStatus == "Present" && status == "Absent")
                    {
                        return Json(new { success = false, message = "Cannot change Present to Absent once marked" });
                    }

                    if (existingStatus == null)
                    {
                        // Insert new attendance
                        var insertQuery = @"
                            INSERT INTO event_attendance (EventId, SeniorId, AttendanceStatus, MarkedBy, MarkedAt)
                            VALUES (@EventId, @SeniorId, @Status, @MarkedBy, @MarkedAt)";

                        await using (var insertCommand = new MySqlCommand(insertQuery, connection))
                        {
                            insertCommand.Parameters.AddWithValue("@EventId", id);
                            insertCommand.Parameters.AddWithValue("@SeniorId", seniorId);
                            insertCommand.Parameters.AddWithValue("@Status", status);
                            insertCommand.Parameters.AddWithValue("@MarkedBy", User.Identity?.Name);
                            insertCommand.Parameters.AddWithValue("@MarkedAt", DateTime.UtcNow);
                            await insertCommand.ExecuteNonQueryAsync();
                        }
                    }
                    else if (existingStatus != status)
                    {
                        // Update existing attendance
                        var updateQuery = @"
                            UPDATE event_attendance 
                            SET AttendanceStatus = @Status, 
                                MarkedBy = @MarkedBy, 
                                MarkedAt = @MarkedAt
                            WHERE EventId = @EventId AND SeniorId = @SeniorId";

                        await using (var updateCommand = new MySqlCommand(updateQuery, connection))
                        {
                            updateCommand.Parameters.AddWithValue("@EventId", id);
                            updateCommand.Parameters.AddWithValue("@SeniorId", seniorId);
                            updateCommand.Parameters.AddWithValue("@Status", status);
                            updateCommand.Parameters.AddWithValue("@MarkedBy", User.Identity?.Name);
                            updateCommand.Parameters.AddWithValue("@MarkedAt", DateTime.UtcNow);
                            await updateCommand.ExecuteNonQueryAsync();
                        }
                    }

                    // Update event attendance count
                    var countQuery = @"
                        SELECT COUNT(*) 
                        FROM event_attendance 
                        WHERE EventId = @EventId AND AttendanceStatus = 'Present'";

                    int presentCount = 0;
                    await using (var countCommand = new MySqlCommand(countQuery, connection))
                    {
                        countCommand.Parameters.AddWithValue("@EventId", id);
                        presentCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
                    }

                    // Update event table
                    var updateEventQuery = @"
                        UPDATE events 
                        SET AttendanceCount = @AttendanceCount,
                            UpdatedAt = @UpdatedAt
                        WHERE Id = @Id";

                    await using (var updateEventCommand = new MySqlCommand(updateEventQuery, connection))
                    {
                        updateEventCommand.Parameters.AddWithValue("@Id", id);
                        updateEventCommand.Parameters.AddWithValue("@AttendanceCount", presentCount);
                        updateEventCommand.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
                        await updateEventCommand.ExecuteNonQueryAsync();
                    }

                    return Json(new
                    {
                        success = true,
                        message = $"Attendance marked as {status}",
                        attendanceCount = presentCount
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: /Event/BulkMarkAttendance/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkMarkAttendance(int id, [FromForm] List<string> seniorIds, [FromForm] string status)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            if (seniorIds == null || !seniorIds.Any())
            {
                return Json(new { success = false, message = "No seniors selected" });
            }

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check event exists and permissions
                    var checkQuery = @"
                        SELECT OrganizedBy, Status 
                        FROM events 
                        WHERE Id = @Id AND IsDeleted = 0";

                    string organizedBy = "";
                    string eventStatus = "";

                    await using (var checkCommand = new MySqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Id", id);
                        await using (var reader = await checkCommand.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                            {
                                return Json(new { success = false, message = "Event not found" });
                            }
                            organizedBy = reader.GetString("OrganizedBy");
                            eventStatus = reader.GetString("Status");
                        }
                    }

                    // Check permissions
                    if (isStaff && !organizedBy.Contains(User.Identity?.Name))
                    {
                        return Json(new { success = false, message = "You don't have permission to mark attendance for this event" });
                    }

                    // Check if event is cancelled
                    if (eventStatus == "Cancelled")
                    {
                        return Json(new { success = false, message = "Cannot mark attendance for cancelled events" });
                    }

                    int markedCount = 0;
                    string currentUser = User.Identity?.Name;

                    foreach (var seniorId in seniorIds)
                    {
                        // Check if senior exists and is active
                        var seniorCheckQuery = "SELECT COUNT(*) FROM seniors WHERE SeniorId = @SeniorId AND Status = 'Active'";
                        await using (var seniorCheckCommand = new MySqlCommand(seniorCheckQuery, connection))
                        {
                            seniorCheckCommand.Parameters.AddWithValue("@SeniorId", seniorId);
                            var seniorExists = Convert.ToInt32(await seniorCheckCommand.ExecuteScalarAsync()) > 0;
                            if (!seniorExists) continue;
                        }

                        // Check existing attendance
                        var existingQuery = @"
                            SELECT AttendanceStatus 
                            FROM event_attendance 
                            WHERE EventId = @EventId AND SeniorId = @SeniorId";

                        string existingStatus = null;
                        await using (var existingCommand = new MySqlCommand(existingQuery, connection))
                        {
                            existingCommand.Parameters.AddWithValue("@EventId", id);
                            existingCommand.Parameters.AddWithValue("@SeniorId", seniorId);
                            var result = await existingCommand.ExecuteScalarAsync();
                            existingStatus = result?.ToString();
                        }

                        // Skip if already Present and trying to change to Absent
                        if (existingStatus == "Present" && status == "Absent")
                        {
                            continue;
                        }

                        if (existingStatus == null)
                        {
                            // Insert new attendance
                            var insertQuery = @"
        INSERT INTO event_attendance (EventId, SeniorId, AttendanceStatus, MarkedBy, MarkedAt)
        VALUES (@EventId, @SeniorId, @Status, @MarkedBy, @MarkedAt)";

                            await using (var insertCommand = new MySqlCommand(insertQuery, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@EventId", id);
                                insertCommand.Parameters.AddWithValue("@SeniorId", seniorId);
                                insertCommand.Parameters.AddWithValue("@Status", status);
                                insertCommand.Parameters.AddWithValue("@MarkedBy", currentUser);
                                insertCommand.Parameters.AddWithValue("@MarkedAt", DateTime.UtcNow);
                                await insertCommand.ExecuteNonQueryAsync();
                            }
                            markedCount++;
                        }
                        else if (existingStatus != status)
                        {
                            // Update existing attendance
                            var updateQuery = @"
        UPDATE event_attendance 
        SET AttendanceStatus = @Status, 
            MarkedBy = @MarkedBy, 
            MarkedAt = @MarkedAt
        WHERE EventId = @EventId AND SeniorId = @SeniorId";

                            await using (var updateCommand = new MySqlCommand(updateQuery, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@EventId", id);
                                updateCommand.Parameters.AddWithValue("@SeniorId", seniorId);
                                updateCommand.Parameters.AddWithValue("@Status", status);
                                updateCommand.Parameters.AddWithValue("@MarkedBy", currentUser);
                                updateCommand.Parameters.AddWithValue("@MarkedAt", DateTime.UtcNow);
                                await updateCommand.ExecuteNonQueryAsync();
                            }
                            markedCount++;
                        }
                    }

                    // Update event attendance count
                    var countQuery = @"
    SELECT COUNT(*) 
    FROM event_attendance 
    WHERE EventId = @EventId AND AttendanceStatus = 'Present'";

                    int presentCount = 0;
                    await using (var countCommand = new MySqlCommand(countQuery, connection))
                    {
                        countCommand.Parameters.AddWithValue("@EventId", id);
                        presentCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
                    }

                    // Update event table
                    var updateEventQuery = @"
    UPDATE events 
    SET AttendanceCount = @AttendanceCount,
        UpdatedAt = @UpdatedAt
    WHERE Id = @Id";

                    await using (var updateEventCommand = new MySqlCommand(updateEventQuery, connection))
                    {
                        updateEventCommand.Parameters.AddWithValue("@Id", id);
                        updateEventCommand.Parameters.AddWithValue("@AttendanceCount", presentCount);
                        updateEventCommand.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
                        await updateEventCommand.ExecuteNonQueryAsync();
                    }

                    return Json(new
                    {
                        success = true,
                        message = $"Marked {markedCount} seniors as {status}",
                        attendanceCount = presentCount
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: /Event/UpdateStatus/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, [FromForm] string status)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            if (string.IsNullOrEmpty(_connectionString))
            {
                return Json(new { success = false, message = "Database connection error" });
            }

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Get current status and organizer
                    var currentStatusQuery = @"
                SELECT Status, OrganizedBy 
                FROM events 
                WHERE Id = @Id AND IsDeleted = 0";

                    string currentStatus = "";
                    string organizedBy = "";

                    await using (var statusCommand = new MySqlCommand(currentStatusQuery, connection))
                    {
                        statusCommand.Parameters.AddWithValue("@Id", id);
                        await using (var reader = await statusCommand.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                            {
                                return Json(new { success = false, message = "Event not found" });
                            }
                            currentStatus = reader.GetString("Status");
                            organizedBy = reader.GetString("OrganizedBy");
                        }
                    }

                    // Check permissions for staff
                    if (isStaff && !organizedBy.Contains(User.Identity?.Name))
                    {
                        return Json(new { success = false, message = "You can only update status for events you organized." });
                    }

                    // Prevent changing from Completed
                    if (currentStatus == "Completed" && status != "Completed")
                    {
                        return Json(new { success = false, message = "Cannot change status from Completed." });
                    }

                    // If changing to Cancelled, automatically archive
                    if (status == "Cancelled")
                    {
                        var archiveQuery = @"
                    UPDATE events SET
                        Status = @Status,
                        IsDeleted = 1,
                        DeletedAt = @DeletedAt,
                        UpdatedAt = @UpdatedAt
                    WHERE Id = @Id";

                        await using (var command = new MySqlCommand(archiveQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Id", id);
                            command.Parameters.AddWithValue("@Status", status);
                            command.Parameters.AddWithValue("@DeletedAt", DateTime.UtcNow);
                            command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
                            await command.ExecuteNonQueryAsync();
                        }

                        return Json(new
                        {
                            success = true,
                            message = "Event cancelled and moved to archive",
                            status = status,
                            archived = true
                        });
                    }

                    // Regular status update
                    var query = @"
                UPDATE events SET
                    Status = @Status,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id AND IsDeleted = 0";

                    await using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        command.Parameters.AddWithValue("@Status", status);
                        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Json(new
                            {
                                success = true,
                                message = "Status updated successfully",
                                status = status
                            });
                        }
                        else
                        {
                            return Json(new { success = false, message = "Event not found" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: /Event/Archive/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            if (string.IsNullOrEmpty(_connectionString))
            {
                return Json(new { success = false, message = "Database connection is not configured." });
            }

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check permissions for staff
                    if (isStaff)
                    {
                        var checkQuery = @"
                    SELECT OrganizedBy FROM events 
                    WHERE Id = @Id AND IsDeleted = 0";

                        await using (var checkCommand = new MySqlCommand(checkQuery, connection))
                        {
                            checkCommand.Parameters.AddWithValue("@Id", id);
                            var organizer = (string)await checkCommand.ExecuteScalarAsync();

                            if (organizer == null || !organizer.Contains(User.Identity?.Name))
                            {
                                return Json(new { success = false, message = "You can only archive events that you organized." });
                            }
                        }
                    }

                    var query = @"
                UPDATE events SET
                    IsDeleted = 1,
                    DeletedAt = @DeletedAt,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id AND IsDeleted = 0";

                    await using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        command.Parameters.AddWithValue("@DeletedAt", DateTime.UtcNow);
                        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Json(new { success = true, message = "Event archived successfully!" });
                        }
                        else
                        {
                            return Json(new { success = false, message = "Event not found or already archived." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error archiving event: {ex.Message}" });
            }
        }

        // POST: /Event/Restore/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check permissions for staff
                    if (isStaff)
                    {
                        var checkQuery = @"
                    SELECT OrganizedBy FROM events 
                    WHERE Id = @Id AND IsDeleted = 1";

                        await using (var checkCommand = new MySqlCommand(checkQuery, connection))
                        {
                            checkCommand.Parameters.AddWithValue("@Id", id);
                            var organizer = (string)await checkCommand.ExecuteScalarAsync();

                            if (organizer == null || !organizer.Contains(User.Identity?.Name))
                            {
                                TempData["ErrorMessage"] = "You can only restore events that you organized.";
                                return RedirectToAction("Archived");
                            }
                        }
                    }

                    var query = @"
                UPDATE events SET
                    IsDeleted = 0,
                    DeletedAt = NULL,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id AND IsDeleted = 1";

                    await using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "Event restored successfully!";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Event not found or not archived.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error restoring event: {ex.Message}";
            }

            return RedirectToAction("Archived");
        }

        // POST: /Event/PermanentDelete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check permissions for staff
                    if (isStaff)
                    {
                        var checkQuery = @"
                    SELECT OrganizedBy FROM events 
                    WHERE Id = @Id AND IsDeleted = 1";

                        await using (var checkCommand = new MySqlCommand(checkQuery, connection))
                        {
                            checkCommand.Parameters.AddWithValue("@Id", id);
                            var organizer = (string)await checkCommand.ExecuteScalarAsync();

                            if (organizer == null || !organizer.Contains(User.Identity?.Name))
                            {
                                return Json(new
                                {
                                    success = false,
                                    message = "You can only delete events that you organized."
                                });
                            }
                        }
                    }

                    // Delete attendance records first
                    var deleteAttendanceQuery = "DELETE FROM event_attendance WHERE EventId = @Id";
                    await using (var deleteAttendanceCommand = new MySqlCommand(deleteAttendanceQuery, connection))
                    {
                        deleteAttendanceCommand.Parameters.AddWithValue("@Id", id);
                        await deleteAttendanceCommand.ExecuteNonQueryAsync();
                    }

                    // Get event title for confirmation message
                    var getTitleQuery = "SELECT EventTitle FROM events WHERE Id = @Id";
                    string eventTitle = "";

                    await using (var getTitleCommand = new MySqlCommand(getTitleQuery, connection))
                    {
                        getTitleCommand.Parameters.AddWithValue("@Id", id);
                        var result = await getTitleCommand.ExecuteScalarAsync();
                        eventTitle = result?.ToString() ?? "Unknown Event";
                    }

                    // Delete event
                    var deleteEventQuery = "DELETE FROM events WHERE Id = @Id AND IsDeleted = 1";

                    await using (var command = new MySqlCommand(deleteEventQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Json(new
                            {
                                success = true,
                                message = $"Event '{eventTitle}' permanently deleted!"
                            });
                        }
                        else
                        {
                            return Json(new
                            {
                                success = false,
                                message = "Event not found or not archived."
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error deleting event: {ex.Message}"
                });
            }
        }

        // Helper method to load event statistics
        private async Task LoadEventStatistics()
        {
            if (string.IsNullOrEmpty(_connectionString))
                return;

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Get total events count
                    var totalQuery = "SELECT COUNT(*) FROM events WHERE IsDeleted = 0";
                    using (var command = new MySqlCommand(totalQuery, connection))
                    {
                        ViewBag.TotalEvents = Convert.ToInt32(await command.ExecuteScalarAsync());
                    }

                    // Get upcoming events count
                    var upcomingQuery = "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND Status = 'Upcoming'";
                    using (var command = new MySqlCommand(upcomingQuery, connection))
                    {
                        ViewBag.UpcomingEvents = Convert.ToInt32(await command.ExecuteScalarAsync());
                    }

                    // Get today's events count
                    var todayQuery = "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND DATE(EventDate) = CURDATE()";
                    using (var command = new MySqlCommand(todayQuery, connection))
                    {
                        ViewBag.TodayEvents = Convert.ToInt32(await command.ExecuteScalarAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently fail statistics
                Console.WriteLine($"Error loading statistics: {ex.Message}");
            }
        }

        // Helper method to load archived statistics
        private async Task LoadArchivedStatistics()
        {
            if (string.IsNullOrEmpty(_connectionString))
                return;

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Get total archived count
                    var totalQuery = "SELECT COUNT(*) FROM events WHERE IsDeleted = 1";
                    using (var command = new MySqlCommand(totalQuery, connection))
                    {
                        ViewBag.TotalArchived = Convert.ToInt32(await command.ExecuteScalarAsync());
                    }

                    // Get archived this month
                    var thisMonthQuery = @"SELECT COUNT(*) FROM events 
                                 WHERE IsDeleted = 1 
                                 AND MONTH(DeletedAt) = MONTH(CURDATE()) 
                                 AND YEAR(DeletedAt) = YEAR(CURDATE())";
                    using (var command = new MySqlCommand(thisMonthQuery, connection))
                    {
                        ViewBag.ArchivedThisMonth = Convert.ToInt32(await command.ExecuteScalarAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently fail statistics
                Console.WriteLine($"Error loading archived statistics: {ex.Message}");
            }
        }
    }
}
