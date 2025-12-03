using System;
using System.ComponentModel.DataAnnotations;

namespace SeniorManagement.Models
{
    public class ActivityLog
    {
        public int Id { get; set; }

        [Display(Name = "User Name")]
        public string UserName { get; set; }

        [Display(Name = "User Role")]
        public string UserRole { get; set; }

        [Display(Name = "Action")]
        public string Action { get; set; }

        [Display(Name = "Details")]
        public string Details { get; set; }

        [Display(Name = "IP Address")]
        public string IpAddress { get; set; }

        [Display(Name = "Date & Time")]
        public DateTime CreatedAt { get; set; }

        // Computed Properties
        [Display(Name = "Date & Time")]
        public string FormattedDate => CreatedAt.ToString("yyyy-MM-dd hh:mm tt");
    }
}