namespace SeniorManagement.Models
{
    public class PensionLog
    {
        public int Id { get; set; }
        public string Month { get; set; }
        public int Year { get; set; }
        public string FilePath { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}