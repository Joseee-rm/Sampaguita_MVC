using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SeniorManagement.Models;
using SeniorManagement.Helpers;
using MySql.Data.MySqlClient;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System;

namespace SeniorManagement.Controllers
{
    [Authorize]
    public class ReportController : BaseController
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ActivityHelper _activityHelper;

        public ReportController(DatabaseHelper dbHelper, ActivityHelper activityHelper)
        {
            _dbHelper = dbHelper;
            _activityHelper = activityHelper;
        }

        public IActionResult Index()
        {
            // Get actual data from database
            var seniors = GetAllSeniors();

            var viewModel = new ReportViewModel
            {
                TotalSeniors = seniors.Count,
                ActiveSeniors = seniors.Count(s => s.Status == "Active"),
                MaleCount = seniors.Count(s => s.s_sex?.ToLower() == "male"),
                FemaleCount = seniors.Count(s => s.s_sex?.ToLower() == "female"),
                ReportDate = DateTime.Now
            };

            // Barangay Distribution
            viewModel.SeniorsByBarangay = seniors
                .Where(s => !string.IsNullOrEmpty(s.s_barangay))
                .GroupBy(s => s.s_barangay)
                .OrderByDescending(g => g.Count())
                .Take(10) // Top 10 barangays
                .ToDictionary(g => g.Key, g => g.Count());

            // Age Group Distribution
            viewModel.SeniorsByAgeGroup = new Dictionary<string, int>
            {
                ["60-69"] = seniors.Count(s => s.s_age >= 60 && s.s_age <= 69),
                ["70-79"] = seniors.Count(s => s.s_age >= 70 && s.s_age <= 79),
                ["80-89"] = seniors.Count(s => s.s_age >= 80 && s.s_age <= 89),
                ["90+"] = seniors.Count(s => s.s_age >= 90)
            };

            // Health Conditions
            viewModel.HealthConditions = new Dictionary<string, int>
            {
                ["Health Problems"] = seniors.Count(s => s.s_health_problems_option == "Yes"),
                ["Maintenance"] = seniors.Count(s => s.s_maintenance_option == "Yes"),
                ["Visual Issues"] = seniors.Count(s => s.s_visual_option == "Yes"),
                ["Hearing Issues"] = seniors.Count(s => s.s_hearing_option == "Yes"),
                ["Emotional"] = seniors.Count(s => s.s_emotional_option == "Yes")
            };

            // Disabilities
            viewModel.Disabilities = new Dictionary<string, int>
            {
                ["With Disability"] = seniors.Count(s => s.s_disability_option == "Yes"),
                ["Without Disability"] = seniors.Count(s => s.s_disability_option != "Yes")
            };

            return View(viewModel);
        }

        // Get all seniors from database - SAME METHOD AS YOUR SENIOR CONTROLLER
        private List<Senior> GetAllSeniors()
        {
            var seniors = new List<Senior>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    connection.Open();
                    string query = @"SELECT Id, s_firstName, s_middleName, s_lastName, s_sex, s_dob, s_age, 
                                   s_contact, s_barangay, s_guardian_zone, s_religion, s_bloodtype, Status, CreatedAt, UpdatedAt,
                                   s_health_problems_option, s_health_problems, s_maintenance_option, s_maintenance,
                                   s_disability_option, s_disability, s_visual_option, s_visual,
                                   s_hearing_option, s_hearing, s_emotional_option, s_emotional,
                                   s_spouse, s_spouse_age, s_spouse_occupation, s_spouse_contact, s_children,
                                   s_guardian_name, s_guardian_relationship, s_guardian_relationship_other, 
                                   s_guardian_contact, s_guardian_address
                                   FROM seniors ORDER BY s_lastName, s_firstName";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            seniors.Add(new Senior
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
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting seniors for report: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading senior records for report.";
            }

