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
    }
}