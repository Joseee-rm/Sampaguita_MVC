// Controllers/ZoneController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SeniorManagement.Models;
using SeniorManagement.Helpers;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Diagnostics;

namespace SeniorManagement.Controllers
{
    [Authorize]
    public class ZoneController : BaseController
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ActivityHelper _activityHelper;

        public ZoneController(DatabaseHelper dbHelper, ActivityHelper activityHelper)
        {
            _dbHelper = dbHelper;
            _activityHelper = activityHelper;
        }

        // GET: Zone/Index
        [HttpGet]
        public IActionResult Index()
        {
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            var zones = GetAllZones();
            return View(zones);
        }

        // GET: Zone/Create
        [HttpGet]
        public IActionResult Create()
        {
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            return View(new Zone());
        }

        // POST: Zone/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Zone model)
        {
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrWhiteSpace(model.ZoneName))
            {
                TempData["ErrorMessage"] = "Zone name is required.";
                return View(model);
            }

            if (model.ZoneNumber <= 0)
            {
                TempData["ErrorMessage"] = "Zone number must be greater than 0.";
                return View(model);
            }

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Check if zone number already exists
                    string checkQuery = "SELECT COUNT(*) FROM zones WHERE ZoneNumber = @ZoneNumber";
                    using (var checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@ZoneNumber", model.ZoneNumber);
                        int existingCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (existingCount > 0)
                        {
                            TempData["ErrorMessage"] = $"Zone number {model.ZoneNumber} already exists.";
                            return View(model);
                        }
                    }

                    // Insert new zone
                    string insertQuery = @"
                        INSERT INTO zones (ZoneNumber, ZoneName, Description, CreatedAt, UpdatedAt, IsActive) 
                        VALUES (@ZoneNumber, @ZoneName, @Description, @CreatedAt, @UpdatedAt, @IsActive)";

                    using (var cmd = new MySqlCommand(insertQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@ZoneNumber", model.ZoneNumber);
                        cmd.Parameters.AddWithValue("@ZoneName", model.ZoneName);
                        cmd.Parameters.AddWithValue("@Description", model.Description ?? string.Empty);
                        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@IsActive", true);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            await _activityHelper.LogActivityAsync(
                                "Create Zone",
                                $"Created new zone: {model.ZoneName} (Zone {model.ZoneNumber})"
                            );

                            TempData["SuccessMessage"] = $"Zone {model.ZoneName} created successfully!";
                            return RedirectToAction("Index");
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Error creating zone.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogActivityAsync("Error", $"Create Zone: {ex.Message}");
                Debug.WriteLine($"Error creating zone: {ex.Message}");
                TempData["ErrorMessage"] = $"Error creating zone: {ex.Message}";
            }

