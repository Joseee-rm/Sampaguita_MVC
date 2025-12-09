using Microsoft.AspNetCore.Mvc;
using SeniorManagement.Models;
using SeniorManagement.Repositories;
using System.Globalization;
using System.Text;

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

        // GET: Dues/ViewLogs
        // GET: Dues/ViewLogs
        public async Task<IActionResult> ViewLogs()
        {
            try
            {
                var logs = await _repository.GetContributionLogsAsync();
                return View(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading contribution logs");
                ViewBag.ErrorMessage = "Error loading logs: " + ex.Message; // Changed from TempData to ViewBag
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

        private string GenerateCsvContent(List<MonthlyContribution> contributions, string monthName, int year)
        {
            var sb = new StringBuilder();

            // Add headers
            sb.AppendLine("Senior ID,Full Name,Zone,Status,Payment Status,Paid Date,Month,Year");

            // Add data
            foreach (var contribution in contributions)
            {
                var paidDate = contribution.PaidDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not Paid";
                var paymentStatus = contribution.IsPaid ? "PAID" : "NOT PAID";

                sb.AppendLine($"\"{contribution.SeniorId}\",\"{contribution.FullName}\",\"{contribution.Zone}\",\"{contribution.Status}\",\"{paymentStatus}\",\"{paidDate}\",\"{monthName}\",\"{year}\"");
            }

            // Add summary
            sb.AppendLine();
            sb.AppendLine($"Summary for {monthName} {year}");
            sb.AppendLine($"Total Seniors: {contributions.Count}");
            sb.AppendLine($"Paid: {contributions.Count(c => c.IsPaid)}");
            sb.AppendLine($"Not Paid: {contributions.Count(c => !c.IsPaid)}");
            sb.AppendLine($"Total Amount: {contributions.Count * 10} pesos");
            sb.AppendLine($"Collected Amount: {contributions.Count(c => c.IsPaid) * 10} pesos");
            sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }
    }
}