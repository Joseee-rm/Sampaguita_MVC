// Update IMonthlyContributionRepository.cs
using SeniorManagement.Models;

namespace SeniorManagement.Repositories
{
    public interface IMonthlyContributionRepository
    {
        Task<List<MonthlyContribution>> GetMonthlyContributionsAsync(int month, int year);
        Task<bool> TogglePaymentAsync(int contributionId);
        Task<int> CreateMonthlyEntriesAsync(int month, int year);
        Task<List<ContributionLog>> GetContributionLogsAsync();
        Task<ContributionLog> GetContributionLogAsync(string month, int year);
        Task<ContributionLog> SaveContributionLogAsync(string month, int year, string filePath, string notes);
        Task<List<MonthlyContribution>> GetContributionsForExportAsync(int month, int year);
        Task<int> GetNewSeniorsCountAsync(int month, int year);

        // Pension Methods - UPDATED WITH PENSION TYPE FILTERING
        Task<List<PensionContribution>> GetMonthlyPensionsAsync(int month, int year, string pensionType = null);
        Task<List<PensionContribution>> GetPensionsForExportAsync(int month, int year, string pensionType = null);
        Task<bool> TogglePensionClaimAsync(int id);
        Task<PensionLog> SavePensionLogAsync(string month, int year, string filePath, string notes);
        Task<PensionLog> GetPensionLogAsync(string month, int year);
        Task<List<PensionLog>> GetPensionLogsAsync();
        Task<int> GetNewPensionSeniorsCountAsync(int month, int year);
        Task<List<string>> GetDistinctPensionTypesAsync(); // NEW METHOD
        Task<int> CreateMonthlyPensionEntriesAsync(int month, int year); // NEW METHOD
        Task<PensionContribution> GetPensionContributionByIdAsync(int id);
    }
}