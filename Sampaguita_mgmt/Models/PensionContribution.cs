namespace SeniorManagement.Models
{
    public class PensionContribution
    {
        public int Id { get; set; }
        public int SeniorId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public bool IsClaimed { get; set; }
        public DateTime? ClaimedDate { get; set; }
        public DateTime CreatedAt { get; set; }

        // Joined fields from Senior Table
        public string FullName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleInitial { get; set; }
        public int Zone { get; set; }
        public string Status { get; set; }
        public string PensionType { get; set; }
        public int Age { get; set; }

        // Helper properties for frequency
        public string DisplayPensionType =>
            string.IsNullOrEmpty(PensionType) ? "No Pension" : PensionType;

        public bool HasPension => !string.IsNullOrEmpty(PensionType) && PensionType != "No Pension";

        public int ExpectedClaimsPerYear => GetExpectedClaimsPerYear();
        public bool IsMonthlyPension => HasPension && ExpectedClaimsPerYear == 12;
        public bool IsFlexiblePension => HasPension && ExpectedClaimsPerYear == 0;
        public bool RequiresRMD => Age >= 73 && HasPension && (PensionType == "Defined Contribution Plan (401k/403b)" ||
                                               PensionType == "IRA (Traditional/Roth)");

        private int GetExpectedClaimsPerYear()
        {
            if (!HasPension) return 0;

            return PensionType switch
            {
                "Social Security" => 12,
                "Defined Benefit Plan" => 12,
                "Annuity" => 12,
                "Government/Military Pension" => 12,
                "Defined Contribution Plan (401k/403b)" => 0, // Flexible
                "IRA (Traditional/Roth)" => 0, // Flexible
                _ => 0 // Other pension types
            };
        }
    }
}