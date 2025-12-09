using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SeniorManagement.Models;
using SeniorManagement.Helpers;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SeniorManagement.Controllers
{
    [Authorize]
    public class SeniorController : BaseController
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ActivityHelper _activityHelper;

        public SeniorController(DatabaseHelper dbHelper, ActivityHelper activityHelper)
        {
            _dbHelper = dbHelper;
            _activityHelper = activityHelper;
        }

        // Senior Records Page - View All Active Seniors
        public IActionResult Index()
        {
            var seniors = GetAllActiveSeniors();
            return View(seniors);
        }

        // CREATE SENIOR - GET (Display the create form)
        [HttpGet]
        public IActionResult CreateSenior()
        {
            return View(new Senior());
        }

        // CREATE SENIOR - POST (Handle form submission - FIXED VERSION)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSenior(Senior senior)
        {
            // Clear any existing model errors
            ModelState.Clear();

            try
            {
                // Validate 12-digit SCCN number
                if (string.IsNullOrEmpty(senior.SeniorId) || !IsValidSCCNNumber(senior.SeniorId))
                {
                    ModelState.AddModelError("SeniorId", "Senior ID must be exactly 12 digits (SCCN number)");
                }
                else
                {
                    // Check if SCCN already exists
                    if (CheckSeniorIdExists(senior.SeniorId))
                    {
                        ModelState.AddModelError("SeniorId", "This SCCN number is already registered. Please use a different SCCN number.");
                    }
                }

                // Validate First Name
                if (string.IsNullOrEmpty(senior.FirstName))
                {
                    ModelState.AddModelError("FirstName", "First Name is required.");
                }
                else if (senior.FirstName.Length > 100)
                {
                    ModelState.AddModelError("FirstName", "First Name cannot exceed 100 characters.");
                }

                // Validate Last Name
                if (string.IsNullOrEmpty(senior.LastName))
                {
                    ModelState.AddModelError("LastName", "Last Name is required.");
                }
                else if (senior.LastName.Length > 100)
                {
                    ModelState.AddModelError("LastName", "Last Name cannot exceed 100 characters.");
                }

                // Validate Gender
                if (string.IsNullOrEmpty(senior.Gender))
                {
                    ModelState.AddModelError("Gender", "Gender is required.");
                }

                // Validate Age
                if (senior.Age < 60 || senior.Age > 120)
                {
                    ModelState.AddModelError("Age", "Age must be between 60 and 120 years.");
                }

                // Validate Zone
                if (senior.Zone < 1 || senior.Zone > 7)
                {
                    ModelState.AddModelError("Zone", "Zone must be between 1 and 7.");
                }

                // Validate Middle Initial (if provided)
                if (!string.IsNullOrEmpty(senior.MiddleInitial) && senior.MiddleInitial.Length > 1)
                {
                    ModelState.AddModelError("MiddleInitial", "Middle Initial must be 1 character.");
                }

                // Validate Contact Number format (if provided)
                if (!string.IsNullOrEmpty(senior.ContactNumber) && senior.ContactNumber.Length > 20)
                {
                    ModelState.AddModelError("ContactNumber", "Contact Number cannot exceed 20 characters.");
                }

                // If there are validation errors, return to view
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Please correct the validation errors below.";
                    return View(senior);
                }

                // Calculate age from BirthDate if provided
                if (senior.BirthDate.HasValue)
                {
                    // Recalculate age to ensure accuracy
                    senior.Age = CalculateAge(senior.BirthDate.Value);

                    // Double-check age validation
                    if (senior.Age < 60 || senior.Age > 120)
                    {
                        ModelState.AddModelError("Age", "Age calculated from birth date must be between 60 and 120 years.");
                        TempData["ErrorMessage"] = "Invalid birth date. Age must be between 60 and 120.";
                        return View(senior);
                    }
                }

                // Set default values
                senior.Status = "Active";
                senior.Barangay = "Sampaguita"; // Fixed barangay
                senior.CreatedAt = DateTime.Now;
                senior.UpdatedAt = DateTime.Now;

                // If BirthDate is not provided but Age is, estimate BirthDate
                if (!senior.BirthDate.HasValue && senior.Age > 0)
                {
                    senior.BirthDate = DateTime.Now.AddYears(-senior.Age);
                }

                // Save to database
                int newSeniorId = InsertSeniorIntoDatabase(senior);

                if (newSeniorId > 0)
                {
                    // Log the activity
                    await _activityHelper.LogActivityAsync(
                        "Create Senior",
                        $"Created new senior record: {senior.FirstName} {senior.LastName} (SCCN: {senior.SeniorId})"
                    );

                    TempData["SuccessMessage"] = $"Senior record created successfully! SCCN: {senior.SeniorId}";
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["ErrorMessage"] = "Error creating senior record in database. Please try again.";
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Create Senior");
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }

            return View(senior);
        }

        // Edit Senior - GET
        [HttpGet]
        public IActionResult EditSenior(int id)
        {
            var senior = GetSeniorById(id);
            if (senior == null)
            {
                TempData["ErrorMessage"] = "Senior record not found.";
                return RedirectToAction("Index");
            }

            return View(senior);
        }

        // Edit Senior - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSenior(Senior senior)
        {
            ModelState.Clear();

            try
            {
                // Validate 12-digit SCCN number
                if (string.IsNullOrEmpty(senior.SeniorId) || !IsValidSCCNNumber(senior.SeniorId))
                {
                    ModelState.AddModelError("SeniorId", "Senior ID must be exactly 12 digits (SCCN number)");
                }
                else
                {
                    // Check if SeniorId already exists (excluding current record)
                    if (CheckSeniorIdExists(senior.SeniorId, senior.Id))
                    {
                        ModelState.AddModelError("SeniorId", "This SCCN number is already registered. Please use a different SCCN number.");
                    }
                }

                // Recalculate age from BirthDate if provided
                if (senior.BirthDate.HasValue)
                {
                    senior.Age = CalculateAge(senior.BirthDate.Value);
                }

                senior.UpdatedAt = DateTime.Now;
                senior.Barangay = "Sampaguita"; // Fixed barangay

                // Validate First Name
                if (string.IsNullOrEmpty(senior.FirstName))
                {
                    ModelState.AddModelError("FirstName", "First Name is required.");
                }

                // Validate Last Name
                if (string.IsNullOrEmpty(senior.LastName))
                {
                    ModelState.AddModelError("LastName", "Last Name is required.");
                }

                // Validate Gender
                if (string.IsNullOrEmpty(senior.Gender))
                {
                    ModelState.AddModelError("Gender", "Gender is required.");
                }

                // Validate Zone is between 1-7
                if (senior.Zone < 1 || senior.Zone > 7)
                {
                    ModelState.AddModelError("Zone", "Zone must be between 1 and 7.");
                }

                // Validate Age is between 60-120
                if (senior.Age < 60 || senior.Age > 120)
                {
                    ModelState.AddModelError("Age", "Age must be between 60 and 120 years.");
                }

                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Please correct the validation errors and try again.";
                    return View(senior);
                }

                if (UpdateSeniorInDatabase(senior))
                {
                    // Log the activity
                    await _activityHelper.LogActivityAsync(
                        "Edit Senior",
                        $"Updated senior record: {senior.FirstName} {senior.LastName} (SCCN: {senior.SeniorId})"
                    );

                    TempData["SuccessMessage"] = "Senior record updated successfully!";
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["ErrorMessage"] = "Error updating senior record. No changes were made.";
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Edit Senior");
                TempData["ErrorMessage"] = $"An error occurred while updating the senior record: {ex.Message}";
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }

            return View(senior);
        }

        // Archive Senior - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveSenior(int id)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Get senior details before archiving
                    var senior = GetSeniorById(id);
                    if (senior == null)
                    {
                        TempData["ErrorMessage"] = "Senior record not found.";
                        return RedirectToAction("Index");
                    }

                    // Check if already archived
                    if (senior.Status == "Archived")
                    {
                        TempData["ErrorMessage"] = "Senior is already archived.";
                        return RedirectToAction("Index");
                    }

                    string query = @"UPDATE seniors 
                                   SET Status = 'Archived',
                                       UpdatedAt = @UpdatedAt
                                   WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Log the activity
                            await _activityHelper.LogActivityAsync(
                                "Archive Senior",
                                $"Archived senior record: {senior.FirstName} {senior.LastName} (SCCN: {senior.SeniorId})"
                            );

                            TempData["SuccessMessage"] = "Senior record archived successfully!";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Senior record not found.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Archive Senior");
                Console.WriteLine($"Error archiving senior: {ex.Message}");
                TempData["ErrorMessage"] = "Error archiving senior record. Please try again.";
            }

            return RedirectToAction("Index");
        }

        // Restore Archived Senior - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreSenior(int id)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // Get senior details before restoring
                    var senior = GetSeniorById(id);
                    if (senior == null)
                    {
                        TempData["ErrorMessage"] = "Senior record not found.";
                        return RedirectToAction("ArchivedSeniors");
                    }

                    // Check if actually archived
                    if (senior.Status != "Archived")
                    {
                        TempData["ErrorMessage"] = "Senior is not archived.";
                        return RedirectToAction("ArchivedSeniors");
                    }

                    string query = @"UPDATE seniors 
                                   SET Status = 'Active',
                                       UpdatedAt = @UpdatedAt
                                   WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Log the activity
                            await _activityHelper.LogActivityAsync(
                                "Restore Senior",
                                $"Restored senior record: {senior.FirstName} {senior.LastName} (SCCN: {senior.SeniorId})"
                            );

                            TempData["SuccessMessage"] = "Senior record restored successfully!";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Senior record not found.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Restore Senior");
                Console.WriteLine($"Error restoring senior: {ex.Message}");
                TempData["ErrorMessage"] = "Error restoring senior record. Please try again.";
            }

            return RedirectToAction("ArchivedSeniors");
        }

        // View Archived Seniors
        public IActionResult ArchivedSeniors()
        {
            var seniors = GetArchivedSeniors();
            return View(seniors);
        }

        // View Senior Details
        [HttpGet]
        public IActionResult ViewSenior(int id)
        {
            var senior = GetSeniorById(id);
            if (senior == null)
            {
                TempData["ErrorMessage"] = "Senior record not found.";
                return RedirectToAction("Index");
            }

            return View(senior);
        }

        // ============ HELPER METHODS ============

        // Validate 12-digit SCCN number
        private bool IsValidSCCNNumber(string seniorId)
        {
            if (string.IsNullOrWhiteSpace(seniorId))
                return false;

            // Must be exactly 12 digits
            if (seniorId.Length != 12)
                return false;

            // Must contain only digits
            return Regex.IsMatch(seniorId, @"^\d{12}$");
        }

        // Insert new senior into database (FIXED VERSION)
        private int InsertSeniorIntoDatabase(Senior senior)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"INSERT INTO seniors 
                    (SeniorId, FirstName, LastName, MiddleInitial, Gender, Age, 
                     BirthDate, ContactNumber, Zone, Barangay, CivilStatus,
                     Status, CreatedAt, UpdatedAt)
                    VALUES 
                    (@SeniorId, @FirstName, @LastName, @MiddleInitial, @Gender, @Age,
                     @BirthDate, @ContactNumber, @Zone, @Barangay, @CivilStatus,
                     @Status, @CreatedAt, @UpdatedAt);
                    SELECT LAST_INSERT_ID();";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        // Basic Information - Ensure no null values
                        cmd.Parameters.AddWithValue("@SeniorId", senior.SeniorId ?? "");
                        cmd.Parameters.AddWithValue("@FirstName", senior.FirstName ?? "");
                        cmd.Parameters.AddWithValue("@LastName", senior.LastName ?? "");
                        cmd.Parameters.AddWithValue("@MiddleInitial",
                            string.IsNullOrEmpty(senior.MiddleInitial) ? DBNull.Value : senior.MiddleInitial.Trim());
                        cmd.Parameters.AddWithValue("@Gender", senior.Gender ?? "");
                        cmd.Parameters.AddWithValue("@Age", senior.Age);

                        // Handle nullable BirthDate
                        if (senior.BirthDate.HasValue)
                        {
                            cmd.Parameters.AddWithValue("@BirthDate", senior.BirthDate.Value);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@BirthDate", DBNull.Value);
                        }

                        cmd.Parameters.AddWithValue("@ContactNumber",
                            string.IsNullOrEmpty(senior.ContactNumber) ? DBNull.Value : senior.ContactNumber.Trim());
                        cmd.Parameters.AddWithValue("@Zone", senior.Zone);
                        cmd.Parameters.AddWithValue("@Barangay", senior.Barangay ?? "Sampaguita");
                        cmd.Parameters.AddWithValue("@CivilStatus",
                            string.IsNullOrEmpty(senior.CivilStatus) ? DBNull.Value : senior.CivilStatus);

                        // System Fields
                        cmd.Parameters.AddWithValue("@Status", senior.Status ?? "Active");
                        cmd.Parameters.AddWithValue("@CreatedAt", senior.CreatedAt);
                        cmd.Parameters.AddWithValue("@UpdatedAt", senior.UpdatedAt);

                        var result = cmd.ExecuteScalar();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (MySqlException mysqlEx)
            {
                Console.WriteLine($"MySQL Error inserting senior: {mysqlEx.Message}");
                Console.WriteLine($"Error Number: {mysqlEx.Number}");
                Console.WriteLine($"SQL State: {mysqlEx.SqlState}");
                TempData["ErrorMessage"] = $"Database error: {mysqlEx.Message}";
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting senior: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Detailed error: {ex.ToString()}");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return 0;
            }
        }

        // Get all active seniors
        private List<Senior> GetAllActiveSeniors()
        {
            var seniors = new List<Senior>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT * FROM seniors 
                                   WHERE Status = 'Active' 
                                   ORDER BY LastName, FirstName";

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
                Console.WriteLine($"Error getting active seniors: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading senior records.";
            }

            return seniors;
        }

        // Get archived seniors
        private List<Senior> GetArchivedSeniors()
        {
            var seniors = new List<Senior>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT * FROM seniors 
                                   WHERE Status = 'Archived' 
                                   ORDER BY LastName, FirstName";

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
                Console.WriteLine($"Error getting archived seniors: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading archived records.";
            }

            return seniors;
        }

        // Get senior by ID
        private Senior GetSeniorById(int id)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT * FROM seniors WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return MapSeniorFromReader(reader);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting senior: {ex.Message}");
            }

            return null;
        }

        // Helper method to map reader to Senior object
        private Senior MapSeniorFromReader(MySqlDataReader reader)
        {
            return new Senior
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
            };
        }

        // Update senior in database
        private bool UpdateSeniorInDatabase(Senior senior)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"UPDATE seniors 
                                   SET SeniorId = @SeniorId,
                                       FirstName = @FirstName,
                                       LastName = @LastName,
                                       MiddleInitial = @MiddleInitial,
                                       Gender = @Gender,
                                       Age = @Age,
                                       BirthDate = @BirthDate,
                                       ContactNumber = @ContactNumber,
                                       Zone = @Zone,
                                       CivilStatus = @CivilStatus,
                                       UpdatedAt = @UpdatedAt
                                   WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        // Basic Information
                        cmd.Parameters.AddWithValue("@Id", senior.Id);
                        cmd.Parameters.AddWithValue("@SeniorId", senior.SeniorId ?? "");
                        cmd.Parameters.AddWithValue("@FirstName", senior.FirstName ?? "");
                        cmd.Parameters.AddWithValue("@LastName", senior.LastName ?? "");
                        cmd.Parameters.AddWithValue("@MiddleInitial", string.IsNullOrEmpty(senior.MiddleInitial) ? DBNull.Value : senior.MiddleInitial);
                        cmd.Parameters.AddWithValue("@Gender", senior.Gender ?? "");
                        cmd.Parameters.AddWithValue("@Age", senior.Age);
                        cmd.Parameters.AddWithValue("@BirthDate", senior.BirthDate.HasValue ? (object)senior.BirthDate.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@ContactNumber", string.IsNullOrEmpty(senior.ContactNumber) ? DBNull.Value : senior.ContactNumber);
                        cmd.Parameters.AddWithValue("@Zone", senior.Zone);
                        cmd.Parameters.AddWithValue("@CivilStatus", string.IsNullOrEmpty(senior.CivilStatus) ? DBNull.Value : senior.CivilStatus);

                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating senior: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Detailed error: {ex.ToString()}");
                return false;
            }
        }

        // Calculate age from date of birth
        private int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age)) age--;
            return age;
        }

        // Check if SeniorId already exists (FIXED VERSION)
        private bool CheckSeniorIdExists(string seniorId, int excludeId = 0)
        {
            if (string.IsNullOrEmpty(seniorId))
                return false;

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM seniors WHERE SeniorId = @SeniorId";

                    if (excludeId > 0)
                    {
                        query += " AND Id != @ExcludeId";
                    }

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@SeniorId", seniorId.Trim());
                        if (excludeId > 0)
                        {
                            cmd.Parameters.AddWithValue("@ExcludeId", excludeId);
                        }

                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            int count = Convert.ToInt32(result);
                            return count > 0;
                        }
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking SeniorId: {ex.Message}");
                return false;
            }
        }

        // AJAX endpoint to check SCCN number availability
        [HttpGet]
        public JsonResult CheckSeniorIdAvailable(string seniorId, int excludeId = 0)
        {
            try
            {
                // First validate format
                if (!IsValidSCCNNumber(seniorId))
                {
                    return Json(new
                    {
                        available = false,
                        validFormat = false,
                        message = "SCCN number must be exactly 12 digits (numbers only)"
                    });
                }

                bool exists = CheckSeniorIdExists(seniorId, excludeId);
                return Json(new
                {
                    available = !exists,
                    validFormat = true,
                    message = exists ? "SCCN number already registered" : "SCCN number available"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    available = false,
                    validFormat = false,
                    error = ex.Message
                });
            }
        }

        // Search seniors by SCCN or name
        [HttpGet]
        public JsonResult SearchSeniors(string searchTerm, string status = "Active")
        {
            var seniors = new List<Senior>();

            try
            {
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    using (var connection = _dbHelper.GetConnection())
                    {
                        connection.Open();
                        string query = @"SELECT * FROM seniors 
                                       WHERE Status = @Status
                                       AND (SeniorId LIKE @SearchTerm 
                                           OR FirstName LIKE @SearchTerm 
                                           OR LastName LIKE @SearchTerm
                                           OR CONCAT(FirstName, ' ', LastName) LIKE @SearchTerm)
                                       ORDER BY LastName, FirstName
                                       LIMIT 20";

                        using (var cmd = new MySqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@Status", status);
                            cmd.Parameters.AddWithValue("@SearchTerm", $"%{searchTerm}%");

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    seniors.Add(MapSeniorFromReader(reader));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching seniors: {ex.Message}");
            }

            return Json(new { success = true, data = seniors });
        }

        // Get SCCN format example
        [HttpGet]
        public JsonResult GetSCCNFormatExample()
        {
            return Json(new
            {
                example = "202312340001",
                format = "12 digits (numbers only)",
                pattern = "XXXXXXXXXXXX"
            });
        }
    }
}