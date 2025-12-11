using Microsoft.AspNetCore.Mvc;
using SeniorManagement.Models;
using SeniorManagement.Repositories;
using System.Globalization;
using System.Text;
using System.IO; // For Path.Combine and Directory.CreateDirectory
using System.Linq; // For LINQ methods like .Where(), .Count()

namespace SeniorManagement.Controllers
{
    public class DuesController : Controller
    {
        private readonly IMonthlyContributionRepository _repository;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DuesController> _logger;

        public DuesController(
            IMonthlyContributionRepository repository,
            IWebHostEnvironment environment,
            ILogger<DuesController> logger)
        {
            _repository = repository;
            _environment = environment;
            _logger = logger;
        }

        // GET: Dues
        public async Task<IActionResult> Index(int? month, int? year, string message = "", string messageType = "")
        {
            try
            {
                // Set default to current month/year if not specified
                month ??= DateTime.Now.Month;
                year ??= DateTime.Now.Year;

                // Get contributions for the selected month/year
                var contributions = await _repository.GetMonthlyContributionsAsync(month.Value, year.Value);

                // Get new seniors count for notification
                var newSeniorsCount = await _repository.GetNewSeniorsCountAsync(month.Value, year.Value);

                // Check if log exists for this month
                var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Value);
                var existingLog = await _repository.GetContributionLogAsync(monthName, year.Value);

                // Calculate statistics
                var paidCount = contributions.Count(c => c.IsPaid);
                var unpaidCount = contributions.Count(c => !c.IsPaid);
                var totalAmount = contributions.Count * 10;
                var collectedAmount = paidCount * 10;

                ViewBag.Month = month;
                ViewBag.Year = year;
                ViewBag.MonthName = monthName;
                ViewBag.PaidCount = paidCount;
                ViewBag.UnpaidCount = unpaidCount;
                ViewBag.TotalAmount = totalAmount;
                ViewBag.CollectedAmount = collectedAmount;
                ViewBag.NewSeniorsCount = newSeniorsCount;
                ViewBag.HasLog = existingLog != null; // This ensures boolean value
                ViewBag.Message = message;
                ViewBag.MessageType = messageType;

                return View(contributions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading monthly contributions");
                ViewBag.Message = "Error loading contributions: " + ex.Message;
                ViewBag.MessageType = "danger";
                ViewBag.HasLog = false; // Ensure HasLog is set even on error
                return View(new List<MonthlyContribution>());
            }
        }

