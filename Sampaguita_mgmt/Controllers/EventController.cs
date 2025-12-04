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
using System.Security.Claims;

namespace SeniorManagement.Controllers
{
    public class EventController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public EventController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("MySqlConnection");
        }

        // GET: /Event
        // GET: /Event/Index
        public async Task<IActionResult> Index(string status = null, string type = null)
        {
            // Set admin status for the view
            bool isAdmin = User.IsInRole("Administrator");
            ViewBag.IsAdmin = isAdmin;

            // Check connection string
            if (string.IsNullOrEmpty(_connectionString))
            {
                ViewBag.Error = "Database connection is not configured. Please check appsettings.json";
                return View(new List<Event>());
            }

            var events = new List<Event>();

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        SELECT Id, eventTitle, eventDescription, eventType, eventDate, eventTime, 
                               eventLocation, OrganizedBy, max_capacity, attendance_count, 
                               status, created_at, updated_at, is_deleted, deleted_at
                        FROM events
                        WHERE is_deleted = 0";

                    var parameters = new List<MySqlParameter>();

                    if (!string.IsNullOrEmpty(status))
                    {
                        query += " AND status = @Status";
                        parameters.Add(new MySqlParameter("@Status", status));
                    }

                    if (!string.IsNullOrEmpty(type))
                    {
                        query += " AND eventType = @Type";
                        parameters.Add(new MySqlParameter("@Type", type));
                    }

                    query += " ORDER BY eventDate, eventTime";

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
                                    EventTitle = reader.GetString("eventTitle"),
                                    EventDescription = reader.GetString("eventDescription"),
                                    EventType = reader.GetString("eventType"),
                                    EventDate = reader.GetDateTime("eventDate"),
                                    EventTime = reader.GetTimeSpan("eventTime"),
                                    EventLocation = reader.GetString("eventLocation"),
                                    OrganizedBy = reader.GetString("OrganizedBy"),
                                    MaxCapacity = reader.IsDBNull(reader.GetOrdinal("max_capacity"))
                                        ? null : (int?)reader.GetInt32("max_capacity"),
                                    AttendanceCount = reader.GetInt32("attendance_count"),
                                    Status = reader.GetString("status"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    UpdatedAt = reader.GetDateTime("updated_at"),
                                    IsDeleted = reader.GetBoolean("is_deleted"),
                                    DeletedAt = reader.IsDBNull(reader.GetOrdinal("deleted_at"))
                                        ? null : (DateTime?)reader.GetDateTime("deleted_at")
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
                Console.WriteLine($"Error: {ex.Message}");
            }

            return View(events);
        }

        // GET: /Event/Archived
        public async Task<IActionResult> Archived()
        {
            // Set admin status
            bool isAdmin = User.IsInRole("Administrator");
            ViewBag.IsAdmin = isAdmin;

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
                        SELECT Id, eventTitle, eventDescription, eventType, eventDate, eventTime, 
                               eventLocation, OrganizedBy, max_capacity, attendance_count, 
                               status, created_at, updated_at, is_deleted, deleted_at
                        FROM events
                        WHERE is_deleted = 1
                        ORDER BY deleted_at DESC";

                    await using (var command = new MySqlCommand(query, connection))
                    await using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var evt = new Event
                            {
                                Id = reader.GetInt32("Id"),
                                EventTitle = reader.GetString("eventTitle"),
                                EventDescription = reader.GetString("eventDescription"),
                                EventType = reader.GetString("eventType"),
                                EventDate = reader.GetDateTime("eventDate"),
                                EventTime = reader.GetTimeSpan("eventTime"),
                                EventLocation = reader.GetString("eventLocation"),
                                OrganizedBy = reader.GetString("OrganizedBy"),
                                MaxCapacity = reader.IsDBNull(reader.GetOrdinal("max_capacity"))
                                    ? null : (int?)reader.GetInt32("max_capacity"),
                                AttendanceCount = reader.GetInt32("attendance_count"),
                                Status = reader.GetString("status"),
                                CreatedAt = reader.GetDateTime("created_at"),
                                UpdatedAt = reader.GetDateTime("updated_at"),
                                IsDeleted = reader.GetBoolean("is_deleted"),
                                DeletedAt = reader.IsDBNull(reader.GetOrdinal("deleted_at"))
                                    ? null : (DateTime?)reader.GetDateTime("deleted_at")
                            };

                            events.Add(evt);
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
            // Set admin status
            bool isAdmin = User.IsInRole("Administrator");
            ViewBag.IsAdmin = isAdmin;
            return View();
        }

        // POST: /Event/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Event eventModel)
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                ModelState.AddModelError("", "Database connection is not configured.");
                return View(eventModel);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await using (var connection = new MySqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        var query = @"
                            INSERT INTO events (
                                eventTitle, eventDescription, eventType, eventDate, eventTime,
                                eventLocation, OrganizedBy, max_capacity, attendance_count,
                                status, created_at, updated_at
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

        // GET: /Event/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            // Set admin status
            bool isAdmin = User.IsInRole("Administrator");
            ViewBag.IsAdmin = isAdmin;

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
                        SELECT Id, eventTitle, eventDescription, eventType, eventDate, eventTime, 
                               eventLocation, OrganizedBy, max_capacity, attendance_count, 
                               status, created_at, updated_at, is_deleted, deleted_at
                        FROM events
                        WHERE Id = @Id AND is_deleted = 0";

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
                                    EventTitle = reader.GetString("eventTitle"),
                                    EventDescription = reader.GetString("eventDescription"),
                                    EventType = reader.GetString("eventType"),
                                    EventDate = reader.GetDateTime("eventDate"),
                                    EventTime = reader.GetTimeSpan("eventTime"),
                                    EventLocation = reader.GetString("eventLocation"),
                                    OrganizedBy = reader.GetString("OrganizedBy"),
                                    MaxCapacity = reader.IsDBNull(reader.GetOrdinal("max_capacity"))
                                        ? null : (int?)reader.GetInt32("max_capacity"),
                                    AttendanceCount = reader.GetInt32("attendance_count"),
                                    Status = reader.GetString("status"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    UpdatedAt = reader.GetDateTime("updated_at"),
                                    IsDeleted = reader.GetBoolean("is_deleted"),
                                    DeletedAt = reader.IsDBNull(reader.GetOrdinal("deleted_at"))
                                        ? null : (DateTime?)reader.GetDateTime("deleted_at")
                                };

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
            if (id != eventModel.Id)
                return BadRequest();

            if (string.IsNullOrEmpty(_connectionString))
            {
                ModelState.AddModelError("", "Database connection is not configured.");
                return View(eventModel);
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
                                eventTitle = @EventTitle,
                                eventDescription = @EventDescription,
                                eventType = @EventType,
                                eventDate = @EventDate,
                                eventTime = @EventTime,
                                eventLocation = @EventLocation,
                                OrganizedBy = @OrganizedBy,
                                max_capacity = @MaxCapacity,
                                attendance_count = @AttendanceCount,
                                status = @Status,
                                updated_at = @UpdatedAt
                            WHERE Id = @Id AND is_deleted = 0";

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

        // POST: /Event/Archive/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                TempData["ErrorMessage"] = "Database connection is not configured.";
                return RedirectToAction("Index");
            }

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        UPDATE events SET
                            is_deleted = 1,
                            deleted_at = @DeletedAt,
                            updated_at = @UpdatedAt
                        WHERE Id = @Id AND is_deleted = 0";

                    await using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        command.Parameters.AddWithValue("@DeletedAt", DateTime.UtcNow);
                        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "Event archived successfully!";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Event not found or already archived.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error archiving event: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // POST: /Event/Restore/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                TempData["ErrorMessage"] = "Database connection is not configured.";
                return RedirectToAction("Archived");
            }

            try
            {
                await using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        UPDATE events SET
                            is_deleted = 0,
                            deleted_at = NULL,
                            updated_at = @UpdatedAt
                        WHERE Id = @Id AND is_deleted = 1";

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
    }
}