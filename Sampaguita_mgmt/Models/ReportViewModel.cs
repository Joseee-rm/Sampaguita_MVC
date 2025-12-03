using System;
using System.Collections.Generic;

namespace SeniorManagement.Models
{
    public class ReportViewModel
    {
        public int TotalSeniors { get; set; }
        public int ActiveSeniors { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public Dictionary<string, int> SeniorsByBarangay { get; set; }
        public Dictionary<string, int> SeniorsByAgeGroup { get; set; }
        public Dictionary<string, int> HealthConditions { get; set; }
        public Dictionary<string, int> Disabilities { get; set; }
        public DateTime ReportDate { get; set; }
    }

    public class AgeGroup
    {
        public string Range { get; set; }
        public int Count { get; set; }
    }
}