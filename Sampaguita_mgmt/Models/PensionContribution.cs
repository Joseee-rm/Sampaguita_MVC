namespace SeniorManagement.Models
{
    public class PensionContribution
    {
        public int Id { get; set; }
        public int SeniorId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public bool IsClaimed { get; set; }
        public DateTime? ClaimedDate { get; set; }
        public DateTime CreatedAt { get; set; }

        // Joined fields from Senior Table
        public string FullName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleInitial { get; set; }
        public int Zone { get; set; }
        public string Status { get; set; }
    }
}