            return View(model);
        }

        // GET: Zone/Edit/{id}
        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            var zone = GetZoneById(id);
            if (zone == null)
            {
                TempData["ErrorMessage"] = "Zone not found.";
                return RedirectToAction("Index");
            }

            return View(zone);
        }

        // POST: Zone/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Zone model)
        {
            if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
            {
                TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrWhiteSpace(model.ZoneName))
            {
                TempData["ErrorMessage"] = "Zone name is required.";
                return View(model);
            }

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Check if zone number already exists (excluding current zone)
                    string checkQuery = "SELECT COUNT(*) FROM zones WHERE ZoneNumber = @ZoneNumber AND Id != @Id";
                    using (var checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@ZoneNumber", model.ZoneNumber);
                        checkCmd.Parameters.AddWithValue("@Id", model.Id);
                        int existingCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (existingCount > 0)
                        {
                            TempData["ErrorMessage"] = $"Zone number {model.ZoneNumber} already exists.";
                            return View(model);
                        }
                    }

                    // Update zone
                    string updateQuery = @"
                        UPDATE zones 
                        SET ZoneNumber = @ZoneNumber, 
                            ZoneName = @ZoneName, 
                            Description = @Description,
                            UpdatedAt = @UpdatedAt,
                            IsActive = @IsActive
                        WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(updateQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@ZoneNumber", model.ZoneNumber);
                        cmd.Parameters.AddWithValue("@ZoneName", model.ZoneName);
                        cmd.Parameters.AddWithValue("@Description", model.Description ?? string.Empty);
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                        cmd.Parameters.AddWithValue("@Id", model.Id);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            await _activityHelper.LogActivityAsync(
                                "Edit Zone",
                                $"Updated zone: {model.ZoneName} (Zone {model.ZoneNumber})"
                            );

                            TempData["SuccessMessage"] = "Zone updated successfully!";
                            return RedirectToAction("Index");
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Zone not found.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogActivityAsync("Error", $"Edit Zone: {ex.Message}");
                Debug.WriteLine($"Error updating zone: {ex.Message}");
                TempData["ErrorMessage"] = $"Error updating zone: {ex.Message}";
            }

            return View(model);
        }

        // POST: Zone/ToggleStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
                {
                    TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                    return RedirectToAction("Index", "Home");
                }

                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Get current status
                    string getQuery = "SELECT IsActive FROM zones WHERE Id = @Id";
                    bool currentStatus = false;

                    using (var getCmd = new MySqlCommand(getQuery, connection))
                    {
                        getCmd.Parameters.AddWithValue("@Id", id);
                        var result = getCmd.ExecuteScalar();
                        if (result != null)
                        {
                            currentStatus = Convert.ToBoolean(result);
                        }
                    }

                    // Toggle status
                    string updateQuery = "UPDATE zones SET IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id";
                    using (var updateCmd = new MySqlCommand(updateQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@IsActive", !currentStatus);
                        updateCmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);
                        updateCmd.Parameters.AddWithValue("@Id", id);
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            var zone = GetZoneById(id);
                            if (zone != null)
                            {
                                await _activityHelper.LogActivityAsync(
                                    "Toggle Zone Status",
                                    $"Zone {zone.ZoneName} {(currentStatus ? "deactivated" : "activated")}"
                                );
                            }

                            TempData["SuccessMessage"] = $"Zone {(currentStatus ? "deactivated" : "activated")} successfully!";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogActivityAsync("Error", ex.Message);
                Debug.WriteLine($"Error toggling zone status: {ex.Message}");
                TempData["ErrorMessage"] = "Error updating zone status.";
            }

            return RedirectToAction("Index");
        }

        // POST: Zone/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if (!(HttpContext.Session.GetString("IsAdmin") == "True"))
                {
                    TempData["ErrorMessage"] = "Access denied. Admin privileges required.";
                    return RedirectToAction("Index", "Home");
                }

                // Check if zone is being used by any seniors
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string checkQuery = "SELECT COUNT(*) FROM seniors WHERE Zone = (SELECT ZoneNumber FROM zones WHERE Id = @Id)";
                    using (var checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@Id", id);
                        int seniorCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (seniorCount > 0)
                        {
                            TempData["ErrorMessage"] = "Cannot delete zone. It is currently assigned to one or more seniors.";
                            return RedirectToAction("Index");
                        }
                    }

                    // Delete zone
                    string deleteQuery = "DELETE FROM zones WHERE Id = @Id";
                    using (var deleteCmd = new MySqlCommand(deleteQuery, connection))
                    {
                        deleteCmd.Parameters.AddWithValue("@Id", id);
                        int rowsAffected = deleteCmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            await _activityHelper.LogActivityAsync(
                                "Delete Zone",
                                $"Deleted zone with ID: {id}"
                            );

                            TempData["SuccessMessage"] = "Zone deleted successfully!";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Zone not found.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogActivityAsync("Error", $"Delete Zone: {ex.Message}");
                Debug.WriteLine($"Error deleting zone: {ex.Message}");
                TempData["ErrorMessage"] = "Error deleting zone.";
            }

            return RedirectToAction("Index");
        }

        // Helper methods
        private List<Zone> GetAllZones()
        {
            var zones = new List<Zone>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT Id, ZoneNumber, ZoneName, Description, CreatedAt, UpdatedAt, IsActive FROM zones ORDER BY ZoneNumber";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            zones.Add(new Zone
                            {
                                Id = reader.GetInt32("Id"),
                                ZoneNumber = reader.GetInt32("ZoneNumber"),
                                ZoneName = reader.GetString("ZoneName"),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ?
                                            string.Empty : reader.GetString("Description"),
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                UpdatedAt = reader.GetDateTime("UpdatedAt"),
                                IsActive = reader.GetBoolean("IsActive")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting zones: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading zones from database.";
            }

            return zones;
        }

        private Zone GetZoneById(int id)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT Id, ZoneNumber, ZoneName, Description, CreatedAt, UpdatedAt, IsActive FROM zones WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Zone
                                {
                                    Id = reader.GetInt32("Id"),
                                    ZoneNumber = reader.GetInt32("ZoneNumber"),
                                    ZoneName = reader.GetString("ZoneName"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ?
                                                string.Empty : reader.GetString("Description"),
                                    CreatedAt = reader.GetDateTime("CreatedAt"),
                                    UpdatedAt = reader.GetDateTime("UpdatedAt"),
                                    IsActive = reader.GetBoolean("IsActive")
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting zone: {ex.Message}");
            }

            return null;
        }
    }
}