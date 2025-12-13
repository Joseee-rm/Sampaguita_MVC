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

    // SeniorStats.cs


    public class SeniorStats
    {
        public int TotalSeniors { get; set; }
        public int ActiveSeniors { get; set; }
        public int ArchivedSeniors { get; set; }

        public int Age60_69 { get; set; }
        public int Age70_79 { get; set; }
        public int Age80_89 { get; set; }
        public int Age90plus { get; set; }

        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }

        // Zone distribution as Dictionary for dynamic zones
        public Dictionary<int, int> ZoneDistribution { get; set; } = new Dictionary<int, int>();

        // For backward compatibility (if needed in other views)
        public int[] ZoneDistributionArray => GetZoneDistributionArray();

        private int[] GetZoneDistributionArray()
        {
            int[] array = new int[20]; // Support up to zone 20
            foreach (var kvp in ZoneDistribution)
            {
                if (kvp.Key >= 1 && kvp.Key < array.Length)
                {
                    array[kvp.Key] = kvp.Value;
                }
            }
            return array;
        }

        public int CivilStatusSingle { get; set; }
        public int CivilStatusMarried { get; set; }
        public int CivilStatusWidowed { get; set; }
        public int CivilStatusSeparated { get; set; }
        public int CivilStatusDivorced { get; set; }

        public int WithContact { get; set; }
        public int WithoutContact { get; set; }

        public int RecentRegistrations { get; set; }
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
