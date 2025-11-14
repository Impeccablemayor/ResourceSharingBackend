using CloudinaryDotNet.Actions;

namespace AcademicResourceApp.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } = "student";
        public string? SchoolEmail { get; set; }

        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationCode { get; set; } // 6-digit OTP
        public DateTime? CodeExpiry { get; set; }

        public bool IsInstitutionVerified { get; set; } = false;
        public bool IsActive { get; set; } = false;

        public List<Resource> Resources { get; set; } 
        public List<BorrowTransaction> BorrowedResources { get; set; } 

    }
}
