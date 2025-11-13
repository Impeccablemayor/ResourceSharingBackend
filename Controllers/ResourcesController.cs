using AcademicResourceApp.Data;
using AcademicResourceApp.DTOs;
using AcademicResourceApp.Models;
using AcademicResourceApp.Services;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AcademicResourceApp.Controllers
{
    [ApiController]
    [Route("api/resources")]
    public class ResourcesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly CloudinaryDotNet.Cloudinary _cloudinary;
        private readonly NotificationService _notificationService;

        public ResourcesController(
            AppDbContext context,
            CloudinaryDotNet.Cloudinary cloudinary,
            NotificationService notificationService)
        {
            _context = context;
            _cloudinary = cloudinary;
            _notificationService = notificationService;
        }

        [HttpPost("upload")]
        //[AllowAnonymous]
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
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userId, out var parsedId))
            {
                uploaderId = parsedId;
            }

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

            // This will notify the user of the file upload
            if (uploaderId.HasValue)
            {
                await _notificationService.NotifyAsync(uploaderId.Value, $"Your resource '{resource.Title}' was uploaded successfully.");
            }

            return Ok(new { message = "Resource uploaded successfully!", url = resource.FileUrl });
        }

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

        // GET: api/resources/{id}
        [HttpGet("{id}")]
        //[Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var resource = await _context.Resources
                .Include(r => r.UploadedBy)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (resource == null)
                return NotFound();

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Guid? currentUserId = null;
            if (Guid.TryParse(userId, out var parsedId))
                currentUserId = parsedId;

            // Check if current user is uploader
            bool isUploader = resource.UploadedById == currentUserId;

            // Check if current user is the approved borrower
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


        [HttpGet("notifications")]
        [AllowAnonymous]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var notifications = await _context.Notifications
                .Where(n => n.UserId.ToString() == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            return Ok(notifications);
        }

        // POST: api/resources/{resourceId}/borrow
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

            // Prevent duplicate pending requests for the same requests
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
                await _notificationService.NotifyAsync(resource.UploadedById.Value, "You have a new borrow request for your hardcover resource.");

            // Notify borrower (the user making the request)
            await _notificationService.NotifyAsync(borrowerId, "Your borrow request has been submitted and is awaiting approval.");

            return Ok(new { message = "Borrow request submitted." });
        }

        // POST: api/resources/borrow/{transactionId}/approve
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

            // Approve this transaction
            transaction.Status = BorrowStatus.Approved;

            // Reject all other pending requests for this resource
            var otherPending = await _context.BorrowTransactions
                .Where(bt => bt.ResourceId == transaction.ResourceId && bt.Id != transactionId && bt.Status == BorrowStatus.Pending)
                .ToListAsync();

            foreach (var bt in otherPending)
                bt.Status = BorrowStatus.Rejected;

            await _context.SaveChangesAsync();

            // Notify the borrower
            await _notificationService.NotifyAsync(transaction.BorrowerId, "Your borrow request was approved!");

            // Notify rejected users
            foreach (var bt in otherPending)
                await _notificationService.NotifyAsync(bt.BorrowerId, "Your borrow request was rejected.");

            return Ok(new { message = "Borrow request approved." });
        }

        // GET: api/resources/{resourceId}/borrow-requests
        [HttpGet("{resourceId}/borrow-requests")]
        [Authorize]
        public async Task<IActionResult> GetBorrowRequestsForResource(int resourceId)
        {
            var resource = await _context.Resources
                .FirstOrDefaultAsync(r => r.Id == resourceId);

            if (resource == null)
                return NotFound();

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
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
