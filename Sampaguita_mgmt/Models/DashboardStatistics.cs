namespace SeniorManagement.Models
{
    public class DashboardStatistics
    {
        public int TotalSeniors { get; set; }
        public int ActiveSeniors { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int TotalEvents { get; set; }
        public int UpcomingEvents { get; set; }
        public string CurrentDate { get; set; }
        public int RecentRegistrations { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
    }
}