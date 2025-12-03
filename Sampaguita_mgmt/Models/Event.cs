using System;
using System.ComponentModel.DataAnnotations;

namespace SeniorManagement.Models
{
    public class Event
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Event title is required")]
        [Display(Name = "Event Title")]
        [StringLength(255, ErrorMessage = "Event title cannot exceed 255 characters")]
        public string EventTitle { get; set; }

        [Required(ErrorMessage = "Event type is required")]
        [Display(Name = "Event Type")]
        public string EventType { get; set; }

        [Required(ErrorMessage = "Event date is required")]
        [Display(Name = "Event Date")]
        [DataType(DataType.Date)]
        public DateTime EventDate { get; set; }

        [Required(ErrorMessage = "Event time is required")]
        [Display(Name = "Event Time")]
        public string EventTime { get; set; }

        [Required(ErrorMessage = "Event location is required")]
        [Display(Name = "Event Location")]
        [StringLength(255, ErrorMessage = "Location cannot exceed 255 characters")]
        public string EventLocation { get; set; }

        [Required(ErrorMessage = "Organizer name is required")]
        [Display(Name = "Organized By")]
        [StringLength(100, ErrorMessage = "Organizer name cannot exceed 100 characters")]
        public string OrganizedBy { get; set; }

        [Required(ErrorMessage = "Event description is required")]
        [Display(Name = "Event Description")]
        public string EventDescription { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Scheduled";

        [Display(Name = "Attendance Count")]
        public int AttendanceCount { get; set; }

        [Display(Name = "Max Capacity")]
        [Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1")]
        public int? MaxCapacity { get; set; }

        [Display(Name = "Is Deleted")]
        public bool IsDeleted { get; set; } = false;

        [Display(Name = "Deleted At")]
        public DateTime? DeletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Helper property for display
        [Display(Name = "Event Type")]
        public string EventTypeDisplay => EventType switch
        {
            "Medical Mission" => "Medical Mission",
            "Assistance Program" => "Assistance Program",
            "Wellness Activity" => "Wellness Activity",
            "Community Gathering" => "Community Gathering",
            _ => EventType
        };

        // Helper property for displaying date and time together
        [Display(Name = "Event Date & Time")]
        public DateTime EventDateTime
        {
            get
            {
                if (DateTime.TryParse($"{EventDate:yyyy-MM-dd} {EventTime}", out DateTime result))
                    return result;
                return EventDate;
            }
        }

        // Helper method to get TimeSpan for database operations
        public TimeSpan GetEventTimeSpan()
        {
            if (TimeSpan.TryParse(EventTime, out TimeSpan result))
                return result;

            if (DateTime.TryParse(EventTime, out DateTime dateTime))
                return dateTime.TimeOfDay;

            return TimeSpan.Zero;
        }

        // Helper property for display formatting
        [Display(Name = "Formatted Time")]
        public string FormattedTime => GetEventTimeSpan().ToString(@"hh\:mm");

        // Helper property for 12-hour format display
        [Display(Name = "Display Time")]
        public string DisplayTime
        {
            get
            {
                var timeSpan = GetEventTimeSpan();
                var datetime = DateTime.Today.Add(timeSpan);
                return datetime.ToString("h:mm tt");
            }
        }

        // Helper property for attendance percentage
        [Display(Name = "Attendance Percentage")]
        public double AttendancePercentage
        {
            get
            {
                if (MaxCapacity.HasValue && MaxCapacity.Value > 0)
                {
                    return (AttendanceCount / (double)MaxCapacity.Value) * 100;
                }
                return 0;
            }
        }
    }
}