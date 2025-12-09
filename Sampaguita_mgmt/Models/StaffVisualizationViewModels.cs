// Models/StaffVisualizationViewModel.cs
using System;
using System.Collections.Generic;

namespace SeniorManagement.Models
{
    public class StaffVisualizationViewModel
    {
        // Senior Statistics
        public int TotalSeniors { get; set; }
        public int ActiveSeniors { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int RecentRegistrations { get; set; }
        public int Zone1Count { get; set; }
        public int Zone2Count { get; set; }
        public int Zone3Count { get; set; }
        public int Zone4Count { get; set; }
        public int Zone5Count { get; set; }
        public int Zone6Count { get; set; }
        public int Zone7Count { get; set; }

        // Age Distribution
        public int Age60_69 { get; set; }
        public int Age70_79 { get; set; }
        public int Age80_89 { get; set; }
        public int Age90plus { get; set; }

        // Event Statistics
        public int TotalEvents { get; set; }
        public int UpcomingEvents { get; set; }
        public int TodayEvents { get; set; }
        public int MedicalEvents { get; set; }
        public int AssistanceEvents { get; set; }
        public int CommunityEvents { get; set; }
        public int WellnessEvents { get; set; }

        // Civil Status Distribution
        public int CivilStatusSingle { get; set; }
        public int CivilStatusMarried { get; set; }
        public int CivilStatusWidowed { get; set; }
        public int CivilStatusSeparated { get; set; }
        public int CivilStatusDivorced { get; set; }

        // Charts Data
        public List<GenderData> GenderDistribution { get; set; }
        public List<ZoneData> ZoneDistribution { get; set; }
        public List<AgeGroupData> AgeDistribution { get; set; }
        public List<EventTypeData> EventTypeDistribution { get; set; }
        public List<CivilStatusData> CivilStatusDistribution { get; set; }

        // Quick Lists
        public List<SeniorBasicInfo> RecentSeniors { get; set; }
        public List<EventBasicInfo> UpcomingEventsList { get; set; }

        // Staff Info
        public string StaffName { get; set; }
        public string CurrentDate { get; set; }
    }

    // Supporting Models for Charts
    public class GenderData
    {
        public string Gender { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class ZoneData
    {
        public string Zone { get; set; }
        public int Count { get; set; }
        public string Color { get; set; }
    }

    public class AgeGroupData
    {
        public string AgeGroup { get; set; }
        public int Count { get; set; }
    }

    public class EventTypeData
    {
        public string EventType { get; set; }
        public int Count { get; set; }
        public string Icon { get; set; }
    }

    public class CivilStatusData
    {
        public string Status { get; set; }
        public int Count { get; set; }
    }

    public class SeniorBasicInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public string Zone { get; set; }
        public string Status { get; set; }
        public DateTime RegisteredDate { get; set; }
    }

    public class EventBasicInfo
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string EventType { get; set; }
        public DateTime EventDate { get; set; }
        public string Location { get; set; }
        public string Status { get; set; }
    }
}