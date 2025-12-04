// Models/Event.cs
using System;

namespace SeniorManagement.Models
{
    public class Event
    {
        public int Id { get; set; }
        public string EventTitle { get; set; }          
        public string EventDescription { get; set; }     
        public string EventType { get; set; }
        public DateTime EventDate { get; set; }
        public TimeSpan EventTime { get; set; }          
        public string EventLocation { get; set; }       
        public string OrganizedBy { get; set; }         
        public int? MaxCapacity { get; set; }            
        public int AttendanceCount { get; set; }      
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }         

        // Computed properties for view
        public bool IsFull => MaxCapacity.HasValue && MaxCapacity > 0 && AttendanceCount >= MaxCapacity.Value;
        public int AvailableSpots => MaxCapacity.HasValue && MaxCapacity > 0 ? MaxCapacity.Value - AttendanceCount : 0;
        public string DateFormatted => EventDate.ToString("MMM dd, yyyy");
        public string TimeFormatted => EventTime.ToString(@"hh\:mm");

        public string StartTime => EventTime.ToString(@"hh\:mm");
        public string EndTime => EventTime.ToString(@"hh\:mm");

        public string StatusColor
        {
            get
            {
                return Status switch
                {
                    "Scheduled" => "info",
                    "Ongoing" => "warning",
                    "Completed" => "success",
                    "Cancelled" => "danger",
                    _ => "secondary"
                };
            }
        }

        public string StatusIcon
        {
            get
            {
                return Status switch
                {
                    "Scheduled" => "fa-calendar-check",
                    "Ongoing" => "fa-play-circle",
                    "Completed" => "fa-check-circle",
                    "Cancelled" => "fa-times-circle",
                    _ => "fa-calendar"
                };
            }
        }
    }
}