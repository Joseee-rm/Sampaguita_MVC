using System;
using System.ComponentModel.DataAnnotations;

namespace SeniorManagement.Models
{
    public class Announcement
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Title")]
        [StringLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
        public string Title { get; set; }

        [Required]
        [Display(Name = "Message")]
        public string Message { get; set; }

        [Required]
        [Display(Name = "Type")]
        public string Type { get; set; } = "Event"; // Event, System, Alert, Info

        [Display(Name = "Related Event ID")]
        public int? RelatedEventId { get; set; }

        [Display(Name = "Is Read")]
        public bool IsRead { get; set; } = false;

        [Display(Name = "Created By")]
        public string CreatedBy { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public Event RelatedEvent { get; set; }

        // Computed properties
        [Display(Name = "Time Ago")]
        public string TimeAgo
        {
            get
            {
                var timeSpan = DateTime.Now - CreatedAt;

                if (timeSpan.TotalMinutes < 1) return "Just now";
                if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes}m ago";
                if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h ago";
                if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays}d ago";

                return CreatedAt.ToString("MMM dd, yyyy");
            }
        }

        [Display(Name = "Formatted Date")]
        public string FormattedDate => CreatedAt.ToString("MMM dd, yyyy hh:mm tt");

        [Display(Name = "Badge Color")]
        public string BadgeColor => Type switch
        {
            "Event" => "bg-primary",
            "System" => "bg-secondary",
            "Alert" => "bg-danger",
            "Info" => "bg-info",
            _ => "bg-primary"
        };

        [Display(Name = "Icon")]
        public string Icon => Type switch
        {
            "Event" => "fa-calendar-alt",
            "System" => "fa-cog",
            "Alert" => "fa-exclamation-triangle",
            "Info" => "fa-info-circle",
            _ => "fa-bell"
        };
    }
}