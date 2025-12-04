using System;

namespace SeniorManagement.Models
{
    public class AnnouncementDTO
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public int? RelatedEventId { get; set; }
        public bool IsRead { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TimeAgo { get; set; }
        public string FormattedDate { get; set; }
        public string BadgeColor { get; set; }
        public string Icon { get; set; }

        // Related event simplified data
        public string? EventTitle { get; set; }
        public DateTime? EventDate { get; set; }
        public string? EventLocation { get; set; }
        public string? OrganizedBy { get; set; }
    }
}