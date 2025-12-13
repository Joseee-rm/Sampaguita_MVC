// Models/Zone.cs
namespace SeniorManagement.Models
{
    public class Zone
    {
        public int Id { get; set; }
        public int ZoneNumber { get; set; }
        public string ZoneName { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}