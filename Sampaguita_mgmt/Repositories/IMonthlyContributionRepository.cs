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

        // Pension Methods
        Task<List<PensionContribution>> GetMonthlyPensionsAsync(int month, int year);
        Task<bool> TogglePensionClaimAsync(int id);
        Task<List<PensionContribution>> GetPensionsForExportAsync(int month, int year);
        Task<PensionLog> SavePensionLogAsync(string month, int year, string filePath, string notes);
        Task<PensionLog> GetPensionLogAsync(string month, int year);
        Task<List<PensionLog>> GetPensionLogsAsync();
        Task<int> GetNewPensionSeniorsCountAsync(int month, int year);
    }
}