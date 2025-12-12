using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SeniorManagement.Models
{
    public class ReportViewModel
    {
        // Senior Report Data
        public int TotalSeniors { get; set; }
        public List<Senior> SeniorList { get; set; }
        public DateTime ReportDate { get; set; }

        // Senior Filters
        public string SelectedStatus { get; set; }
        public string SelectedZoneFilter { get; set; }
        public string SelectedGender { get; set; }
        public string SelectedCivilStatus { get; set; }
        public string SelectedAgeRange { get; set; }
        public string SelectedMonthYear { get; set; }
        public string SelectedPensionType { get; set; }
        public string SeniorSearchTerm { get; set; }
        public List<string> AvailableMonthsYears { get; set; }
        public List<string> AvailablePensionTypes { get; set; }

        // Event Report Data
        public int TotalEvents { get; set; }
        public List<EventReportItem> EventList { get; set; }

        // Event Filters
        public string SelectedEventStatus { get; set; }
        public string SelectedEventType { get; set; }
        public string SelectedEventDateFilter { get; set; }
        public string EventSearchTerm { get; set; }
        public string SelectedFromDate { get; set; }
        public string SelectedToDate { get; set; }

        // Constructor
        public ReportViewModel()
        {
            SeniorList = new List<Senior>();
            EventList = new List<EventReportItem>();
            AvailableMonthsYears = new List<string>();
            AvailablePensionTypes = new List<string>();
        }
    }

    public class EventReportItem
    {
        public int Id { get; set; }

        [Display(Name = "Event Title")]
        public string EventTitle { get; set; }

        [Display(Name = "Event Type")]
        public string EventType { get; set; }

        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime EventDate { get; set; }

        [Display(Name = "Time")]
        public TimeSpan EventTime { get; set; }

        [Display(Name = "Location")]
        public string EventLocation { get; set; }

        [Display(Name = "Organized By")]
        public string OrganizedBy { get; set; }

        [Display(Name = "Max Capacity")]
        public int? MaxCapacity { get; set; }

        [Display(Name = "Attendance")]
        public int AttendanceCount { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        // Attendance Properties
        [Display(Name = "Attendance %")]
        public string AttendancePercentage
        {
            get
            {
                if (MaxCapacity.HasValue && MaxCapacity.Value > 0)
                {
                    double percentage = (double)AttendanceCount / MaxCapacity.Value * 100;
                    return $"{percentage:F1}%";
                }
                return "N/A";
            }
        }

        [Display(Name = "Available Slots")]
        public int AvailableSlots
        {
            get
            {
                if (MaxCapacity.HasValue)
                    return Math.Max(0, MaxCapacity.Value - AttendanceCount);
                return -1; // Unlimited
            }
        }

        [Display(Name = "Is Full")]
        public bool IsFull
        {
            get
            {
                if (MaxCapacity.HasValue)
                    return AttendanceCount >= MaxCapacity.Value;
                return false;
            }
        }

        [Display(Name = "Attendance Status")]
        public string AttendanceStatus
        {
            get
            {
                if (!MaxCapacity.HasValue || MaxCapacity.Value <= 0)
                    return "No Limit";

                if (AttendanceCount >= MaxCapacity.Value)
                    return "FULL";

                double percentage = (double)AttendanceCount / MaxCapacity.Value * 100;

                if (percentage >= 90)
                    return "Almost Full";
                else if (percentage >= 70)
                    return "Good";
                else if (percentage >= 50)
                    return "Moderate";
                else if (percentage >= 25)
                    return "Low";
                else
                    return "Very Low";
            }
        }

        [Display(Name = "Date & Time")]
        public string EventDateTime
        {
            get
            {
                var dateTime = EventDate.Add(EventTime);
                return dateTime.ToString("MMM dd, yyyy h:mm tt");
            }
        }
    }
}