            return seniors;
        }

        // PDF Export Functionality
        public async Task<IActionResult> ExportPdf()
        {
            var seniors = GetAllSeniors();

            if (!seniors.Any())
            {
                TempData["ErrorMessage"] = "No senior data available for export.";
                return RedirectToAction(nameof(Index));
            }

            // Generate PDF content
            var pdfContent = GeneratePdfContent(seniors);
            var bytes = Encoding.UTF8.GetBytes(pdfContent);

            // Log the activity
            await _activityHelper.LogActivityAsync(
                "Export PDF",
                $"Exported PDF report with {seniors.Count} senior records"
            );

            return File(bytes, "application/pdf", $"Senior_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // Excel Export Functionality
        public async Task<IActionResult> ExportExcel()
        {
            var seniors = GetAllSeniors();

            if (!seniors.Any())
            {
                TempData["ErrorMessage"] = "No senior data available for export.";
                return RedirectToAction(nameof(Index));
            }

            // Generate CSV (Excel-compatible)
            var csvContent = GenerateCsvContent(seniors);
            var bytes = Encoding.UTF8.GetBytes(csvContent);

            // Log the activity
            await _activityHelper.LogActivityAsync(
                "Export Excel",
                $"Exported Excel report with {seniors.Count} senior records"
            );

            return File(bytes, "application/vnd.ms-excel", $"Senior_Report_{DateTime.Now:yyyyMMdd}.csv");
        }

        private string GeneratePdfContent(List<Senior> seniors)
        {
            var sb = new StringBuilder();

            sb.AppendLine("SENIOR CITIZENS REPORT");
            sb.AppendLine($"Generated on: {DateTime.Now:MMMM dd, yyyy hh:mm tt}");
            sb.AppendLine("=".PadRight(50, '='));
            sb.AppendLine();

            // Summary
            sb.AppendLine("SUMMARY");
            sb.AppendLine($"Total Seniors: {seniors.Count}");
            sb.AppendLine($"Active Seniors: {seniors.Count(s => s.Status == "Active")}");
            sb.AppendLine($"Male: {seniors.Count(s => s.s_sex?.ToLower() == "male")}");
            sb.AppendLine($"Female: {seniors.Count(s => s.s_sex?.ToLower() == "female")}");
            sb.AppendLine($"With Health Problems: {seniors.Count(s => s.s_health_problems_option == "Yes")}");
            sb.AppendLine($"With Disability: {seniors.Count(s => s.s_disability_option == "Yes")}");
            sb.AppendLine();

            // Barangay Distribution
            sb.AppendLine("BARANGAY DISTRIBUTION");
            sb.AppendLine("=".PadRight(50, '='));
            var barangayGroups = seniors
                .Where(s => !string.IsNullOrEmpty(s.s_barangay))
                .GroupBy(s => s.s_barangay)
                .OrderByDescending(g => g.Count());

            foreach (var group in barangayGroups)
            {
                sb.AppendLine($"{group.Key}: {group.Count()} seniors");
            }
            sb.AppendLine();

            // Detailed List
            sb.AppendLine("DETAILED LIST");
            sb.AppendLine("=".PadRight(50, '='));
            sb.AppendLine($"{"ID",-5} {"Name",-25} {"Sex",-6} {"Age",-4} {"Barangay",-12} {"Status",-8} {"Health",-8} {"Disability",-10}");
            sb.AppendLine("-".PadRight(80, '-'));

            foreach (var senior in seniors)
            {
                sb.AppendLine($"{senior.Id,-5} {senior.FullName,-25} {senior.s_sex,-6} {senior.s_age,-4} {senior.s_barangay,-12} {senior.Status,-8} {senior.s_health_problems_option,-8} {senior.s_disability_option,-10}");
            }

            return sb.ToString();
        }

        private string GenerateCsvContent(List<Senior> seniors)
        {
            var sb = new StringBuilder();

            // Headers
            sb.AppendLine("ID,Full Name,First Name,Middle Name,Last Name,Sex,Age,Date of Birth,Barangay,Contact,Status,Health Problems,Maintenance Medicines,Visual Problems,Hearing Problems,Emotional Conditions,Disability");

            // Data
            foreach (var senior in seniors)
            {
                sb.AppendLine($"\"{senior.Id}\",\"{senior.FullName}\",\"{senior.s_firstName}\",\"{senior.s_middleName}\",\"{senior.s_lastName}\",\"{senior.s_sex}\",\"{senior.s_age}\",\"{senior.s_dob:yyyy-MM-dd}\",\"{senior.s_barangay}\",\"{senior.s_contact}\",\"{senior.Status}\",\"{senior.s_health_problems_option}\",\"{senior.s_maintenance_option}\",\"{senior.s_visual_option}\",\"{senior.s_hearing_option}\",\"{senior.s_emotional_option}\",\"{senior.s_disability_option}\"");
            }

            return sb.ToString();
        }

        // Additional method to get statistics for AJAX calls if needed
        [HttpGet]
        public async Task<JsonResult> GetStatistics()
        {
            try
            {
                var seniors = GetAllSeniors();

                var statistics = new
                {
                    total = seniors.Count,
                    active = seniors.Count(s => s.Status == "Active"),
                    male = seniors.Count(s => s.s_sex?.ToLower() == "male"),
                    female = seniors.Count(s => s.s_sex?.ToLower() == "female"),
                    withHealthProblems = seniors.Count(s => s.s_health_problems_option == "Yes"),
                    withDisability = seniors.Count(s => s.s_disability_option == "Yes"),
                    barangayDistribution = seniors
                        .Where(s => !string.IsNullOrEmpty(s.s_barangay))
                        .GroupBy(s => s.s_barangay)
                        .Select(g => new { barangay = g.Key, count = g.Count() })
                        .OrderByDescending(x => x.count)
                        .Take(5)
                };

                return Json(statistics);
            }
            catch (Exception ex)
            {
                await _activityHelper.LogErrorAsync(ex.Message, "Get Statistics");
                return Json(new { error = ex.Message });
            }
        }
    }
}