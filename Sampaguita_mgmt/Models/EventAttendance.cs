using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SeniorManagement.Models
{
    public class EventAttendance
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Event")]
        public int EventId { get; set; }

        [Required]
        [Display(Name = "Senior ID")]
        [StringLength(12)]
        public string SeniorId { get; set; }

        [Required]
        [Display(Name = "Status")]
        [StringLength(20)]
        public string AttendanceStatus { get; set; } = "Present";

        [Display(Name = "Marked By")]
        public string MarkedBy { get; set; }

        public DateTime MarkedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("EventId")]
        public virtual Event Event { get; set; }

        [ForeignKey("SeniorId")]
        public virtual Senior Senior { get; set; }
    }
}