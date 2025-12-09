using System;
using System.ComponentModel.DataAnnotations;

namespace SeniorManagement.Models
{
    public class Senior
    {
        public int Id { get; set; }  // Auto-increment primary key

        [Required(ErrorMessage = "SCCN number is required")]
        [Display(Name = "SCCN Number")]
        [StringLength(12, MinimumLength = 12, ErrorMessage = "SCCN number must be exactly 12 digits")]
        [RegularExpression(@"^\d{12}$", ErrorMessage = "SCCN number must contain only numbers (0-9)")]
        public string SeniorId { get; set; }

        [Required(ErrorMessage = "First Name is required")]
        [Display(Name = "First Name")]
        [StringLength(100, ErrorMessage = "First Name cannot exceed 100 characters")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last Name is required")]
        [Display(Name = "Last Name")]
        [StringLength(100, ErrorMessage = "Last Name cannot exceed 100 characters")]
        public string LastName { get; set; }

        [Display(Name = "Middle Initial")]
        [StringLength(1, ErrorMessage = "Middle Initial must be 1 character")]
        public string MiddleInitial { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [Display(Name = "Gender")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Age is required")]
        [Range(60, 120, ErrorMessage = "Age must be between 60 and 120")]
        public int Age { get; set; }

        [Display(Name = "Birth Date")]
        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [Display(Name = "Contact Number")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Contact Number cannot exceed 20 characters")]
        public string ContactNumber { get; set; }

        [Required(ErrorMessage = "Zone is required")]
        [Range(1, 7, ErrorMessage = "Zone must be between 1 and 7")]
        public int Zone { get; set; }

        [Display(Name = "Civil Status")]
        public string CivilStatus { get; set; }

        // Fixed value as per requirement
        public string Barangay { get; set; } = "Sampaguita";

        // Status for archive/active
        public string Status { get; set; } = "Active";

        // System fields
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Computed property
        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        [Display(Name = "Complete Name")]
        public string CompleteName
        {
            get
            {
                var name = $"{FirstName} {LastName}".Trim();
                if (!string.IsNullOrEmpty(MiddleInitial))
                {
                    name = $"{FirstName} {MiddleInitial}. {LastName}".Trim();
                }
                return name;
            }
        }

        // Display SCCN with formatting
        [Display(Name = "Formatted SCCN")]
        public string FormattedSCCN
        {
            get
            {
                if (string.IsNullOrEmpty(SeniorId) || SeniorId.Length != 12)
                    return SeniorId;

                // Format as: XXX-XXX-XXX-XXX
                return $"{SeniorId.Substring(0, 3)}-{SeniorId.Substring(3, 3)}-{SeniorId.Substring(6, 3)}-{SeniorId.Substring(9, 3)}";
            }
        }
    }
}