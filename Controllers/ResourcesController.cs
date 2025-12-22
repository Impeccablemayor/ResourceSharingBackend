using AcademicResourceApp.Data;
using AcademicResourceApp.DTOs;
using AcademicResourceApp.Models;
using AcademicResourceApp.Services;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace AcademicResourceApp.Controllers
{
    [ApiController]
    [Route("api/resources")]
    public class ResourcesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly NotificationService _notificationService;

        public ResourcesController(
            AppDbContext context,
            Cloudinary cloudinary,
            NotificationService notificationService)
        {
            _context = context;
            _cloudinary = cloudinary;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Upload a new resource to the platform.
        /// </summary>
        /// <param name="dto">Resource upload data (file, metadata, optional image)</param>
        /// <returns>Success message and uploaded file URL</returns>
        /// <response code="200">Resource uploaded successfully</response>
        /// <response code="400">Invalid file or missing data</response>
        /// <response code="500">Cloudinary upload failed</response>
        [HttpPost("upload")]
        //[Authorize]
        public async Task<IActionResult> Upload([FromForm] UploadResourceDto dto)
        {
            if (dto.File == null || dto.File.Length == 0)
                return BadRequest("No file uploaded.");

            // Upload main file to Cloudinary
            using var stream = dto.File.OpenReadStream();
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(dto.File.FileName, stream)
            };
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                return StatusCode(500, $"Cloudinary upload failed: {uploadResult.Error?.Message}");

            // Get uploaderId from claims
            Guid? uploaderId = null;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userId, out var parsedId))
                uploaderId = parsedId;

            // Upload hardcover image if applicable
            string? imageUrl = null;
            if (dto.Type == "Hardcover" && dto.Image != null && dto.Image.Length > 0)
            {
                using var imageStream = dto.Image.OpenReadStream();
                var imageUploadParams = new ImageUploadParams
                {
                    File = new FileDescription(dto.Image.FileName, imageStream)
                };
                var imageUploadResult = await _cloudinary.UploadAsync(imageUploadParams);
                if (imageUploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                    imageUrl = imageUploadResult.SecureUrl.ToString();
            }

            // Save resource metadata to DB
            var resource = new Models.Resource
            {
                Title = dto.Title,
                CourseCode = dto.CourseCode,
                Author = dto.Author,
                Format = dto.Format,
                Department = dto.Department,
                Level = dto.Level,
                Type = dto.Type,
                Description = dto.Description,
                FileUrl = uploadResult.SecureUrl.ToString(),
                UploadedAt = DateTime.UtcNow,
                UploadedById = uploaderId,
                PhysicalLocation = dto.Type == "Hardcover" ? dto.PhysicalLocation : null,
                MeetupLocation = dto.Type == "Hardcover" ? dto.MeetupLocation : null,
                ImageUrl = imageUrl
            };

            _context.Resources.Add(resource);
            await _context.SaveChangesAsync();

            // Notify uploader
            if (uploaderId.HasValue)
            {
                await _notificationService.NotifyAsync(
                    uploaderId.Value,
                    "Resource Uploaded",
                    $"Your resource '{resource.Title}' was uploaded successfully.",
                    NotificationType.Approval
                );
            }

            return Ok(new { message = "Resource uploaded successfully!", url = resource.FileUrl });
        }

        /// <summary>
        /// Get all resources.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var resources = await _context.Resources
                .Select(r => new ResourceListDto
                {
                    Id = r.Id,
                    Title = r.Title,
                    CourseCode = r.CourseCode,
                    Author = r.Author,
                    Format = r.Format,
                    Department = r.Department,
                    Level = r.Level,
                    Type = r.Type,
                    Description = r.Description,
                    FileUrl = r.FileUrl,
                    UploadedAt = r.UploadedAt,
                    ImageUrl = r.ImageUrl
                })
                .ToListAsync();

            return Ok(resources);
        }

        /// <summary>
        /// Get a resource by its ID.
        /// </summary>
        [HttpGet("{id}")]
        //[Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var resource = await _context.Resources
                .Include(r => r.UploadedBy)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (resource == null)
                return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? currentUserId = null;
            if (Guid.TryParse(userId, out var parsedId))
                currentUserId = parsedId;

            bool isUploader = resource.UploadedById == currentUserId;

            bool isApprovedBorrower = await _context.BorrowTransactions
                .AnyAsync(bt => bt.ResourceId == resource.Id && bt.BorrowerId == currentUserId && bt.Status == BorrowStatus.Approved);

            var dto = new Models.Resource
            {
                Id = resource.Id,
                Title = resource.Title,
                CourseCode = resource.CourseCode,
                Author = resource.Author,
                Format = resource.Format,
                Department = resource.Department,
                Level = resource.Level,
                Type = resource.Type,
                Description = resource.Description,
                FileUrl = resource.FileUrl,
                UploadedAt = resource.UploadedAt,
                UploadedById = resource.UploadedById,
                ImageUrl = resource.ImageUrl,
                PhysicalLocation = (isUploader || isApprovedBorrower) ? resource.PhysicalLocation : null,
                MeetupLocation = (isUploader || isApprovedBorrower) ? resource.MeetupLocation : null
            };

            return Ok(dto);
        }

        /// <summary>
        /// Get all notifications for the current user.
        /// </summary>
        [HttpGet("notifications")]
        //[Authorize]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out var parsedId))
                return Unauthorized();

            var notifications = await _notificationService.GetAllAsync(parsedId);
            return Ok(notifications);
        }

        /// <summary>
        /// Request to borrow a hardcover resource.
        /// </summary>
        [HttpPost("{resourceId}/borrow")]
        //[Authorize]
        public async Task<IActionResult> RequestBorrow(int resourceId)
        {
            var resource = await _context.Resources.FindAsync(resourceId);
            if (resource == null || resource.Type != "Hardcover")
                return BadRequest("Borrowing is only allowed for hardcover resources.");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out var borrowerId))
                return Unauthorized();

            bool alreadyRequested = await _context.BorrowTransactions
                .AnyAsync(bt => bt.ResourceId == resourceId && bt.BorrowerId == borrowerId && bt.Status == BorrowStatus.Pending);

            if (alreadyRequested)
                return BadRequest("You already have a pending borrow request for this resource.");

            var borrowTransaction = new BorrowTransaction
            {
                ResourceId = resourceId,
                BorrowerId = borrowerId,
                Status = BorrowStatus.Pending,
                RequestDate = DateTime.UtcNow
            };
            _context.BorrowTransactions.Add(borrowTransaction);
            await _context.SaveChangesAsync();

            // Notify uploader
            if (resource.UploadedById.HasValue)
            {
                await _notificationService.NotifyAsync(
                    resource.UploadedById.Value,
                    "New Borrow Request",
                    "You have a new borrow request for your hardcover resource.",
                    NotificationType.Request
                );
            }

            // Notify borrower
            await _notificationService.NotifyAsync(
                borrowerId,
                "Borrow Request Submitted",
                "Your borrow request has been submitted and is awaiting approval.",
                NotificationType.Pending
            );

            return Ok(new { message = "Borrow request submitted." });
        }

        /// <summary>
        /// Approve a borrow request.
        /// </summary>
        [HttpPost("borrow/{transactionId}/approve")]
        [Authorize]
        public async Task<IActionResult> ApproveBorrow(int transactionId)
        {
            var transaction = await _context.BorrowTransactions
                .Include(bt => bt.Resource)
                .FirstOrDefaultAsync(bt => bt.Id == transactionId);

            if (transaction == null)
                return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out var uploaderId) || transaction.Resource.UploadedById != uploaderId)
                return Forbid("Only the uploader can approve borrow requests.");

            if (transaction.Resource.Type != "Hardcover")
                return BadRequest("Only hardcover resources can be borrowed.");

            transaction.Status = BorrowStatus.Approved;

            var otherPending = await _context.BorrowTransactions
                .Where(bt => bt.ResourceId == transaction.ResourceId && bt.Id != transactionId && bt.Status == BorrowStatus.Pending)
                .ToListAsync();

            foreach (var bt in otherPending)
                bt.Status = BorrowStatus.Rejected;

            await _context.SaveChangesAsync();

            // Notify approved borrower
            await _notificationService.NotifyAsync(
                transaction.BorrowerId,
                "Borrow Request Approved",
                "Your borrow request was approved!",
                NotificationType.Approval
            );

            // Notify rejected borrowers
            foreach (var bt in otherPending)
            {
                await _notificationService.NotifyAsync(
                    bt.BorrowerId,
                    "Borrow Request Rejected",
                    "Your borrow request was rejected.",
                    NotificationType.Pending
                );
            }

            return Ok(new { message = "Borrow request approved." });
        }

        /// <summary>
        /// Get all borrow requests for a resource (uploader only).
        /// </summary>
        [HttpGet("{resourceId}/borrow-requests")]
        [Authorize]
        public async Task<IActionResult> GetBorrowRequestsForResource(int resourceId)
        {
            var resource = await _context.Resources.FirstOrDefaultAsync(r => r.Id == resourceId);
            if (resource == null)
                return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out var uploaderId) || resource.UploadedById != uploaderId)
                return Forbid("Only the uploader can view borrow requests for this resource.");

            var requests = await _context.BorrowTransactions
                .Where(bt => bt.ResourceId == resourceId)
                .Include(bt => bt.Borrower)
                .Select(bt => new BorrowRequestDto
                {
                    Id = bt.Id,
                    ResourceId = bt.ResourceId,
                    ResourceTitle = bt.Resource.Title,
                    BorrowerId = bt.BorrowerId,
                    BorrowerName = bt.Borrower.FirstName + " " + bt.Borrower.LastName,
                    BorrowerEmail = bt.Borrower.Email,
                    Status = bt.Status.ToString(),
                    RequestDate = bt.RequestDate
                })
                .OrderByDescending(bt => bt.RequestDate)
                .ToListAsync();

            return Ok(requests);
        }
    }
}
