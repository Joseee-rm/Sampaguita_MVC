using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SeniorManagement.Models
{
    public class Senior
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "First name is required")]
        public string s_firstName { get; set; }
        public string s_middleName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        public string s_lastName { get; set; }

        [Required(ErrorMessage = "Sex is required")]
        public string s_sex { get; set; }

        [Required(ErrorMessage = "Date of birth is required")]
        public DateTime s_dob { get; set; }
        public int s_age { get; set; }

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        public string s_contact { get; set; }
        public string s_barangay { get; set; }
        public string s_guardian_zone { get; set; }
        public string s_religion { get; set; }
        public string s_bloodtype { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Health Information
        public string s_health_problems_option { get; set; } = "No";
        public string s_health_problems { get; set; }
        public string s_maintenance_option { get; set; } = "No";
        public string s_maintenance { get; set; }
        public string s_disability_option { get; set; } = "No";
        public string s_disability { get; set; }
        public string s_visual_option { get; set; } = "No";
        public string s_visual { get; set; }
        public string s_hearing_option { get; set; } = "No";
        public string s_hearing { get; set; }
        public string s_emotional_option { get; set; } = "No";
        public string s_emotional { get; set; }

        // Family Information
        public string s_spouse { get; set; }
        public int? s_spouse_age { get; set; }
        public string s_spouse_occupation { get; set; }
        public string s_spouse_contact { get; set; }
        public string s_children { get; set; }
        public List<Child> ChildrenList { get; set; } = new List<Child>(); // For multiple children

        // Guardian/Emergency Contact
        public string s_guardian_name { get; set; }
        public string s_guardian_relationship { get; set; }
        public string s_guardian_relationship_other { get; set; }
        public string s_guardian_contact { get; set; }
        public string s_guardian_address { get; set; }

        // Maintenance Medicines
        public List<MaintenanceMedicine> MaintenanceMedicines { get; set; } = new List<MaintenanceMedicine>();

        public string FullName => $"{s_firstName} {s_lastName}".Trim();
    }

    public class MaintenanceMedicine
    {
        public int Id { get; set; }
        public int SeniorId { get; set; }
        public string MedicineName { get; set; }
        public string Dosage { get; set; }
        public string Schedule { get; set; }
        public string Instructions { get; set; }
    }

    public class Child
    {
        public int Id { get; set; }
        public int SeniorId { get; set; }
        public string Name { get; set; }
        public int? Age { get; set; }
        public string Relationship { get; set; }
    }
}