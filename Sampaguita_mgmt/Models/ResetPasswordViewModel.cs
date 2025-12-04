// Models/ResetPasswordViewModel.cs
namespace SeniorManagement.Models
{
    public class ResetPasswordViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Name { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }
}