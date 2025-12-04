using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SeniorManagement.Models;
using SeniorManagement.Helpers;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Linq;
using System;

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

        // Senior Records Page - View All Seniors (excluding deleted)
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

        // CREATE SENIOR - POST (Handle form submission)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSenior(Senior senior)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the validation errors and try again.";
                return View(senior);
            }

            try
            {
                // Calculate age from DOB
                senior.s_age = CalculateAge(senior.s_dob);
                senior.CreatedAt = DateTime.Now;
                senior.UpdatedAt = DateTime.Now;
                senior.Status = "Active";
                senior.IsDeleted = false;

                // Ensure optional fields are properly set
                senior.s_health_problems_option ??= "No";
                senior.s_maintenance_option ??= "No";
                senior.s_disability_option ??= "No";
                senior.s_visual_option ??= "No";
                senior.s_hearing_option ??= "No";
                senior.s_emotional_option ??= "No";

                // Process children list to text format
                if (senior.ChildrenList != null && senior.ChildrenList.Any(c => !string.IsNullOrWhiteSpace(c.Name)))
                {
                    senior.s_children = FormatChildrenText(senior.ChildrenList);
                }

                int newSeniorId = InsertSeniorIntoDatabase(senior);

                if (newSeniorId > 0)
                {
                    // Handle maintenance medicines if any
                    if (senior.MaintenanceMedicines != null && senior.MaintenanceMedicines.Any(m => !string.IsNullOrWhiteSpace(m.MedicineName)))
                    {
                        InsertMaintenanceMedicines(newSeniorId, senior.MaintenanceMedicines);
                    }

                    // Log the activity
                    await _activityHelper.LogActivityAsync(
                        "Create Senior",
                        $"Created new senior record: {senior.s_firstName} {senior.s_lastName} (ID: {newSeniorId})"
                    );

                    TempData["SuccessMessage"] = "Senior record created successfully!";
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["ErrorMessage"] = "Error creating senior record. Please try again.";
                }
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Create Senior");
                TempData["ErrorMessage"] = $"An error occurred while creating the senior record: {ex.Message}";
                Console.WriteLine($"Error: {ex.Message}");
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

            // Load maintenance medicines for this senior
            senior.MaintenanceMedicines = GetMaintenanceMedicines(id);

            // Parse children from text field
            if (!string.IsNullOrEmpty(senior.s_children))
            {
                senior.ChildrenList = ParseChildrenFromText(senior.s_children);
            }

            return View(senior);
        }

        // Edit Senior - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSenior(Senior senior)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the validation errors and try again.";
                return View(senior);
            }

            try
            {
                // Recalculate age from DOB
                senior.s_age = CalculateAge(senior.s_dob);
                senior.UpdatedAt = DateTime.Now;

                // Ensure optional fields are properly set
                senior.s_health_problems_option ??= "No";
                senior.s_maintenance_option ??= "No";
                senior.s_disability_option ??= "No";
                senior.s_visual_option ??= "No";
                senior.s_hearing_option ??= "No";
                senior.s_emotional_option ??= "No";

                // Process children list to text format
                if (senior.ChildrenList != null && senior.ChildrenList.Any(c => !string.IsNullOrWhiteSpace(c.Name)))
                {
                    senior.s_children = FormatChildrenText(senior.ChildrenList);
                }

                if (UpdateSeniorInDatabase(senior))
                {
                    // Handle maintenance medicines
                    UpdateMaintenanceMedicines(senior.Id, senior.MaintenanceMedicines);

                    // Log the activity
                    await _activityHelper.LogActivityAsync(
                        "Edit Senior",
                        $"Updated senior record: {senior.s_firstName} {senior.s_lastName} (ID: {senior.Id})"
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
                TempData["ErrorMessage"] = $"An error occurred while updating the senior record. Please try again.";
                Console.WriteLine($"Error: {ex.Message}");
            }

            return View(senior);
        }

        // Soft Delete Senior (Archive)
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

                    string query = @"UPDATE seniors 
                                   SET IsDeleted = 1, 
                                       DeletedAt = @DeletedAt,
                                       Status = 'Archived'
                                   WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@DeletedAt", DateTime.Now);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Log the activity
                            await _activityHelper.LogActivityAsync(
                                "Archive Senior",
                                $"Archived senior record: {senior?.s_firstName} {senior?.s_lastName} (ID: {id})"
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

        // Restore Archived Senior
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

                    string query = @"UPDATE seniors 
                                   SET IsDeleted = 0, 
                                       DeletedAt = NULL,
                                       Status = 'Active'
                                   WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Log the activity
                            await _activityHelper.LogActivityAsync(
                                "Restore Senior",
                                $"Restored senior record: {senior?.s_firstName} {senior?.s_lastName} (ID: {id})"
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

            return RedirectToAction("DeletedSeniors");
        }

        // View Deleted Seniors
        public IActionResult DeletedSeniors()
        {
            var seniors = GetDeletedSeniors();
            return View(seniors);
        }

        // Insert new senior into database
        private int InsertSeniorIntoDatabase(Senior senior)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    string query = @"INSERT INTO seniors 
                    (s_firstName, s_middleName, s_lastName, s_sex, s_dob, s_age, s_contact, 
                     s_barangay, s_guardian_zone, s_religion, s_bloodtype,
                     s_health_problems_option, s_health_problems,
                     s_maintenance_option, s_maintenance,
                     s_disability_option, s_disability,
                     s_visual_option, s_visual,
                     s_hearing_option, s_hearing,
                     s_emotional_option, s_emotional,
                     s_spouse, s_spouse_age, s_spouse_occupation, s_spouse_contact, s_children,
                     s_guardian_name, s_guardian_relationship, s_guardian_relationship_other, 
                     s_guardian_contact, s_guardian_address,
                     Status, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES 
                    (@FirstName, @MiddleName, @LastName, @Sex, @Dob, @Age, @Contact,
                     @Barangay, @GuardianZone, @Religion, @BloodType,
                     @HealthProblemsOption, @HealthProblems,
                     @MaintenanceOption, @Maintenance,
                     @DisabilityOption, @Disability,
                     @VisualOption, @Visual,
                     @HearingOption, @Hearing,
                     @EmotionalOption, @Emotional,
                     @Spouse, @SpouseAge, @SpouseOccupation, @SpouseContact, @Children,
                     @GuardianName, @GuardianRelationship, @GuardianRelationshipOther, 
                     @GuardianContact, @GuardianAddress,
                     @Status, @CreatedAt, @UpdatedAt, @IsDeleted);
                    SELECT LAST_INSERT_ID();";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        // Personal Information
                        cmd.Parameters.AddWithValue("@FirstName", senior.s_firstName ?? "");
                        cmd.Parameters.AddWithValue("@MiddleName", string.IsNullOrEmpty(senior.s_middleName) ? DBNull.Value : senior.s_middleName);
                        cmd.Parameters.AddWithValue("@LastName", senior.s_lastName ?? "");
                        cmd.Parameters.AddWithValue("@Sex", senior.s_sex ?? "");
                        cmd.Parameters.AddWithValue("@Dob", senior.s_dob);
                        cmd.Parameters.AddWithValue("@Age", senior.s_age);
                        cmd.Parameters.AddWithValue("@Contact", string.IsNullOrEmpty(senior.s_contact) ? DBNull.Value : senior.s_contact);
                        cmd.Parameters.AddWithValue("@Barangay", string.IsNullOrEmpty(senior.s_barangay) ? DBNull.Value : senior.s_barangay);
                        cmd.Parameters.AddWithValue("@GuardianZone", string.IsNullOrEmpty(senior.s_guardian_zone) ? DBNull.Value : senior.s_guardian_zone);
                        cmd.Parameters.AddWithValue("@Religion", string.IsNullOrEmpty(senior.s_religion) ? DBNull.Value : senior.s_religion);
                        cmd.Parameters.AddWithValue("@BloodType", string.IsNullOrEmpty(senior.s_bloodtype) ? DBNull.Value : senior.s_bloodtype);

                        // Health Information
                        cmd.Parameters.AddWithValue("@HealthProblemsOption", senior.s_health_problems_option ?? "No");
                        cmd.Parameters.AddWithValue("@HealthProblems", string.IsNullOrEmpty(senior.s_health_problems) ? DBNull.Value : senior.s_health_problems);
                        cmd.Parameters.AddWithValue("@MaintenanceOption", senior.s_maintenance_option ?? "No");
                        cmd.Parameters.AddWithValue("@Maintenance", string.IsNullOrEmpty(senior.s_maintenance) ? DBNull.Value : senior.s_maintenance);
                        cmd.Parameters.AddWithValue("@DisabilityOption", senior.s_disability_option ?? "No");
                        cmd.Parameters.AddWithValue("@Disability", string.IsNullOrEmpty(senior.s_disability) ? DBNull.Value : senior.s_disability);
                        cmd.Parameters.AddWithValue("@VisualOption", senior.s_visual_option ?? "No");
                        cmd.Parameters.AddWithValue("@Visual", string.IsNullOrEmpty(senior.s_visual) ? DBNull.Value : senior.s_visual);
                        cmd.Parameters.AddWithValue("@HearingOption", senior.s_hearing_option ?? "No");
                        cmd.Parameters.AddWithValue("@Hearing", string.IsNullOrEmpty(senior.s_hearing) ? DBNull.Value : senior.s_hearing);
                        cmd.Parameters.AddWithValue("@EmotionalOption", senior.s_emotional_option ?? "No");
                        cmd.Parameters.AddWithValue("@Emotional", string.IsNullOrEmpty(senior.s_emotional) ? DBNull.Value : senior.s_emotional);

                        // Family Information
                        cmd.Parameters.AddWithValue("@Spouse", string.IsNullOrEmpty(senior.s_spouse) ? DBNull.Value : senior.s_spouse);
                        cmd.Parameters.AddWithValue("@SpouseAge", senior.s_spouse_age.HasValue ? (object)senior.s_spouse_age.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@SpouseOccupation", string.IsNullOrEmpty(senior.s_spouse_occupation) ? DBNull.Value : senior.s_spouse_occupation);
                        cmd.Parameters.AddWithValue("@SpouseContact", string.IsNullOrEmpty(senior.s_spouse_contact) ? DBNull.Value : senior.s_spouse_contact);
                        cmd.Parameters.AddWithValue("@Children", string.IsNullOrEmpty(senior.s_children) ? DBNull.Value : senior.s_children);

                        // Guardian/Emergency Contact
                        cmd.Parameters.AddWithValue("@GuardianName", string.IsNullOrEmpty(senior.s_guardian_name) ? DBNull.Value : senior.s_guardian_name);
                        cmd.Parameters.AddWithValue("@GuardianRelationship", string.IsNullOrEmpty(senior.s_guardian_relationship) ? DBNull.Value : senior.s_guardian_relationship);
                        cmd.Parameters.AddWithValue("@GuardianRelationshipOther", string.IsNullOrEmpty(senior.s_guardian_relationship_other) ? DBNull.Value : senior.s_guardian_relationship_other);
                        cmd.Parameters.AddWithValue("@GuardianContact", string.IsNullOrEmpty(senior.s_guardian_contact) ? DBNull.Value : senior.s_guardian_contact);
                        cmd.Parameters.AddWithValue("@GuardianAddress", string.IsNullOrEmpty(senior.s_guardian_address) ? DBNull.Value : senior.s_guardian_address);

                        // System Fields
                        cmd.Parameters.AddWithValue("@Status", senior.Status ?? "Active");
                        cmd.Parameters.AddWithValue("@CreatedAt", senior.CreatedAt);
                        cmd.Parameters.AddWithValue("@UpdatedAt", senior.UpdatedAt);
                        cmd.Parameters.AddWithValue("@IsDeleted", senior.IsDeleted);

                        var result = cmd.ExecuteScalar();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting senior: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Detailed error: {ex.ToString()}");
                return 0;
            }
        }

        // Insert maintenance medicines for a new senior
        private void InsertMaintenanceMedicines(int seniorId, List<MaintenanceMedicine> medicines)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    foreach (var medicine in medicines.Where(m => !string.IsNullOrWhiteSpace(m.MedicineName)))
                    {
                        string query = @"INSERT INTO maintenance_medicines 
                                        (SeniorId, MedicineName, Dosage, Schedule, Instructions, CreatedAt, UpdatedAt) 
                                        VALUES (@SeniorId, @MedicineName, @Dosage, @Schedule, @Instructions, @CreatedAt, @UpdatedAt)";

                        using (var cmd = new MySqlCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@SeniorId", seniorId);
                            cmd.Parameters.AddWithValue("@MedicineName", medicine.MedicineName);
                            cmd.Parameters.AddWithValue("@Dosage", medicine.Dosage);
                            cmd.Parameters.AddWithValue("@Schedule", medicine.Schedule);
                            cmd.Parameters.AddWithValue("@Instructions", string.IsNullOrWhiteSpace(medicine.Instructions) ? DBNull.Value : (object)medicine.Instructions);
                            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting maintenance medicines: {ex.Message}");
            }
        }

        // Get all active seniors (excluding deleted)
        private List<Senior> GetAllActiveSeniors()
        {
            var seniors = new List<Senior>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT * FROM seniors 
                                   WHERE IsDeleted = 0 
                                   ORDER BY s_lastName, s_firstName";

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
                TempData["ErrorMessage"] = "Error loading senior records.";
            }

            return seniors;
        }

        // Get deleted seniors
        private List<Senior> GetDeletedSeniors()
        {
            var seniors = new List<Senior>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT * FROM seniors 
                                   WHERE IsDeleted = 1 
                                   ORDER BY DeletedAt DESC";

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
                Console.WriteLine($"Error getting deleted seniors: {ex.Message}");
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

        // Get maintenance medicines for a senior
        private List<MaintenanceMedicine> GetMaintenanceMedicines(int seniorId)
        {
            var medicines = new List<MaintenanceMedicine>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT * FROM maintenance_medicines 
                                   WHERE SeniorId = @SeniorId 
                                   ORDER BY MedicineName";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@SeniorId", seniorId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                medicines.Add(new MaintenanceMedicine
                                {
                                    Id = reader.GetInt32("Id"),
                                    SeniorId = reader.GetInt32("SeniorId"),
                                    MedicineName = reader.GetString("MedicineName"),
                                    Dosage = reader.IsDBNull(reader.GetOrdinal("Dosage")) ? "" : reader.GetString("Dosage"),
                                    Schedule = reader.IsDBNull(reader.GetOrdinal("Schedule")) ? "" : reader.GetString("Schedule"),
                                    Instructions = reader.IsDBNull(reader.GetOrdinal("Instructions")) ? "" : reader.GetString("Instructions"),
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting maintenance medicines: {ex.Message}");
            }

            return medicines;
        }

        // Update maintenance medicines
        private void UpdateMaintenanceMedicines(int seniorId, List<MaintenanceMedicine> medicines)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();

                    // First, delete existing medicines for this senior
                    string deleteQuery = "DELETE FROM maintenance_medicines WHERE SeniorId = @SeniorId";
                    using (var deleteCmd = new MySqlCommand(deleteQuery, connection))
                    {
                        deleteCmd.Parameters.AddWithValue("@SeniorId", seniorId);
                        deleteCmd.ExecuteNonQuery();
                    }

                    // Then insert new ones if any
                    if (medicines != null && medicines.Any(m => !string.IsNullOrWhiteSpace(m.MedicineName)))
                    {
                        foreach (var medicine in medicines.Where(m => !string.IsNullOrWhiteSpace(m.MedicineName)))
                        {
                            string insertQuery = @"INSERT INTO maintenance_medicines 
                                                (SeniorId, MedicineName, Dosage, Schedule, Instructions, CreatedAt, UpdatedAt) 
                                                VALUES (@SeniorId, @MedicineName, @Dosage, @Schedule, @Instructions, @CreatedAt, @UpdatedAt)";

                            using (var insertCmd = new MySqlCommand(insertQuery, connection))
                            {
                                insertCmd.Parameters.AddWithValue("@SeniorId", seniorId);
                                insertCmd.Parameters.AddWithValue("@MedicineName", medicine.MedicineName);
                                insertCmd.Parameters.AddWithValue("@Dosage", medicine.Dosage);
                                insertCmd.Parameters.AddWithValue("@Schedule", medicine.Schedule);
                                insertCmd.Parameters.AddWithValue("@Instructions", string.IsNullOrWhiteSpace(medicine.Instructions) ? DBNull.Value : (object)medicine.Instructions);
                                insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                                insertCmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                                insertCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating maintenance medicines: {ex.Message}");
            }
        }

        // Helper method to map reader to Senior object
        private Senior MapSeniorFromReader(MySqlDataReader reader)
        {
            return new Senior
            {
                Id = reader.GetInt32("Id"),
                s_firstName = reader.GetString("s_firstName"),
                s_middleName = reader.IsDBNull(reader.GetOrdinal("s_middleName")) ? "" : reader.GetString("s_middleName"),
                s_lastName = reader.GetString("s_lastName"),
                s_sex = reader.GetString("s_sex"),
                s_dob = reader.GetDateTime("s_dob"),
                s_age = reader.GetInt32("s_age"),
                s_contact = reader.IsDBNull(reader.GetOrdinal("s_contact")) ? "" : reader.GetString("s_contact"),
                s_barangay = reader.IsDBNull(reader.GetOrdinal("s_barangay")) ? "" : reader.GetString("s_barangay"),
                s_guardian_zone = reader.IsDBNull(reader.GetOrdinal("s_guardian_zone")) ? "" : reader.GetString("s_guardian_zone"),
                s_religion = reader.IsDBNull(reader.GetOrdinal("s_religion")) ? "" : reader.GetString("s_religion"),
                s_bloodtype = reader.IsDBNull(reader.GetOrdinal("s_bloodtype")) ? "" : reader.GetString("s_bloodtype"),
                Status = reader.GetString("Status"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedAt = reader.GetDateTime("UpdatedAt"),
                DeletedAt = reader.IsDBNull(reader.GetOrdinal("DeletedAt")) ? (DateTime?)null : reader.GetDateTime("DeletedAt"),
                IsDeleted = reader.GetBoolean("IsDeleted"),

                // Health Information
                s_health_problems_option = reader.IsDBNull(reader.GetOrdinal("s_health_problems_option")) ? "No" : reader.GetString("s_health_problems_option"),
                s_health_problems = reader.IsDBNull(reader.GetOrdinal("s_health_problems")) ? "" : reader.GetString("s_health_problems"),
                s_maintenance_option = reader.IsDBNull(reader.GetOrdinal("s_maintenance_option")) ? "No" : reader.GetString("s_maintenance_option"),
                s_maintenance = reader.IsDBNull(reader.GetOrdinal("s_maintenance")) ? "" : reader.GetString("s_maintenance"),
                s_disability_option = reader.IsDBNull(reader.GetOrdinal("s_disability_option")) ? "No" : reader.GetString("s_disability_option"),
                s_disability = reader.IsDBNull(reader.GetOrdinal("s_disability")) ? "" : reader.GetString("s_disability"),
                s_visual_option = reader.IsDBNull(reader.GetOrdinal("s_visual_option")) ? "No" : reader.GetString("s_visual_option"),
                s_visual = reader.IsDBNull(reader.GetOrdinal("s_visual")) ? "" : reader.GetString("s_visual"),
                s_hearing_option = reader.IsDBNull(reader.GetOrdinal("s_hearing_option")) ? "No" : reader.GetString("s_hearing_option"),
                s_hearing = reader.IsDBNull(reader.GetOrdinal("s_hearing")) ? "" : reader.GetString("s_hearing"),
                s_emotional_option = reader.IsDBNull(reader.GetOrdinal("s_emotional_option")) ? "No" : reader.GetString("s_emotional_option"),
                s_emotional = reader.IsDBNull(reader.GetOrdinal("s_emotional")) ? "" : reader.GetString("s_emotional"),

                // Family Information
                s_spouse = reader.IsDBNull(reader.GetOrdinal("s_spouse")) ? "" : reader.GetString("s_spouse"),
                s_spouse_age = reader.IsDBNull(reader.GetOrdinal("s_spouse_age")) ? null : (int?)reader.GetInt32("s_spouse_age"),
                s_spouse_occupation = reader.IsDBNull(reader.GetOrdinal("s_spouse_occupation")) ? "" : reader.GetString("s_spouse_occupation"),
                s_spouse_contact = reader.IsDBNull(reader.GetOrdinal("s_spouse_contact")) ? "" : reader.GetString("s_spouse_contact"),
                s_children = reader.IsDBNull(reader.GetOrdinal("s_children")) ? "" : reader.GetString("s_children"),

                // Guardian/Emergency Contact
                s_guardian_name = reader.IsDBNull(reader.GetOrdinal("s_guardian_name")) ? "" : reader.GetString("s_guardian_name"),
                s_guardian_relationship = reader.IsDBNull(reader.GetOrdinal("s_guardian_relationship")) ? "" : reader.GetString("s_guardian_relationship"),
                s_guardian_relationship_other = reader.IsDBNull(reader.GetOrdinal("s_guardian_relationship_other")) ? "" : reader.GetString("s_guardian_relationship_other"),
                s_guardian_contact = reader.IsDBNull(reader.GetOrdinal("s_guardian_contact")) ? "" : reader.GetString("s_guardian_contact"),
                s_guardian_address = reader.IsDBNull(reader.GetOrdinal("s_guardian_address")) ? "" : reader.GetString("s_guardian_address")
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
                                   SET s_firstName = @FirstName, s_middleName = @MiddleName, s_lastName = @LastName, 
                                       s_sex = @Sex, s_dob = @Dob, s_age = @Age, s_contact = @Contact,
                                       s_barangay = @Barangay, s_guardian_zone = @GuardianZone, s_religion = @Religion, 
                                       s_bloodtype = @BloodType,
                                       s_health_problems_option = @HealthProblemsOption, s_health_problems = @HealthProblems,
                                       s_maintenance_option = @MaintenanceOption, s_maintenance = @Maintenance,
                                       s_disability_option = @DisabilityOption, s_disability = @Disability,
                                       s_visual_option = @VisualOption, s_visual = @Visual,
                                       s_hearing_option = @HearingOption, s_hearing = @Hearing,
                                       s_emotional_option = @EmotionalOption, s_emotional = @Emotional,
                                       s_spouse = @Spouse, s_spouse_age = @SpouseAge, s_spouse_occupation = @SpouseOccupation,
                                       s_spouse_contact = @SpouseContact, s_children = @Children,
                                       s_guardian_name = @GuardianName, s_guardian_relationship = @GuardianRelationship,
                                       s_guardian_relationship_other = @GuardianRelationshipOther, s_guardian_contact = @GuardianContact,
                                       s_guardian_address = @GuardianAddress,
                                       UpdatedAt = @UpdatedAt
                                   WHERE Id = @Id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        // Personal Information
                        cmd.Parameters.AddWithValue("@Id", senior.Id);
                        cmd.Parameters.AddWithValue("@FirstName", senior.s_firstName ?? "");
                        cmd.Parameters.AddWithValue("@MiddleName", string.IsNullOrEmpty(senior.s_middleName) ? DBNull.Value : senior.s_middleName);
                        cmd.Parameters.AddWithValue("@LastName", senior.s_lastName ?? "");
                        cmd.Parameters.AddWithValue("@Sex", senior.s_sex ?? "");
                        cmd.Parameters.AddWithValue("@Dob", senior.s_dob);
                        cmd.Parameters.AddWithValue("@Age", senior.s_age);
                        cmd.Parameters.AddWithValue("@Contact", string.IsNullOrEmpty(senior.s_contact) ? DBNull.Value : senior.s_contact);
                        cmd.Parameters.AddWithValue("@Barangay", string.IsNullOrEmpty(senior.s_barangay) ? DBNull.Value : senior.s_barangay);
                        cmd.Parameters.AddWithValue("@GuardianZone", string.IsNullOrEmpty(senior.s_guardian_zone) ? DBNull.Value : senior.s_guardian_zone);
                        cmd.Parameters.AddWithValue("@Religion", string.IsNullOrEmpty(senior.s_religion) ? DBNull.Value : senior.s_religion);
                        cmd.Parameters.AddWithValue("@BloodType", string.IsNullOrEmpty(senior.s_bloodtype) ? DBNull.Value : senior.s_bloodtype);

                        // Health Information
                        cmd.Parameters.AddWithValue("@HealthProblemsOption", senior.s_health_problems_option ?? "No");
                        cmd.Parameters.AddWithValue("@HealthProblems", string.IsNullOrEmpty(senior.s_health_problems) ? DBNull.Value : senior.s_health_problems);
                        cmd.Parameters.AddWithValue("@MaintenanceOption", senior.s_maintenance_option ?? "No");
                        cmd.Parameters.AddWithValue("@Maintenance", string.IsNullOrEmpty(senior.s_maintenance) ? DBNull.Value : senior.s_maintenance);
                        cmd.Parameters.AddWithValue("@DisabilityOption", senior.s_disability_option ?? "No");
                        cmd.Parameters.AddWithValue("@Disability", string.IsNullOrEmpty(senior.s_disability) ? DBNull.Value : senior.s_disability);
                        cmd.Parameters.AddWithValue("@VisualOption", senior.s_visual_option ?? "No");
                        cmd.Parameters.AddWithValue("@Visual", string.IsNullOrEmpty(senior.s_visual) ? DBNull.Value : senior.s_visual);
                        cmd.Parameters.AddWithValue("@HearingOption", senior.s_hearing_option ?? "No");
                        cmd.Parameters.AddWithValue("@Hearing", string.IsNullOrEmpty(senior.s_hearing) ? DBNull.Value : senior.s_hearing);
                        cmd.Parameters.AddWithValue("@EmotionalOption", senior.s_emotional_option ?? "No");
                        cmd.Parameters.AddWithValue("@Emotional", string.IsNullOrEmpty(senior.s_emotional) ? DBNull.Value : senior.s_emotional);

                        // Family Information
                        cmd.Parameters.AddWithValue("@Spouse", string.IsNullOrEmpty(senior.s_spouse) ? DBNull.Value : senior.s_spouse);
                        cmd.Parameters.AddWithValue("@SpouseAge", senior.s_spouse_age.HasValue ? (object)senior.s_spouse_age.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@SpouseOccupation", string.IsNullOrEmpty(senior.s_spouse_occupation) ? DBNull.Value : senior.s_spouse_occupation);
                        cmd.Parameters.AddWithValue("@SpouseContact", string.IsNullOrEmpty(senior.s_spouse_contact) ? DBNull.Value : senior.s_spouse_contact);
                        cmd.Parameters.AddWithValue("@Children", string.IsNullOrEmpty(senior.s_children) ? DBNull.Value : senior.s_children);

                        // Guardian/Emergency Contact
                        cmd.Parameters.AddWithValue("@GuardianName", string.IsNullOrEmpty(senior.s_guardian_name) ? DBNull.Value : senior.s_guardian_name);
                        cmd.Parameters.AddWithValue("@GuardianRelationship", string.IsNullOrEmpty(senior.s_guardian_relationship) ? DBNull.Value : senior.s_guardian_relationship);
                        cmd.Parameters.AddWithValue("@GuardianRelationshipOther", string.IsNullOrEmpty(senior.s_guardian_relationship_other) ? DBNull.Value : senior.s_guardian_relationship_other);
                        cmd.Parameters.AddWithValue("@GuardianContact", string.IsNullOrEmpty(senior.s_guardian_contact) ? DBNull.Value : senior.s_guardian_contact);
                        cmd.Parameters.AddWithValue("@GuardianAddress", string.IsNullOrEmpty(senior.s_guardian_address) ? DBNull.Value : senior.s_guardian_address);

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

        // Parse children from text format
        private List<Child> ParseChildrenFromText(string childrenText)
        {
            var children = new List<Child>();

            if (string.IsNullOrEmpty(childrenText))
                return children;

            var lines = childrenText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var child = new Child();
                // Simple parsing - you can enhance this based on your format
                child.Name = line.Trim();
                children.Add(child);
            }

            return children;
        }

        // Format children list to text
        private string FormatChildrenText(List<Child> children)
        {
            if (children == null || !children.Any())
                return null;

            var lines = new List<string>();
            foreach (var child in children.Where(c => !string.IsNullOrWhiteSpace(c.Name)))
            {
                var parts = new List<string> { child.Name };

                if (child.Age.HasValue)
                    parts.Add($"Age: {child.Age}");

                if (!string.IsNullOrWhiteSpace(child.Relationship))
                    parts.Add($"({child.Relationship})");

                lines.Add(string.Join(" ", parts));
            }

            return string.Join("\n", lines);
        }
    }
}