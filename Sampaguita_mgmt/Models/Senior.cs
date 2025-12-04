using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SeniorManagement.Models
{
    public class Senior
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        public string s_firstName { get; set; }

        [Display(Name = "Middle Name")]
        [StringLength(50, ErrorMessage = "Middle name cannot exceed 50 characters")]
        public string s_middleName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        public string s_lastName { get; set; }

        [Required(ErrorMessage = "Sex is required")]
        [Display(Name = "Sex")]
        public string s_sex { get; set; }

        [Required(ErrorMessage = "Date of birth is required")]
        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime s_dob { get; set; }

        [Display(Name = "Age")]
        public int s_age { get; set; }

        [Display(Name = "Contact Number")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Contact number cannot exceed 20 characters")]
        public string s_contact { get; set; }

        [Display(Name = "Barangay")]
        [StringLength(100, ErrorMessage = "Barangay cannot exceed 100 characters")]
        public string s_barangay { get; set; }

        [Display(Name = "Guardian Zone/Street")]
        [StringLength(100, ErrorMessage = "Guardian zone cannot exceed 100 characters")]
        public string s_guardian_zone { get; set; }

        [Display(Name = "Religion")]
        [StringLength(50, ErrorMessage = "Religion cannot exceed 50 characters")]
        public string s_religion { get; set; }

        [Display(Name = "Blood Type")]
        public string s_bloodtype { get; set; }

        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Health Information
        [Display(Name = "Health Problems Option")]
        public string s_health_problems_option { get; set; } = "No";

        [Display(Name = "Health Problems")]
        public string s_health_problems { get; set; }

        [Display(Name = "Maintenance Option")]
        public string s_maintenance_option { get; set; } = "No";

        [Display(Name = "Maintenance")]
        public string s_maintenance { get; set; }

        [Display(Name = "Disability Option")]
        public string s_disability_option { get; set; } = "No";

        [Display(Name = "Disability")]
        public string s_disability { get; set; }

        [Display(Name = "Visual Option")]
        public string s_visual_option { get; set; } = "No";

        [Display(Name = "Visual Issues")]
        public string s_visual { get; set; }

        [Display(Name = "Hearing Option")]
        public string s_hearing_option { get; set; } = "No";

        [Display(Name = "Hearing Issues")]
        public string s_hearing { get; set; }

        [Display(Name = "Emotional Option")]
        public string s_emotional_option { get; set; } = "No";

        [Display(Name = "Emotional Conditions")]
        public string s_emotional { get; set; }

        // Family Information
        [Display(Name = "Spouse Name")]
        [StringLength(100, ErrorMessage = "Spouse name cannot exceed 100 characters")]
        public string s_spouse { get; set; }

        [Display(Name = "Spouse Age")]
        [Range(0, 120, ErrorMessage = "Spouse age must be between 0 and 120")]
        public int? s_spouse_age { get; set; }

        [Display(Name = "Spouse Occupation")]
        [StringLength(100, ErrorMessage = "Spouse occupation cannot exceed 100 characters")]
        public string s_spouse_occupation { get; set; }

        [Display(Name = "Spouse Contact")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Spouse contact cannot exceed 20 characters")]
        public string s_spouse_contact { get; set; }

        [Display(Name = "Children")]
        public string s_children { get; set; }

        [Display(Name = "Children List")]
        public List<Child> ChildrenList { get; set; } = new List<Child>();

        // Guardian/Emergency Contact
        [Display(Name = "Guardian Name")]
        [StringLength(100, ErrorMessage = "Guardian name cannot exceed 100 characters")]
        public string s_guardian_name { get; set; }

        [Display(Name = "Guardian Relationship")]
        [StringLength(50, ErrorMessage = "Guardian relationship cannot exceed 50 characters")]
        public string s_guardian_relationship { get; set; }

        [Display(Name = "Guardian Relationship (Other)")]
        [StringLength(50, ErrorMessage = "Guardian relationship cannot exceed 50 characters")]
        public string s_guardian_relationship_other { get; set; }

        [Display(Name = "Guardian Contact")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Guardian contact cannot exceed 20 characters")]
        public string s_guardian_contact { get; set; }

        [Display(Name = "Guardian Address")]
        public string s_guardian_address { get; set; }

        // Maintenance Medicines
        public List<MaintenanceMedicine> MaintenanceMedicines { get; set; } = new List<MaintenanceMedicine>();

        public string FullName => $"{s_firstName} {s_lastName}".Trim();
    }

    public class MaintenanceMedicine
    {
        public int Id { get; set; }
        public int SeniorId { get; set; }

        [Display(Name = "Medicine Name")]
        [StringLength(255, ErrorMessage = "Medicine name cannot exceed 255 characters")]
        public string MedicineName { get; set; }

        [Display(Name = "Dosage")]
        [StringLength(100, ErrorMessage = "Dosage cannot exceed 100 characters")]
        public string Dosage { get; set; }

        [Display(Name = "Schedule")]
        [StringLength(100, ErrorMessage = "Schedule cannot exceed 100 characters")]
        public string Schedule { get; set; }

        [Display(Name = "Instructions")]
        public string Instructions { get; set; }
    }

    public class Child
    {
        public int Id { get; set; }
        public int SeniorId { get; set; }

        [Display(Name = "Name")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; }

        [Display(Name = "Age")]
        [Range(0, 120, ErrorMessage = "Age must be between 0 and 120")]
        public int? Age { get; set; }

        [Display(Name = "Relationship")]
        [StringLength(50, ErrorMessage = "Relationship cannot exceed 50 characters")]
        public string Relationship { get; set; }
    }
}