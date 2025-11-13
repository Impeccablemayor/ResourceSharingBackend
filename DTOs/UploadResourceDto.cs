using System.ComponentModel.DataAnnotations;

namespace AcademicResourceApp.DTOs
{
    public class UploadResourceDto : IValidatableObject
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string CourseCode { get; set; }

        [Required]
        public string Author { get; set; }

        [Required]
        public string Format { get; set; }

        [Required]
        public string Department { get; set; }

        [Required]
        public string Level { get; set; }

        [Required]
        public string Type { get; set; }

        public string? Description { get; set; }

        public IFormFile? File { get; set; }

        // Hardcover-specific fields
        public string? PhysicalLocation { get; set; }
        public string? MeetupLocation { get; set; }
        public IFormFile? Image { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Type?.ToLower() == "softcopy" && File == null)
            {
                yield return new ValidationResult(
                    "File is required for softcopy resources.",
                    new[] { nameof(File) });
            }

            if (Type?.ToLower() == "hardcover" && Image == null)
            {
                yield return new ValidationResult(
                    "Image is required for hardcover resources.",
                    new[] { nameof(Image) });
            }
        }
    }
}
