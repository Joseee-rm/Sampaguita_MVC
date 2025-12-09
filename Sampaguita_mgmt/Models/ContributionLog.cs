namespace SeniorManagement.Models
{
    public class ContributionLog
    {
        public int Id { get; set; }
        public string Month { get; set; }  // Make sure this is "Month" not "LogMonth"
        public int Year { get; set; }      // Make sure this is "Year" not "LogYear"
        public string FilePath { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Notes { get; set; }
        public string MonthYear => $"{Month} {Year}";
    }
}