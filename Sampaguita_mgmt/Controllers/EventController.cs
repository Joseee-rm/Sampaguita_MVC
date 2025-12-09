// Controllers/EventController.cs
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
            string userName = User.Identity.Name;

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsStaff = isStaff;
            ViewBag.UserName = userName;

            // Store filter values for UI
            ViewBag.SearchQuery = search;
            ViewBag.SelectedStatus = status;
            ViewBag.SelectedType = type;
            ViewBag.SelectedDateFilter = dateFilter;

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
                               Status, CreatedAt, UpdatedAt, IsDeleted, DeletedAt
                        FROM events
                        WHERE IsDeleted = 0";

                    var parameters = new List<MySqlParameter>();

                    // Apply search filter
                    if (!string.IsNullOrEmpty(search))
                    {
                        query += " AND (EventTitle LIKE @Search OR EventDescription LIKE @Search OR EventLocation LIKE @Search OR OrganizedBy LIKE @Search)";
                        parameters.Add(new MySqlParameter("@Search", $"%{search}%"));
                    }

                    // Apply status filter
                    if (!string.IsNullOrEmpty(status))
                    {
                        query += " AND Status = @Status";
                        parameters.Add(new MySqlParameter("@Status", status));
                    }

                    // Apply type filter
                    if (!string.IsNullOrEmpty(type))
                    {
                        query += " AND EventType = @Type";
                        parameters.Add(new MySqlParameter("@Type", type));
                    }

                    // Apply date filter
                    if (!string.IsNullOrEmpty(dateFilter))
                    {
                        var now = DateTime.Now;
                        switch (dateFilter)
                        {
                            case "today":
                                query += " AND DATE(EventDate) = CURDATE()";
                                break;
                            case "tomorrow":
                                query += " AND DATE(EventDate) = DATE_ADD(CURDATE(), INTERVAL 1 DAY)";
                                break;
                            case "thisweek":
                                query += " AND YEARWEEK(EventDate, 1) = YEARWEEK(CURDATE(), 1)";
                                break;
                            case "nextweek":
                                query += " AND YEARWEEK(EventDate, 1) = YEARWEEK(CURDATE(), 1) + 1";
                                break;
                            case "thismonth":
                                query += " AND MONTH(EventDate) = MONTH(CURDATE()) AND YEAR(EventDate) = YEAR(CURDATE())";
                                break;
                            case "nextmonth":
                                query += " AND MONTH(EventDate) = MONTH(DATE_ADD(CURDATE(), INTERVAL 1 MONTH)) AND YEAR(EventDate) = YEAR(DATE_ADD(CURDATE(), INTERVAL 1 MONTH))";
                                break;
                            case "past":
                                query += " AND EventDate < CURDATE()";
                                break;
                            case "upcoming":
                                query += " AND EventDate >= CURDATE()";
                                break;
                        }
                    }

                    // Staff can only see events they organized or are participating in
                    if (isStaff)
                    {
                        query += " AND (OrganizedBy LIKE @StaffName)";
                        parameters.Add(new MySqlParameter("@StaffName", $"%{userName}%"));
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
                ViewBag.Error = $"Database error: {ex.Message}";
            }

            // Get statistics for filter sidebar
            await LoadEventStatistics();

            return View(events);
        }

        // GET: /Event/Archived
        public async Task<IActionResult> Archived(string search = "", string status = "", string type = "", string dateFilter = "")
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");
            string userName = User.Identity.Name;

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsStaff = isStaff;
            ViewBag.UserName = userName;

            // Store filter values for UI
            ViewBag.SearchQuery = search;
            ViewBag.SelectedStatus = status;
            ViewBag.SelectedType = type;
            ViewBag.SelectedDateFilter = dateFilter;

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
                               Status, CreatedAt, UpdatedAt, IsDeleted, DeletedAt
                        FROM events
                        WHERE IsDeleted = 1";

                    var parameters = new List<MySqlParameter>();

                    // Apply search filter
                    if (!string.IsNullOrEmpty(search))
                    {
                        query += " AND (EventTitle LIKE @Search OR EventDescription LIKE @Search OR EventLocation LIKE @Search OR OrganizedBy LIKE @Search)";
                        parameters.Add(new MySqlParameter("@Search", $"%{search}%"));
                    }

                    // Apply status filter
                    if (!string.IsNullOrEmpty(status))
                    {
                        query += " AND Status = @Status";
                        parameters.Add(new MySqlParameter("@Status", status));
                    }

                    // Apply type filter
                    if (!string.IsNullOrEmpty(type))
                    {
                        query += " AND EventType = @Type";
                        parameters.Add(new MySqlParameter("@Type", type));
                    }

                    // Apply date filter (archive date)
                    if (!string.IsNullOrEmpty(dateFilter))
                    {
                        var now = DateTime.Now;
                        switch (dateFilter)
                        {
                            case "today":
                                query += " AND DATE(DeletedAt) = CURDATE()";
                                break;
                            case "thisweek":
                                query += " AND YEARWEEK(DeletedAt, 1) = YEARWEEK(CURDATE(), 1)";
                                break;
                            case "thismonth":
                                query += " AND MONTH(DeletedAt) = MONTH(CURDATE()) AND YEAR(DeletedAt) = YEAR(CURDATE())";
                                break;
                            case "lastmonth":
                                query += " AND MONTH(DeletedAt) = MONTH(DATE_SUB(CURDATE(), INTERVAL 1 MONTH)) AND YEAR(DeletedAt) = YEAR(DATE_SUB(CURDATE(), INTERVAL 1 MONTH))";
                                break;
                            case "older":
                                query += " AND DeletedAt < DATE_SUB(CURDATE(), INTERVAL 3 MONTH)";
                                break;
                        }
                    }

                    // Staff can only see their own archived events
                    if (isStaff)
                    {
                        query += " AND OrganizedBy LIKE @StaffName";
                        parameters.Add(new MySqlParameter("@StaffName", $"%{userName}%"));
                    }

                    query += " ORDER BY DeletedAt DESC";

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
                ViewBag.Error = $"Database error: {ex.Message}";
            }

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
                            command.Parameters.AddWithValue("@StaffName", $"%{User.Identity.Name}%");
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
            string userName = User.Identity.Name;

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
            string userName = User.Identity.Name;

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

                            command.Parameters.AddWithValue("@AttendanceCount", eventModel.AttendanceCount);
                            command.Parameters.AddWithValue("@Status", eventModel.Status ?? "Scheduled");
                            command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                            command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    TempData["SuccessMessage"] = "Event created successfully!";
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
                                if (isStaff && !evt.OrganizedBy.Contains(User.Identity.Name))
                                {
                                    TempData["ErrorMessage"] = "You can only view events that you organized.";
                                    return RedirectToAction("Index");
                                }

                                ViewBag.CanEdit = isAdmin || (isStaff && evt.OrganizedBy.Contains(User.Identity.Name));
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
                                if (isStaff && !evt.OrganizedBy.Contains(User.Identity.Name))
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
                            SELECT OrganizedBy FROM events 
                            WHERE Id = @Id AND IsDeleted = 0";

                        await using (var checkCommand = new MySqlCommand(checkQuery, connection))
                        {
                            checkCommand.Parameters.AddWithValue("@Id", id);
                            var organizer = (string)await checkCommand.ExecuteScalarAsync();

                            if (organizer == null || !organizer.Contains(User.Identity.Name))
                            {
                                TempData["ErrorMessage"] = "You can only edit events that you organized.";
                                return RedirectToAction("Index");
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
                            command.Parameters.AddWithValue("@Status", eventModel.Status ?? "Scheduled");
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

        // POST: /Event/UpdateAttendance/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAttendance(int id, [FromForm] int? newAttendance, [FromForm] int? count)
        {
            bool isAdmin = User.IsInRole("Administrator");
            bool isStaff = User.IsInRole("Staff");

            int actualAttendance = 0;
            bool isIncrement = false;

            if (count.HasValue)
            {
                // Increment/Decrement mode
                actualAttendance = count.Value;
                isIncrement = true;
            }
            else if (newAttendance.HasValue)
            {
                // Direct set mode
                actualAttendance = newAttendance.Value;
            }
            else
            {
                return Json(new { success = false, message = "No attendance value provided." });
            }

            if (string.IsNullOrEmpty(_connectionString))
            {
                return Json(new { success = false, message = "Database connection is not configured." });
            }

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Get current event details
                    var checkQuery = @"
                        SELECT EventTitle, MaxCapacity, AttendanceCount, OrganizedBy, Status 
                        FROM events WHERE Id = @Id AND IsDeleted = 0";

                    string eventTitle = "";
                    int? maxCapacity = null;
                    int currentAttendance = 0;
                    string organizedBy = "";
                    string status = "";

                    await using (var checkCommand = new MySqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Id", id);

                        await using (var reader = await checkCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                eventTitle = reader.GetString("EventTitle");
                                maxCapacity = reader.IsDBNull(reader.GetOrdinal("MaxCapacity"))
                                    ? null : (int?)reader.GetInt32("MaxCapacity");
                                currentAttendance = reader.GetInt32("AttendanceCount");
                                organizedBy = reader.GetString("OrganizedBy");
                                status = reader.GetString("Status");
                            }
                            else
                            {
                                return Json(new { success = false, message = "Event not found." });
                            }
                        }
                    }

                    // Check permissions
                    if (isStaff && !organizedBy.Contains(User.Identity.Name))
                    {
                        return Json(new { success = false, message = "You can only update attendance for events you organized." });
                    }

                    // Calculate new attendance
                    int newAttendanceValue;
                    if (isIncrement)
                    {
                        newAttendanceValue = currentAttendance + actualAttendance;
                    }
                    else
                    {
                        newAttendanceValue = actualAttendance;
                    }

                    if (newAttendanceValue < 0)
                    {
                        return Json(new { success = false, message = "Attendance cannot be negative." });
                    }

                    // Check capacity
                    if (maxCapacity.HasValue && newAttendanceValue > maxCapacity.Value)
                    {
                        return Json(new { success = false, message = $"Cannot exceed maximum capacity of {maxCapacity.Value}." });
                    }

                    // Update attendance
                    var updateQuery = @"
                        UPDATE events SET
                            AttendanceCount = @AttendanceCount,
                            UpdatedAt = @UpdatedAt
                        WHERE Id = @Id AND IsDeleted = 0";

                    await using (var command = new MySqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        command.Parameters.AddWithValue("@AttendanceCount", newAttendanceValue);
                        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Json(new
                            {
                                success = true,
                                message = $"Attendance updated to {newAttendanceValue}",
                                attendance = newAttendanceValue,
                                isFull = maxCapacity.HasValue && newAttendanceValue >= maxCapacity.Value,
                                percentage = maxCapacity.HasValue ? (int)Math.Round((double)newAttendanceValue / maxCapacity.Value * 100) : 0
                            });
                        }
                        else
                        {
                            return Json(new { success = false, message = "Failed to update attendance." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error updating attendance: {ex.Message}" });
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
                // Check if staff has permission
                if (isStaff)
                {
                    await using (var connection = new MySqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        var checkQuery = @"
                            SELECT OrganizedBy FROM events 
                            WHERE Id = @Id AND IsDeleted = 0";

                        await using (var checkCommand = new MySqlCommand(checkQuery, connection))
                        {
                            checkCommand.Parameters.AddWithValue("@Id", id);
                            var organizer = (string)await checkCommand.ExecuteScalarAsync();

                            if (organizer == null || !organizer.Contains(User.Identity.Name))
                            {
                                return Json(new { success = false, message = "You can only update status for events you organized." });
                            }
                        }
                    }
                }

                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

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

                            if (organizer == null || !organizer.Contains(User.Identity.Name))
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

                            if (organizer == null || !organizer.Contains(User.Identity.Name))
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

                            if (organizer == null || !organizer.Contains(User.Identity.Name))
                            {
                                return Json(new
                                {
                                    success = false,
                                    message = "You can only delete events that you organized."
                                });
                            }
                        }
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

                    var query = "DELETE FROM events WHERE Id = @Id AND IsDeleted = 1";

                    await using (var command = new MySqlCommand(query, connection))
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

        // POST: /Event/BulkDeleteArchived - Admin only
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> BulkDeleteArchived()
        {
            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // First, count how many events will be deleted
                    var countQuery = "SELECT COUNT(*) FROM events WHERE IsDeleted = 1";
                    int eventCount = 0;

                    await using (var countCommand = new MySqlCommand(countQuery, connection))
                    {
                        eventCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
                    }

                    if (eventCount == 0)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "No archived events to delete."
                        });
                    }

                    var deleteQuery = "DELETE FROM events WHERE IsDeleted = 1";

                    await using (var command = new MySqlCommand(deleteQuery, connection))
                    {
                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        return Json(new
                        {
                            success = true,
                            message = $"Successfully deleted {rowsAffected} archived events!",
                            count = rowsAffected
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Error: {ex.Message}"
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
                    var upcomingQuery = "SELECT COUNT(*) FROM events WHERE IsDeleted = 0 AND EventDate >= CURDATE()";
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