        // POST: Dues/TogglePayment
        [HttpPost]
        public async Task<IActionResult> TogglePayment(int id)
        {
            try
            {
                var success = await _repository.TogglePaymentAsync(id);

                if (success)
                {
                    return Json(new { success = true, message = "Payment status updated successfully!" });
                }

                return Json(new { success = false, message = "Failed to update payment status." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling payment");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // GET: Dues/ViewLogs with date range filter
        public async Task<IActionResult> ViewLogs(string fromMonth = null, int? fromYear = null,
                                                  string toMonth = null, int? toYear = null)
        {
            try
            {
                var logs = await _repository.GetContributionLogsAsync();

                // Store filter values in ViewBag
                if (!string.IsNullOrEmpty(fromMonth)) ViewBag.FromMonth = fromMonth;
                if (fromYear.HasValue) ViewBag.FromYear = fromYear;
                if (!string.IsNullOrEmpty(toMonth)) ViewBag.ToMonth = toMonth;
                if (toYear.HasValue) ViewBag.ToYear = toYear;

                return View(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading contribution logs");
                ViewBag.ErrorMessage = "Error loading logs: " + ex.Message;
                return View(new List<ContributionLog>());
            }
        }

        // POST: Dues/SaveLog
        [HttpPost]
        public async Task<IActionResult> SaveLog(int month, int year)
        {
            try
            {
                // Generate CSV file
                var contributions = await _repository.GetContributionsForExportAsync(month, year);
                var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);

                // Create CSV content
                var csvContent = GenerateCsvContent(contributions, monthName, year);

                // Define file path
                var fileName = $"MonthlyContributions_{monthName}_{year}_{DateTime.Now:yyyyMMddHHmmss}.csv";
                var logsFolder = Path.Combine(_environment.WebRootPath, "logs", "contributions");

                // Ensure directory exists
                Directory.CreateDirectory(logsFolder);

                var filePath = Path.Combine(logsFolder, fileName);

                // Save file
                await System.IO.File.WriteAllTextAsync(filePath, csvContent, Encoding.UTF8);

                // Save log entry
                var relativePath = $"/logs/contributions/{fileName}";
                var notes = $"Total Seniors: {contributions.Count}, Paid: {contributions.Count(c => c.IsPaid)}, Unpaid: {contributions.Count(c => !c.IsPaid)}";

                var log = await _repository.SaveContributionLogAsync(monthName, year, relativePath, notes);

                return RedirectToAction("Index", new
                {
                    month,
                    year,
                    message = $"Log saved successfully! File: {fileName}",
                    messageType = "success"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving log");
                return RedirectToAction("Index", new
                {
                    month,
                    year,
                    message = "Error saving log: " + ex.Message,
                    messageType = "danger"
                });
            }
        }

        // POST: Dues/UpdateLog
        [HttpPost]
        public async Task<IActionResult> UpdateLog(int month, int year)
        {
            try
            {
                // Get existing log
                var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
                var existingLog = await _repository.GetContributionLogAsync(monthName, year);

                if (existingLog == null)
                {
                    return RedirectToAction("Index", new
                    {
                        month,
                        year,
                        message = "No log found to update. Please save a log first.",
                        messageType = "warning"
                    });
                }

                // Generate new CSV file
                var contributions = await _repository.GetContributionsForExportAsync(month, year);

                // Create CSV content
                var csvContent = GenerateCsvContent(contributions, monthName, year);

                // Define file path - use the same filename or generate new one
                var fileName = $"MonthlyContributions_{monthName}_{year}_{DateTime.Now:yyyyMMddHHmmss}.csv";
                var logsFolder = Path.Combine(_environment.WebRootPath, "logs", "contributions");

                // Ensure directory exists
                Directory.CreateDirectory(logsFolder);

                var filePath = Path.Combine(logsFolder, fileName);

                // Save new file
                await System.IO.File.WriteAllTextAsync(filePath, csvContent, Encoding.UTF8);

                // Update log entry with new file
                var relativePath = $"/logs/contributions/{fileName}";
                var notes = $"Total Seniors: {contributions.Count}, Paid: {contributions.Count(c => c.IsPaid)}, Unpaid: {contributions.Count(c => !c.IsPaid)}. Updated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                var log = await _repository.SaveContributionLogAsync(monthName, year, relativePath, notes);

                // Delete old file if it exists and is different
                if (!string.IsNullOrEmpty(existingLog.FilePath) && existingLog.FilePath != relativePath)
                {
                    var oldFilePath = Path.Combine(_environment.WebRootPath, existingLog.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                return RedirectToAction("Index", new
                {
                    month,
                    year,
                    message = $"Log updated successfully! New file: {fileName}",
                    messageType = "success"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating log");
                return RedirectToAction("Index", new
                {
                    month,
                    year,
                    message = "Error updating log: " + ex.Message,
                    messageType = "danger"
                });
            }
        }

        // GET: Dues/ExportCsv
        public async Task<IActionResult> ExportCsv(int month, int year)
        {
            try
            {
                var contributions = await _repository.GetContributionsForExportAsync(month, year);
                var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);

                var csvContent = GenerateCsvContent(contributions, monthName, year);
                var fileName = $"MonthlyContributions_{monthName}_{year}.csv";

                return File(Encoding.UTF8.GetBytes(csvContent), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting CSV");
                TempData["ErrorMessage"] = "Error exporting CSV: " + ex.Message;
                return RedirectToAction("Index", new { month, year });
            }
        }

        // GET: Dues/ExportFilteredCsv
        public async Task<IActionResult> ExportFilteredCsv(string fromMonth, int fromYear,
                                                           string toMonth, int toYear)
        {
            try
            {
                // Get all logs
                var logs = await _repository.GetContributionLogsAsync();

                // Filter logs by date range
                var months = new[] { "January", "February", "March", "April", "May", "June",
                                   "July", "August", "September", "October", "November", "December" };

                int fromMonthNum = Array.IndexOf(months, fromMonth) + 1;
                int toMonthNum = Array.IndexOf(months, toMonth) + 1;

                var filteredLogs = logs.Where(log =>
                {
                    int logMonthNum = Array.IndexOf(months, log.Month) + 1;

                    if (log.Year == fromYear && log.Year == toYear)
                    {
                        return logMonthNum >= fromMonthNum && logMonthNum <= toMonthNum;
                    }
                    else if (log.Year == fromYear)
                    {
                        return logMonthNum >= fromMonthNum;
                    }
                    else if (log.Year == toYear)
                    {
                        return logMonthNum <= toMonthNum;
                    }
                    else
                    {
                        return log.Year > fromYear && log.Year < toYear;
                    }
                }).ToList();

                // Get total collected amount from all filtered logs
                int totalCollectedAmount = 0;
                int totalPaidSeniors = 0;
                int totalSeniors = 0;

                foreach (var log in filteredLogs)
                {
                    // Parse the notes to extract paid count and total seniors
                    // Format: "Total Seniors: X, Paid: Y, Unpaid: Z"
                    var notesParts = log.Notes.Split(',');
                    if (notesParts.Length >= 2)
                    {
                        // Extract total seniors
                        if (notesParts[0].Contains("Total Seniors:"))
                        {
                            var totalPart = notesParts[0].Split(':');
                            if (totalPart.Length >= 2 && int.TryParse(totalPart[1].Trim(), out int total))
                            {
                                totalSeniors += total;
                            }
                        }

                        // Extract paid seniors
                        if (notesParts[1].Contains("Paid:"))
                        {
                            var paidPart = notesParts[1].Split(':');
                            if (paidPart.Length >= 2 && int.TryParse(paidPart[1].Trim(), out int paid))
                            {
                                totalPaidSeniors += paid;
                            }
                        }
                    }
                }

                // Calculate total collected amount (assuming ₱10 per senior)
                totalCollectedAmount = totalPaidSeniors * 10;

                // Generate CSV content
                var sb = new StringBuilder();

                // Add headers
                sb.AppendLine("Month,Year,Saved Date,Notes,File Path");

                // Add data
                foreach (var log in filteredLogs.OrderBy(l => l.Year).ThenBy(l => Array.IndexOf(months, l.Month)))
                {
                    sb.AppendLine($"\"{log.Month}\",\"{log.Year}\",\"{log.CreatedAt:yyyy-MM-dd HH:mm:ss}\",\"{log.Notes}\",\"{log.FilePath}\"");
                }

                // Add summary
                sb.AppendLine();
                sb.AppendLine($"SUMMARY FOR DATE RANGE: {fromMonth} {fromYear} to {toMonth} {toYear}");
                sb.AppendLine($"Total Logs: {filteredLogs.Count}");
                sb.AppendLine($"Total Seniors (Accumulated): {totalSeniors}");
                sb.AppendLine($"Total Paid Seniors (Accumulated): {totalPaidSeniors}");
                sb.AppendLine($"Total Unpaid Seniors (Accumulated): {totalSeniors - totalPaidSeniors}");
                sb.AppendLine($"Total Collected Amount: ₱{totalCollectedAmount}.00");
                sb.AppendLine($"Total Expected Amount (if all paid): ₱{totalSeniors * 10}.00");
                sb.AppendLine($"Collection Rate: {(totalSeniors > 0 ? ((double)totalPaidSeniors / totalSeniors * 100).ToString("0.00") : "0.00")}%");
                sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                var fileName = $"Contribution_Logs_{fromMonth}_{fromYear}_to_{toMonth}_{toYear}.csv";
                return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting filtered CSV");
                TempData["ErrorMessage"] = "Error exporting CSV: " + ex.Message;
                return RedirectToAction("ViewLogs");
            }
        }

        // GET: Dues/DownloadLog
        public async Task<IActionResult> DownloadLog(string month, int year)
        {
            try
            {
                var log = await _repository.GetContributionLogAsync(month, year);

                if (log == null || string.IsNullOrEmpty(log.FilePath))
                {
                    TempData["ErrorMessage"] = "Log file not found";
                    return RedirectToAction("ViewLogs");
                }

                var filePath = Path.Combine(_environment.WebRootPath, log.FilePath.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                {
                    TempData["ErrorMessage"] = "File not found on server";
                    return RedirectToAction("ViewLogs");
                }

                var fileName = Path.GetFileName(filePath);
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

                return File(fileBytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading log");
                TempData["ErrorMessage"] = "Error downloading file: " + ex.Message;
                return RedirectToAction("ViewLogs");
            }
        }

        // Add these methods to your existing DuesController

        // GET: Dues/Pension
        public async Task<IActionResult> Pension(int? month, int? year, string message = "", string messageType = "")
        {
            try
            {
                // Set default to current month/year if not specified
                month ??= DateTime.Now.Month;
                year ??= DateTime.Now.Year;

                // Get pension contributions for the selected month/year
                var pensions = await _repository.GetMonthlyPensionsAsync(month.Value, year.Value);

                // Get new pension seniors count for notification
                var newPensionSeniorsCount = await _repository.GetNewPensionSeniorsCountAsync(month.Value, year.Value);

                // Check if log exists for this month
                var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Value);
                var existingLog = await _repository.GetPensionLogAsync(monthName, year.Value);

                // Calculate statistics
                var claimedCount = pensions.Count(c => c.IsClaimed);
                var unclaimedCount = pensions.Count(c => !c.IsClaimed);
                var totalPensionAmount = pensions.Count * 500; // Assuming ₱500 per pension
                var claimedAmount = claimedCount * 500;

                ViewBag.Month = month;
                ViewBag.Year = year;
                ViewBag.MonthName = monthName;
                ViewBag.ClaimedCount = claimedCount;
                ViewBag.UnclaimedCount = unclaimedCount;
                ViewBag.TotalPensionAmount = totalPensionAmount;
                ViewBag.ClaimedAmount = claimedAmount;
                ViewBag.NewPensionSeniorsCount = newPensionSeniorsCount;
                ViewBag.HasLog = existingLog != null;
                ViewBag.Message = message;
                ViewBag.MessageType = messageType;

                return View(pensions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pension contributions");
                ViewBag.Message = "Error loading pensions: " + ex.Message;
                ViewBag.MessageType = "danger";
                ViewBag.HasLog = false;
                return View(new List<PensionContribution>());
            }
        }

        // POST: Dues/TogglePensionClaim
        [HttpPost]
        public async Task<IActionResult> TogglePensionClaim(int id)
        {
            try
            {
                var success = await _repository.TogglePensionClaimAsync(id);

                if (success)
                {
                    return Json(new { success = true, message = "Pension claim status updated successfully!" });
                }

                return Json(new { success = false, message = "Failed to update pension claim status." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling pension claim");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // GET: Dues/ViewPensionLogs
        public async Task<IActionResult> ViewPensionLogs(string fromMonth = null, int? fromYear = null,
                                                      string toMonth = null, int? toYear = null)
        {
            try
            {
                var logs = await _repository.GetPensionLogsAsync();

                // Store filter values in ViewBag
                if (!string.IsNullOrEmpty(fromMonth)) ViewBag.FromMonth = fromMonth;
                if (fromYear.HasValue) ViewBag.FromYear = fromYear;
                if (!string.IsNullOrEmpty(toMonth)) ViewBag.ToMonth = toMonth;
                if (toYear.HasValue) ViewBag.ToYear = toYear;

                return View(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pension logs");
                ViewBag.ErrorMessage = "Error loading pension logs: " + ex.Message;
                return View(new List<PensionLog>());
            }
        }

        // POST: Dues/SavePensionLog
        [HttpPost]
        public async Task<IActionResult> SavePensionLog(int month, int year)
        {
            try
            {
                // Generate CSV file
                var pensions = await _repository.GetPensionsForExportAsync(month, year);
                var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);

                // Create CSV content
                var csvContent = GeneratePensionCsvContent(pensions, monthName, year);

                // Define file path
                var fileName = $"PensionContributions_{monthName}_{year}_{DateTime.Now:yyyyMMddHHmmss}.csv";
                var logsFolder = Path.Combine(_environment.WebRootPath, "logs", "pensions");

                // Ensure directory exists
                Directory.CreateDirectory(logsFolder);

                var filePath = Path.Combine(logsFolder, fileName);

                // Save file
                await System.IO.File.WriteAllTextAsync(filePath, csvContent, Encoding.UTF8);

                // Save log entry
                var relativePath = $"/logs/pensions/{fileName}";
                var notes = $"Total Seniors: {pensions.Count}, Claimed: {pensions.Count(c => c.IsClaimed)}, Unclaimed: {pensions.Count(c => !c.IsClaimed)}";

                var log = await _repository.SavePensionLogAsync(monthName, year, relativePath, notes);

                return RedirectToAction("Pension", new
                {
                    month,
                    year,
                    message = $"Pension log saved successfully! File: {fileName}",
                    messageType = "success"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving pension log");
                return RedirectToAction("Pension", new
                {
                    month,
                    year,
                    message = "Error saving pension log: " + ex.Message,
                    messageType = "danger"
                });
            }
        }

        // POST: Dues/UpdatePensionLog
        [HttpPost]
        public async Task<IActionResult> UpdatePensionLog(int month, int year)
        {
            try
            {
                // Get existing log
                var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
                var existingLog = await _repository.GetPensionLogAsync(monthName, year);

                if (existingLog == null)
                {
                    return RedirectToAction("Pension", new
                    {
                        month,
                        year,
                        message = "No pension log found to update. Please save a log first.",
                        messageType = "warning"
                    });
                }

                // Generate new CSV file
                var pensions = await _repository.GetPensionsForExportAsync(month, year);

                // Create CSV content
                var csvContent = GeneratePensionCsvContent(pensions, monthName, year);

                // Define file path
                var fileName = $"PensionContributions_{monthName}_{year}_{DateTime.Now:yyyyMMddHHmmss}.csv";
                var logsFolder = Path.Combine(_environment.WebRootPath, "logs", "pensions");

                // Ensure directory exists
                Directory.CreateDirectory(logsFolder);

                var filePath = Path.Combine(logsFolder, fileName);

                // Save new file
                await System.IO.File.WriteAllTextAsync(filePath, csvContent, Encoding.UTF8);

                // Update log entry with new file
                var relativePath = $"/logs/pensions/{fileName}";
                var notes = $"Total Seniors: {pensions.Count}, Claimed: {pensions.Count(c => c.IsClaimed)}, Unclaimed: {pensions.Count(c => !c.IsClaimed)}. Updated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                var log = await _repository.SavePensionLogAsync(monthName, year, relativePath, notes);

                // Delete old file if it exists and is different
                if (!string.IsNullOrEmpty(existingLog.FilePath) && existingLog.FilePath != relativePath)
                {
                    var oldFilePath = Path.Combine(_environment.WebRootPath, existingLog.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                return RedirectToAction("Pension", new
                {
                    month,
                    year,
                    message = $"Pension log updated successfully! New file: {fileName}",
                    messageType = "success"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pension log");
                return RedirectToAction("Pension", new
                {
                    month,
                    year,
                    message = "Error updating pension log: " + ex.Message,
                    messageType = "danger"
                });
            }
        }

        // GET: Dues/ExportPensionCsv
        public async Task<IActionResult> ExportPensionCsv(int month, int year)
        {
            try
            {
                var pensions = await _repository.GetPensionsForExportAsync(month, year);
                var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);

                var csvContent = GeneratePensionCsvContent(pensions, monthName, year);
                var fileName = $"PensionContributions_{monthName}_{year}.csv";

                return File(Encoding.UTF8.GetBytes(csvContent), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting pension CSV");
                TempData["ErrorMessage"] = "Error exporting pension CSV: " + ex.Message;
                return RedirectToAction("Pension", new { month, year });
            }
        }

        // GET: Dues/ExportFilteredPensionCsv
        public async Task<IActionResult> ExportFilteredPensionCsv(string fromMonth, int fromYear,
                                                               string toMonth, int toYear)
        {
            try
            {
                // Get all logs
                var logs = await _repository.GetPensionLogsAsync();

                // Filter logs by date range
                var months = new[] { "January", "February", "March", "April", "May", "June",
                           "July", "August", "September", "October", "November", "December" };

                int fromMonthNum = Array.IndexOf(months, fromMonth) + 1;
                int toMonthNum = Array.IndexOf(months, toMonth) + 1;

                var filteredLogs = logs.Where(log =>
                {
                    int logMonthNum = Array.IndexOf(months, log.Month) + 1;

                    if (log.Year == fromYear && log.Year == toYear)
                    {
                        return logMonthNum >= fromMonthNum && logMonthNum <= toMonthNum;
                    }
                    else if (log.Year == fromYear)
                    {
                        return logMonthNum >= fromMonthNum;
                    }
                    else if (log.Year == toYear)
                    {
                        return logMonthNum <= toMonthNum;
                    }
                    else
                    {
                        return log.Year > fromYear && log.Year < toYear;
                    }
                }).ToList();

                // Get total claimed amount from all filtered logs
                int totalClaimedAmount = 0;
                int totalClaimedSeniors = 0;
                int totalSeniors = 0;

                foreach (var log in filteredLogs)
                {
                    // Parse the notes to extract claimed count and total seniors
                    var notesParts = log.Notes.Split(',');
                    if (notesParts.Length >= 2)
                    {
                        // Extract total seniors
                        if (notesParts[0].Contains("Total Seniors:"))
                        {
                            var totalPart = notesParts[0].Split(':');
                            if (totalPart.Length >= 2 && int.TryParse(totalPart[1].Trim(), out int total))
                            {
                                totalSeniors += total;
                            }
                        }

                        // Extract claimed seniors
                        if (notesParts[1].Contains("Claimed:"))
                        {
                            var claimedPart = notesParts[1].Split(':');
                            if (claimedPart.Length >= 2 && int.TryParse(claimedPart[1].Trim(), out int claimed))
                            {
                                totalClaimedSeniors += claimed;
                            }
                        }
                    }
                }

                // Calculate total claimed amount (assuming ₱500 per senior)
                totalClaimedAmount = totalClaimedSeniors * 500;

                // Generate CSV content
                var sb = new StringBuilder();

                // Add headers
                sb.AppendLine("Month,Year,Saved Date,Notes,File Path");

                // Add data
                foreach (var log in filteredLogs.OrderBy(l => l.Year).ThenBy(l => Array.IndexOf(months, l.Month)))
                {
                    sb.AppendLine($"\"{log.Month}\",\"{log.Year}\",\"{log.CreatedAt:yyyy-MM-dd HH:mm:ss}\",\"{log.Notes}\",\"{log.FilePath}\"");
                }

                // Add summary
                sb.AppendLine();
                sb.AppendLine($"PENSION SUMMARY FOR DATE RANGE: {fromMonth} {fromYear} to {toMonth} {toYear}");
                sb.AppendLine($"Total Logs: {filteredLogs.Count}");
                sb.AppendLine($"Total Seniors (Accumulated): {totalSeniors}");
                sb.AppendLine($"Total Claimed Seniors (Accumulated): {totalClaimedSeniors}");
                sb.AppendLine($"Total Unclaimed Seniors (Accumulated): {totalSeniors - totalClaimedSeniors}");
                sb.AppendLine($"Total Claimed Amount: ₱{totalClaimedAmount}.00");
                sb.AppendLine($"Total Expected Amount (if all claimed): ₱{totalSeniors * 500}.00");
                sb.AppendLine($"Claim Rate: {(totalSeniors > 0 ? ((double)totalClaimedSeniors / totalSeniors * 100).ToString("0.00") : "0.00")}%");
                sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                var fileName = $"Pension_Logs_{fromMonth}_{fromYear}_to_{toMonth}_{toYear}.csv";
                return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting filtered pension CSV");
                TempData["ErrorMessage"] = "Error exporting pension CSV: " + ex.Message;
                return RedirectToAction("ViewPensionLogs");
            }
        }

        // GET: Dues/DownloadPensionLog
        public async Task<IActionResult> DownloadPensionLog(string month, int year)
        {
            try
            {
                var log = await _repository.GetPensionLogAsync(month, year);

                if (log == null || string.IsNullOrEmpty(log.FilePath))
                {
                    TempData["ErrorMessage"] = "Pension log file not found";
                    return RedirectToAction("ViewPensionLogs");
                }

                var filePath = Path.Combine(_environment.WebRootPath, log.FilePath.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                {
                    TempData["ErrorMessage"] = "File not found on server";
                    return RedirectToAction("ViewPensionLogs");
                }

                var fileName = Path.GetFileName(filePath);
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

                return File(fileBytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading pension log");
                TempData["ErrorMessage"] = "Error downloading pension file: " + ex.Message;
                return RedirectToAction("ViewPensionLogs");
            }
        }

        private string GeneratePensionCsvContent(List<PensionContribution> pensions, string monthName, int year)
        {
            var sb = new StringBuilder();

            // Add headers
            sb.AppendLine("Senior ID,Full Name,Zone,Status,Claim Status,Claimed Date,Month,Year");

            // Add data
            foreach (var pension in pensions)
            {
                var claimStatus = pension.IsClaimed ? "CLAIMED" : "NOT CLAIMED";
                var claimedDate = pension.IsClaimed
                    ? pension.ClaimedDate?.ToString("MM/dd/yyyy HH:mm") ?? "Claimed (Date Not Recorded)"
                    : "Not Claimed";

                sb.AppendLine($"\"{pension.SeniorId}\",\"{pension.FullName}\",\"{pension.Zone}\",\"{pension.Status}\",\"{claimStatus}\",\"{claimedDate}\",\"{monthName}\",\"{year}\"");
            }

            // Add summary
            sb.AppendLine();
            sb.AppendLine($"Pension Summary for {monthName} {year}");
            sb.AppendLine($"Total Seniors: {pensions.Count}");
            sb.AppendLine($"Claimed: {pensions.Count(c => c.IsClaimed)}");
            sb.AppendLine($"Not Claimed: {pensions.Count(c => !c.IsClaimed)}");
            sb.AppendLine($"Claimed Amount: ₱{pensions.Count(c => c.IsClaimed) * 500}.00");
            sb.AppendLine($"Total Pension Amount: ₱{pensions.Count * 500}.00");
            sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }

        private string GenerateCsvContent(List<MonthlyContribution> contributions, string monthName, int year)
        {
            var sb = new StringBuilder();

            // Add headers
            sb.AppendLine("Senior ID,Full Name,Zone,Status,Payment Status,Paid Date,Month,Year");

            // Add data
            foreach (var contribution in contributions)
            {
                var paymentStatus = contribution.IsPaid ? "PAID" : "NOT PAID";

                // For paid seniors, use the actual PaidDate in MM/dd/yyyy HH:mm format
                // For unpaid seniors, show "Not Paid"
                var paidDate = contribution.IsPaid
                    ? contribution.PaidDate?.ToString("MM/dd/yyyy HH:mm") ?? "Paid (Date Not Recorded)"
                    : "Not Paid";

                sb.AppendLine($"\"{contribution.SeniorId}\",\"{contribution.FullName}\",\"{contribution.Zone}\",\"{contribution.Status}\",\"{paymentStatus}\",\"{paidDate}\",\"{monthName}\",\"{year}\"");
            }

            // Add summary
            sb.AppendLine();
            sb.AppendLine($"Summary for {monthName} {year}");
            sb.AppendLine($"Total Seniors: {contributions.Count}");
            sb.AppendLine($"Paid: {contributions.Count(c => c.IsPaid)}");
            sb.AppendLine($"Not Paid: {contributions.Count(c => !c.IsPaid)}");
            sb.AppendLine($"Collected Amount: {contributions.Count(c => c.IsPaid) * 10} pesos");
            sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }
    }
}