// Models/Event.cs - Complete Fixed Version
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SeniorManagement.Models
{
    public class Event
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Event title is required")]
        [Display(Name = "Event Title")]
        [StringLength(200)]
        public string EventTitle { get; set; }

        [Required(ErrorMessage = "Event description is required")]
        [Display(Name = "Event Description")]
        public string EventDescription { get; set; }

        [Required(ErrorMessage = "Event type is required")]
        [Display(Name = "Event Type")]
        public string EventType { get; set; }

        [Required(ErrorMessage = "Event date is required")]
        [Display(Name = "Event Date")]
        [DataType(DataType.Date)]
        public DateTime EventDate { get; set; }

        [Required(ErrorMessage = "Event time is required")]
        [Display(Name = "Event Time")]
        [DataType(DataType.Time)]
        public TimeSpan EventTime { get; set; }

        [Required(ErrorMessage = "Event location is required")]
        [Display(Name = "Event Location")]
        public string EventLocation { get; set; }

        [Required(ErrorMessage = "Organizer is required")]
        [Display(Name = "Organized By")]
        public string OrganizedBy { get; set; }

        [Display(Name = "Maximum Capacity")]
        [Range(0, int.MaxValue)]
        public int? MaxCapacity { get; set; }

        [Display(Name = "Attendance Count")]
        [Range(0, int.MaxValue)]
        public int AttendanceCount { get; set; } = 0;

        [Display(Name = "Status")]
        public string Status { get; set; } = "Scheduled";

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? DeletedAt { get; set; }

        // Display properties (not stored in database)
        [NotMapped]
        public string TimeFormatted
        {
            get
            {
                try
                {
                    var time = DateTime.Today.Add(EventTime);
                    return time.ToString("h:mm tt");
                }
                catch
                {
                    return EventTime.ToString(@"hh\:mm");
                }
            }
        }

        [NotMapped]
        public string DateTimeFormatted
        {
            get
            {
                try
                {
                    var dateTime = EventDate.Add(EventTime);
                    return dateTime.ToString("MMM dd, yyyy") + " at " +
                           dateTime.ToString("h:mm tt");
                }
                catch
                {
                    return $"{EventDate:MMM dd, yyyy} at {EventTime:hh\\:mm}";
                }
            }
        }
    }
}