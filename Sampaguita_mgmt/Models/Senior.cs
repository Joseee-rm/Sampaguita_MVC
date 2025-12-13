using System;
using System.ComponentModel.DataAnnotations;

namespace SeniorManagement.Models
{
    public class Senior
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "SCCN number is required")]
        [Display(Name = "OSCA Number")]
        [StringLength(12, MinimumLength = 12, ErrorMessage = "OSCA number must be exactly 12 digits")]
        [RegularExpression(@"^\d{12}$", ErrorMessage = "OSCA number must contain only numbers (0-9)")]
        public string SeniorId { get; set; }

        [Display(Name = "NCSC Registration Number (Optional)")]
        [StringLength(50, ErrorMessage = "NCSC Registration Number cannot exceed 50 characters")]
        public string NCSRegistrationNumber { get; set; }

        [Required(ErrorMessage = "First Name is required")]
        [Display(Name = "First Name")]
        [StringLength(100, ErrorMessage = "First Name cannot exceed 100 characters")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last Name is required")]
        [Display(Name = "Last Name")]
        [StringLength(100, ErrorMessage = "Last Name cannot exceed 100 characters")]
        public string LastName { get; set; }

        [Display(Name = "Middle Name")]
        [StringLength(100, ErrorMessage = "Middle Name cannot exceed 100 characters")]
        public string MiddleInitial { get; set; }

        [Display(Name = "Extension")]
        [StringLength(10, ErrorMessage = "Extension cannot exceed 10 characters")]
        public string Extension { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [Display(Name = "Gender")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Age is required")]
        [Range(60, 120, ErrorMessage = "Age must be between 60 and 120")]
        public int Age { get; set; }

        [Display(Name = "Birth Date")]
        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [Display(Name = "Citizenship")]
        [StringLength(50, ErrorMessage = "Citizenship cannot exceed 50 characters")]
        public string Citizenship { get; set; } = "Filipino";

        [Display(Name = "Dual Citizenship Country")]
        [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters")]
        public string DualCitizenshipCountry { get; set; }

        [Display(Name = "Contact Number")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Contact Number cannot exceed 20 characters")]
        public string ContactNumber { get; set; }

        [Display(Name = "Email Address")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Zone is required")]
        [Range(1, 7, ErrorMessage = "Zone must be between 1 and 7")]
        public int Zone { get; set; }

        [Display(Name = "Civil Status")]
        public string CivilStatus { get; set; }

        [Display(Name = "Has Pension?")]
        public bool HasPension { get; set; }

        [Display(Name = "Pension Type")]
        [StringLength(50, ErrorMessage = "Pension Type cannot exceed 50 characters")]
        public string PensionType { get; set; }

        [Display(Name = "Other Pension Type")]
        [StringLength(100, ErrorMessage = "Other Pension Type cannot exceed 100 characters")]
        public string PensionOther { get; set; }

        // Address Information
        [Display(Name = "House Number")]
        [StringLength(50, ErrorMessage = "House Number cannot exceed 50 characters")]
        public string HouseNumber { get; set; }

        public string Barangay { get; set; } = "Sampaguita";

        [Display(Name = "City/Municipality")]
        [StringLength(100, ErrorMessage = "City/Municipality cannot exceed 100 characters")]
        public string CityMunicipality { get; set; } = "Naujan";

        [Display(Name = "Province")]
        [StringLength(100, ErrorMessage = "Province cannot exceed 100 characters")]
        public string Province { get; set; } = "Oriental Mindoro";

        [Display(Name = "Zip Code")]
        [StringLength(10, ErrorMessage = "Zip Code cannot exceed 10 characters")]
        public string ZipCode { get; set; } = "5204";

        // Family Information
        [Display(Name = "Spouse First Name")]
        [StringLength(100, ErrorMessage = "Spouse First Name cannot exceed 100 characters")]
        public string SpouseFirstName { get; set; }

        [Display(Name = "Spouse Last Name")]
        [StringLength(100, ErrorMessage = "Spouse Last Name cannot exceed 100 characters")]
        public string SpouseLastName { get; set; }

        [Display(Name = "Spouse Middle Name")]
        [StringLength(100, ErrorMessage = "Spouse Middle Name cannot exceed 100 characters")]
        public string SpouseMiddleName { get; set; }

        [Display(Name = "Spouse Extension")]
        [StringLength(10, ErrorMessage = "Spouse Extension cannot exceed 10 characters")]
        public string SpouseExtension { get; set; }

        [Display(Name = "Spouse Citizenship")]
        [StringLength(50, ErrorMessage = "Spouse Citizenship cannot exceed 50 characters")]
        public string SpouseCitizenship { get; set; }

        [Display(Name = "Spouse Dual Citizenship Country")]
        [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters")]
        public string SpouseDualCitizenshipCountry { get; set; }

        // Children Information (separate fields for first child)
        [Display(Name = "Child First Name")]
        [StringLength(100, ErrorMessage = "Child First Name cannot exceed 100 characters")]
        public string ChildFirstName { get; set; }

        [Display(Name = "Child Last Name")]
        [StringLength(100, ErrorMessage = "Child Last Name cannot exceed 100 characters")]
        public string ChildLastName { get; set; }

        [Display(Name = "Child Middle Name")]
        [StringLength(100, ErrorMessage = "Child Middle Name cannot exceed 100 characters")]
        public string ChildMiddleName { get; set; }

        [Display(Name = "Child Extension")]
        [StringLength(10, ErrorMessage = "Child Extension cannot exceed 10 characters")]
        public string ChildExtension { get; set; }

        [Display(Name = "Additional Children Info")]
        public string ChildrenInfo { get; set; }

        // Authorized Representative (separate fields)
        [Display(Name = "Authorized Rep First Name")]
        [StringLength(100, ErrorMessage = "First Name cannot exceed 100 characters")]
        public string AuthorizedRepFirstName { get; set; }

        [Display(Name = "Authorized Rep Last Name")]
        [StringLength(100, ErrorMessage = "Last Name cannot exceed 100 characters")]
        public string AuthorizedRepLastName { get; set; }

        [Display(Name = "Authorized Rep Middle Name")]
        [StringLength(100, ErrorMessage = "Middle Name cannot exceed 100 characters")]
        public string AuthorizedRepMiddleName { get; set; }

        [Display(Name = "Authorized Rep Extension")]
        [StringLength(10, ErrorMessage = "Extension cannot exceed 10 characters")]
        public string AuthorizedRepExtension { get; set; }

        [Display(Name = "Authorized Rep Relationship")]
        [StringLength(50, ErrorMessage = "Relationship cannot exceed 50 characters")]
        public string AuthorizedRepRelationship { get; set; }

        [Display(Name = "Additional Authorized Rep Info")]
        public string AuthorizedRepInfo { get; set; }

        // Designated Beneficiary Information
        [Display(Name = "Primary Beneficiary First Name")]
        [StringLength(100, ErrorMessage = "Primary Beneficiary First Name cannot exceed 100 characters")]
        public string PrimaryBeneficiaryFirstName { get; set; }

        [Display(Name = "Primary Beneficiary Last Name")]
        [StringLength(100, ErrorMessage = "Primary Beneficiary Last Name cannot exceed 100 characters")]
        public string PrimaryBeneficiaryLastName { get; set; }

        [Display(Name = "Primary Beneficiary Middle Name")]
        [StringLength(100, ErrorMessage = "Primary Beneficiary Middle Name cannot exceed 100 characters")]
        public string PrimaryBeneficiaryMiddleName { get; set; }

        [Display(Name = "Primary Beneficiary Extension")]
        [StringLength(10, ErrorMessage = "Primary Beneficiary Extension cannot exceed 10 characters")]
        public string PrimaryBeneficiaryExtension { get; set; }

        [Display(Name = "Primary Beneficiary Relationship")]
        [StringLength(50, ErrorMessage = "Primary Beneficiary Relationship cannot exceed 50 characters")]
        public string PrimaryBeneficiaryRelationship { get; set; }

        [Display(Name = "Contingent Beneficiary First Name")]
        [StringLength(100, ErrorMessage = "Contingent Beneficiary First Name cannot exceed 100 characters")]
        public string ContingentBeneficiaryFirstName { get; set; }

        [Display(Name = "Contingent Beneficiary Last Name")]
        [StringLength(100, ErrorMessage = "Contingent Beneficiary Last Name cannot exceed 100 characters")]
        public string ContingentBeneficiaryLastName { get; set; }

        [Display(Name = "Contingent Beneficiary Middle Name")]
        [StringLength(100, ErrorMessage = "Contingent Beneficiary Middle Name cannot exceed 100 characters")]
        public string ContingentBeneficiaryMiddleName { get; set; }

        [Display(Name = "Contingent Beneficiary Extension")]
        [StringLength(10, ErrorMessage = "Contingent Beneficiary Extension cannot exceed 10 characters")]
        public string ContingentBeneficiaryExtension { get; set; }

        [Display(Name = "Contingent Beneficiary Relationship")]
        [StringLength(50, ErrorMessage = "Contingent Beneficiary Relationship cannot exceed 50 characters")]
        public string ContingentBeneficiaryRelationship { get; set; }

        public byte[] ProfilePicture { get; set; }
        public string ProfilePictureContentType { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Computed properties
        public string FullName => $"{FirstName} {LastName}";

        public string CompleteName
        {
            get
            {
                var name = $"{FirstName} {LastName}".Trim();
                if (!string.IsNullOrEmpty(MiddleInitial))
                {
                    name = $"{FirstName} {MiddleInitial} {LastName}".Trim();
                }
                if (!string.IsNullOrEmpty(Extension))
                {
                    name = $"{name} {Extension}";
                }
                return name;
            }
        }

        public string FormattedSCCN
        {
            get
            {
                if (string.IsNullOrEmpty(SeniorId) || SeniorId.Length != 12)
                    return SeniorId;

                return $"{SeniorId.Substring(0, 3)}-{SeniorId.Substring(3, 3)}-{SeniorId.Substring(6, 3)}-{SeniorId.Substring(9, 3)}";
            }
        }

        public string ProfilePicturePath { get; set; }

        public string ProfilePictureUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(ProfilePicturePath))
                {
                    return $"/uploads/profiles/{ProfilePicturePath}";
                }
                return "/images/default-profile.png";
            }
        }

        // Updated DisplayPensionType to include "HasPension" logic
        public string DisplayPensionType
        {
            get
            {
                if (!HasPension) return "None";
                if (!string.IsNullOrEmpty(PensionOther)) return PensionOther;
                if (!string.IsNullOrEmpty(PensionType)) return PensionType;
                return "None";
            }
        }

        public string PermanentAddress
        {
            get
            {
                var addressParts = new System.Text.StringBuilder();

                if (!string.IsNullOrEmpty(HouseNumber))
                {
                    addressParts.Append(HouseNumber).Append(", ");
                }

                addressParts.Append($"Zone {Zone}, ");
                addressParts.Append(Barangay).Append(", ");
                addressParts.Append(CityMunicipality).Append(", ");
                addressParts.Append(Province).Append(", ");
                addressParts.Append(ZipCode);

                return addressParts.ToString();
            }
        }

        public bool IsEligibleForOctogenarian => Age >= 80 && Age < 90;
        public bool IsEligibleForNonagenarian => Age >= 90 && Age < 100;
        public bool IsEligibleForCentenarian => Age >= 100;

        public string SpecialBenefitsEligibility
        {
            get
            {
                if (Age >= 100) return "Centenarian Benefit Program";
                if (Age >= 90) return "Nonagenarian Benefit Program";
                if (Age >= 80) return "Octogenarian Benefit Program";
                return "Not eligible for special benefits";
            }
        }

        public string SpouseFullName
        {
            get
            {
                if (string.IsNullOrEmpty(SpouseFirstName) && string.IsNullOrEmpty(SpouseLastName))
                    return string.Empty;

                var name = $"{SpouseFirstName} {SpouseLastName}".Trim();
                if (!string.IsNullOrEmpty(SpouseMiddleName))
                {
                    name = $"{SpouseFirstName} {SpouseMiddleName} {SpouseLastName}".Trim();
                }
                if (!string.IsNullOrEmpty(SpouseExtension))
                {
                    name = $"{name} {SpouseExtension}";
                }
                return name;
            }
        }

        public string ChildFullName
        {
            get
            {
                if (string.IsNullOrEmpty(ChildFirstName) && string.IsNullOrEmpty(ChildLastName))
                    return string.Empty;

                var name = $"{ChildFirstName} {ChildLastName}".Trim();
                if (!string.IsNullOrEmpty(ChildMiddleName))
                {
                    name = $"{ChildFirstName} {ChildMiddleName} {ChildLastName}".Trim();
                }
                if (!string.IsNullOrEmpty(ChildExtension))
                {
                    name = $"{name} {ChildExtension}";
                }
                return name;
            }
        }

        public string AuthorizedRepFullName
        {
            get
            {
                if (string.IsNullOrEmpty(AuthorizedRepFirstName) && string.IsNullOrEmpty(AuthorizedRepLastName))
                    return string.Empty;

                var name = $"{AuthorizedRepFirstName} {AuthorizedRepLastName}".Trim();
                if (!string.IsNullOrEmpty(AuthorizedRepMiddleName))
                {
                    name = $"{AuthorizedRepFirstName} {AuthorizedRepMiddleName} {AuthorizedRepLastName}".Trim();
                }
                if (!string.IsNullOrEmpty(AuthorizedRepExtension))
                {
                    name = $"{name} {AuthorizedRepExtension}";
                }
                return name;
            }
        }

        public string PrimaryBeneficiaryFullName
        {
            get
            {
                if (string.IsNullOrEmpty(PrimaryBeneficiaryFirstName) && string.IsNullOrEmpty(PrimaryBeneficiaryLastName))
                    return string.Empty;

                var name = $"{PrimaryBeneficiaryFirstName} {PrimaryBeneficiaryLastName}".Trim();
                if (!string.IsNullOrEmpty(PrimaryBeneficiaryMiddleName))
                {
                    name = $"{PrimaryBeneficiaryFirstName} {PrimaryBeneficiaryMiddleName} {PrimaryBeneficiaryLastName}".Trim();
                }
                if (!string.IsNullOrEmpty(PrimaryBeneficiaryExtension))
                {
                    name = $"{name} {PrimaryBeneficiaryExtension}";
                }
                return name;
            }
        }

        public string ContingentBeneficiaryFullName
        {
            get
            {
                if (string.IsNullOrEmpty(ContingentBeneficiaryFirstName) && string.IsNullOrEmpty(ContingentBeneficiaryLastName))
                    return string.Empty;

                var name = $"{ContingentBeneficiaryFirstName} {ContingentBeneficiaryLastName}".Trim();
                if (!string.IsNullOrEmpty(ContingentBeneficiaryMiddleName))
                {
                    name = $"{ContingentBeneficiaryFirstName} {ContingentBeneficiaryMiddleName} {ContingentBeneficiaryLastName}".Trim();
                }
                if (!string.IsNullOrEmpty(ContingentBeneficiaryExtension))
                {
                    name = $"{name} {ContingentBeneficiaryExtension}";
                }
                return name;
            }
        }
    }
}