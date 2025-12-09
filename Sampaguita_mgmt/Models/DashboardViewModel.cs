// Models/DashboardViewModel.cs
namespace SeniorManagement.Models
{
    public class DashboardViewModel
    {
        // Senior Statistics
        public SeniorStats SeniorStats { get; set; } = new SeniorStats();

        // Event Statistics
        public EventStats EventStats { get; set; } = new EventStats();

        // Recent Activities
        public List<ActivityLog> RecentActivities { get; set; } = new List<ActivityLog>();
    }

    public class SeniorStats
    {
        public int TotalSeniors { get; set; }
        public int ActiveSeniors { get; set; }
        public int ArchivedSeniors { get; set; }

        // Age Groups
        public int Age60_69 { get; set; }
        public int Age70_79 { get; set; }
        public int Age80_89 { get; set; }
        public int Age90plus { get; set; }

        // Gender Distribution
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }

        // Zone Distribution
        public Dictionary<int, int> ZoneDistribution { get; set; } = new Dictionary<int, int>();

        // Civil Status
        public int CivilStatusSingle { get; set; }
        public int CivilStatusMarried { get; set; }
        public int CivilStatusWidowed { get; set; }
        public int CivilStatusSeparated { get; set; }
        public int CivilStatusDivorced { get; set; }

        // Contact Information
        public int WithContact { get; set; }
        public int WithoutContact { get; set; }


        public int RecentRegistrations { get; set; } // Add this linw
    }


    public class EventStats
    {
        public int TotalEvents { get; set; }
        public int UpcomingEvents { get; set; }
        public int TodayEvents { get; set; }
        public int ScheduledEvents { get; set; }
        public int OngoingEvents { get; set; }
        public int CompletedEvents { get; set; }
        public int CancelledEvents { get; set; }

        // Event Types
        public int MedicalCount { get; set; }
        public int AssistanceCount { get; set; }
        public int CommunityCount { get; set; }
        public int WellnessCount { get; set; }
        public int EducationalCount { get; set; }
        public int SocialCount { get; set; }

        // Attendance
        public int TotalAttendance { get; set; }
        public int TotalCapacity { get; set; }

    }
}