// EventController.cs - Replace the entire file with this updated version
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SeniorManagement.Models;
using SeniorManagement.Helpers;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Linq;

namespace SeniorManagement.Controllers
{
    [Authorize]
    public class EventController : BaseController
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly NotificationHelper _notificationHelper;
        private readonly ActivityHelper _activityHelper;

        public EventController(DatabaseHelper dbHelper, ActivityHelper activityHelper)
        {
            _dbHelper = dbHelper;
            _notificationHelper = new NotificationHelper(dbHelper);
            _activityHelper = activityHelper;
        }

        // Events List Page
        public IActionResult Index()
        {
            var events = GetAllEvents();
            return View(events);
        }

        // Archived Events Page
        public IActionResult Archived()
        {
            var events = GetArchivedEvents();
            return View(events);
        }

        // Create Event - GET
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // Create Event - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Event eventItem)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the validation errors.";
                return View(eventItem);
            }

            try
            {
                if (CreateEventInDatabase(eventItem))
                {
                    var currentUser = HttpContext.Session.GetString("UserName") ?? "System";
                    await _notificationHelper.CreateEventAnnouncementAsync(eventItem, currentUser);

                    // Log the activity
                    await _activityHelper.LogActivityAsync(
                        "Create Event",
                        $"Created event: '{eventItem.EventTitle}' on {eventItem.EventDate:yyyy-MM-dd}"
                    );

                    TempData["SuccessMessage"] = "Event created successfully!";
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["ErrorMessage"] = "Error creating event. Please try again.";
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Create Event");
                Console.WriteLine($"Error creating event: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while creating the event.";
            }

            return View(eventItem);
        }

        // Edit Event - GET
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var eventItem = GetEventById(id);
            if (eventItem == null || eventItem.IsDeleted)
            {
                TempData["ErrorMessage"] = "Event not found.";
                return RedirectToAction("Index");
            }
            return View(eventItem);
        }

        // Edit Event - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Event eventItem)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the validation errors.";
                return View(eventItem);
            }

            try
            {
                eventItem.UpdatedAt = DateTime.Now;

                if (UpdateEventInDatabase(eventItem))
                {
                    var currentUser = HttpContext.Session.GetString("UserName") ?? "System";
                    await _notificationHelper.CreateEventUpdateAnnouncementAsync(eventItem, currentUser);

                    // Log the activity
                    await _activityHelper.LogActivityAsync(
                        "Edit Event",
                        $"Updated event: '{eventItem.EventTitle}' (ID: {eventItem.Id})"
                    );

                    TempData["SuccessMessage"] = "Event updated successfully!";
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["ErrorMessage"] = "Error updating event. Please try again.";
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Edit Event");
                Console.WriteLine($"Error updating event: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while updating the event.";
            }

            return View(eventItem);
        }

        // Soft Delete Event
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            try
            {
                var eventItem = GetEventById(id);

                if (eventItem != null && !eventItem.IsDeleted)
                {
                    if (SoftDeleteEvent(id))
                    {
                        var currentUser = HttpContext.Session.GetString("UserName") ?? "System";
                        await _notificationHelper.CreateEventDeleteAnnouncementAsync(eventItem, currentUser);

                        // Log the activity
                        await _activityHelper.LogActivityAsync(
                            "Archive Event",
                            $"Archived event: '{eventItem.EventTitle}' (ID: {id})"
                        );

                        TempData["SuccessMessage"] = "Event archived successfully!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Error archiving event.";
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "Event not found or already archived.";
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Archive Event");
                Console.WriteLine($"Error archiving event: {ex.Message}");
                TempData["ErrorMessage"] = "Error archiving event. Please try again.";
            }

            return RedirectToAction("Index");
        }

        // Restore Archived Event
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            try
            {
                if (RestoreEvent(id))
                {
                    var eventItem = GetEventById(id);

                    // Log the activity
                    await _activityHelper.LogActivityAsync(
                        "Restore Event",
                        $"Restored event: '{eventItem?.EventTitle}' (ID: {id})"
                    );

                    TempData["SuccessMessage"] = "Event restored successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Error restoring event.";
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Restore Event");
                Console.WriteLine($"Error restoring event: {ex.Message}");
                TempData["ErrorMessage"] = "Error restoring event. Please try again.";
            }

            return RedirectToAction("Archived");
        }

        // Update Attendance
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAttendance(int id, int attendanceCount)
        {
            try
            {
                if (UpdateEventAttendance(id, attendanceCount))
                {
                    var eventItem = GetEventById(id);

                    // Log the activity
                    await _activityHelper.LogActivityAsync(
                        "Update Attendance",
                        $"Updated attendance for '{eventItem?.EventTitle}' to {attendanceCount}"
                    );

                    TempData["SuccessMessage"] = "Attendance updated successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Error updating attendance.";
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Update Attendance");
                Console.WriteLine($"Error updating attendance: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while updating attendance.";
            }

            return RedirectToAction("Index");
        }

        // Update Event Status
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            try
            {
                if (UpdateEventStatusInDatabase(id, status))
                {
                    var eventItem = GetEventById(id);

                    // Log the activity
                    await _activityHelper.LogActivityAsync(
                        "Update Event Status",
                        $"Updated status of '{eventItem?.EventTitle}' to '{status}'"
                    );

                    TempData["SuccessMessage"] = $"Event status updated to {status} successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Error updating event status. Please try again.";
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Update Event Status");
                Console.WriteLine($"Error updating event status: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while updating the event status.";
            }

            return RedirectToAction("Index");
        }

        // Database Methods
        private List<Event> GetAllEvents()
        {
            var events = new List<Event>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT Id, eventTitle, eventType, eventDate, eventTime, eventLocation, 
                                   OrganizedBy, eventDescription, status, created_at, updated_at,
                                   attendance_count, max_capacity, is_deleted, deleted_at
                                   FROM events 
                                   WHERE is_deleted = FALSE
                                   ORDER BY eventDate DESC, eventTime DESC";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            events.Add(MapEventFromReader(reader));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting events: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading events.";
            }

            return events;
        }

        private List<Event> GetArchivedEvents()
        {
            var events = new List<Event>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT Id, eventTitle, eventType, eventDate, eventTime, eventLocation, 
                                   OrganizedBy, eventDescription, status, created_at, updated_at,
                                   attendance_count, max_capacity, is_deleted, deleted_at
                                   FROM events 
                                   WHERE is_deleted = TRUE
                                   ORDER BY deleted_at DESC";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            events.Add(MapEventFromReader(reader));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting archived events: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading archived events.";
            }

            return events;
        }

        private Event GetEventById(int id)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT Id, eventTitle, eventType, eventDate, eventTime, eventLocation, 
                                   OrganizedBy, eventDescription, status, created_at, updated_at,
                                   attendance_count, max_capacity, is_deleted, deleted_at
                                   FROM events WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return MapEventFromReader(reader);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting event: {ex.Message}");
            }

            return null;
        }

        private Event MapEventFromReader(MySqlDataReader reader)
        {
            var eventItem = new Event
            {
                Id = reader.GetInt32("Id"),
                EventTitle = reader.GetString("eventTitle"),
                EventType = reader.GetString("eventType"),
                EventDate = reader.GetDateTime("eventDate"),
                EventLocation = reader.GetString("eventLocation"),
                OrganizedBy = reader.GetString("OrganizedBy"),
                EventDescription = reader.GetString("eventDescription"),
                Status = reader.GetString("status"),
                CreatedAt = reader.GetDateTime("created_at"),
                UpdatedAt = reader.GetDateTime("updated_at"),
                AttendanceCount = reader.GetInt32("attendance_count"),
                IsDeleted = reader.GetBoolean("is_deleted")
            };

            // Handle nullable columns
            if (!reader.IsDBNull(reader.GetOrdinal("max_capacity")))
            {
                eventItem.MaxCapacity = reader.GetInt32("max_capacity");
            }

            if (!reader.IsDBNull(reader.GetOrdinal("deleted_at")))
            {
                eventItem.DeletedAt = reader.GetDateTime("deleted_at");
            }

            if (!reader.IsDBNull(reader.GetOrdinal("eventTime")))
            {
                var timeSpan = reader.GetTimeSpan("eventTime");
                eventItem.EventTime = timeSpan.ToString(@"hh\:mm");
            }

            return eventItem;
        }

        private bool CreateEventInDatabase(Event eventItem)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"INSERT INTO events 
                           (eventTitle, eventType, eventDate, eventTime, eventLocation, 
                            OrganizedBy, eventDescription, status, created_at, updated_at,
                            attendance_count, max_capacity, is_deleted)
                           VALUES (@Title, @Type, @Date, @Time, @Location, 
                                   @OrganizedBy, @Description, @Status, @CreatedAt, @UpdatedAt,
                                   @AttendanceCount, @MaxCapacity, FALSE)";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Title", eventItem.EventTitle);
                        cmd.Parameters.AddWithValue("@Type", eventItem.EventType);
                        cmd.Parameters.AddWithValue("@Date", eventItem.EventDate);

                        if (TimeSpan.TryParse(eventItem.EventTime, out TimeSpan timeSpan))
                        {
                            cmd.Parameters.AddWithValue("@Time", timeSpan);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Time", DateTime.Now.TimeOfDay);
                        }

                        cmd.Parameters.AddWithValue("@Location", eventItem.EventLocation);
                        cmd.Parameters.AddWithValue("@OrganizedBy", eventItem.OrganizedBy);
                        cmd.Parameters.AddWithValue("@Description", eventItem.EventDescription);
                        cmd.Parameters.AddWithValue("@Status", eventItem.Status);
                        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@AttendanceCount", eventItem.AttendanceCount);
                        cmd.Parameters.AddWithValue("@MaxCapacity", (object)eventItem.MaxCapacity ?? DBNull.Value);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating event: {ex.Message}");
                return false;
            }
        }

        private bool UpdateEventInDatabase(Event eventItem)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"UPDATE events 
                           SET eventTitle = @Title, eventType = @Type, eventDate = @Date, 
                               eventTime = @Time, eventLocation = @Location, OrganizedBy = @OrganizedBy,
                               eventDescription = @Description, status = @Status, updated_at = @UpdatedAt,
                               attendance_count = @AttendanceCount, max_capacity = @MaxCapacity
                           WHERE Id = @Id AND is_deleted = FALSE";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Title", eventItem.EventTitle);
                        cmd.Parameters.AddWithValue("@Type", eventItem.EventType);
                        cmd.Parameters.AddWithValue("@Date", eventItem.EventDate);

                        if (TimeSpan.TryParse(eventItem.EventTime, out TimeSpan timeSpan))
                        {
                            cmd.Parameters.AddWithValue("@Time", timeSpan);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Time", DateTime.Now.TimeOfDay);
                        }

                        cmd.Parameters.AddWithValue("@Location", eventItem.EventLocation);
                        cmd.Parameters.AddWithValue("@OrganizedBy", eventItem.OrganizedBy);
                        cmd.Parameters.AddWithValue("@Description", eventItem.EventDescription);
                        cmd.Parameters.AddWithValue("@Status", eventItem.Status);
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@AttendanceCount", eventItem.AttendanceCount);
                        cmd.Parameters.AddWithValue("@MaxCapacity", (object)eventItem.MaxCapacity ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Id", eventItem.Id);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating event: {ex.Message}");
                return false;
            }
        }

        private bool SoftDeleteEvent(int id)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"UPDATE events 
                           SET is_deleted = TRUE, deleted_at = @DeletedAt, status = 'Archived'
                           WHERE Id = @Id AND is_deleted = FALSE";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@DeletedAt", DateTime.Now);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error soft deleting event: {ex.Message}");
                return false;
            }
        }

        private bool RestoreEvent(int id)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"UPDATE events 
                           SET is_deleted = FALSE, deleted_at = NULL, status = 'Scheduled'
                           WHERE Id = @Id AND is_deleted = TRUE";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring event: {ex.Message}");
                return false;
            }
        }

        private bool UpdateEventAttendance(int id, int attendanceCount)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"UPDATE events 
                           SET attendance_count = @AttendanceCount, updated_at = @UpdatedAt
                           WHERE Id = @Id AND is_deleted = FALSE";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@AttendanceCount", attendanceCount);
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Id", id);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating attendance: {ex.Message}");
                return false;
            }
        }

        private bool UpdateEventStatusInDatabase(int id, string status)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"UPDATE events 
                           SET status = @Status, updated_at = @UpdatedAt
                           WHERE Id = @Id AND is_deleted = FALSE";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Status", status);
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Id", id);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating event status: {ex.Message}");
                return false;
            }
        }
    }
}