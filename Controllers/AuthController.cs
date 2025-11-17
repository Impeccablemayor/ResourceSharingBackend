using AcademicResourceApp.Data;
using AcademicResourceApp.DTOs;
using AcademicResourceApp.Helpers;
using AcademicResourceApp.Models;
using AcademicResourceApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcademicResourceApp.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AuthService _authService;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, AuthService authService, IConfiguration config)
        {
            _context = context;
            _authService = authService;
            _config = config;
        }

        // Test the email
        [HttpGet("test-email")]
        public async Task<IActionResult> TestEmail([FromServices] EmailService emailService)
        {
            await emailService.SendEmailAsync("binuyomayor16@gmail.com", "Test Email", "Email service is working!");

            return Ok(new { message = "Email sent successfully!" });
        }


        // ---------------- REGISTER ----------------
        [HttpPost("register")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var message = await _authService.RegisterAsync(request);
                return Ok(new { message });
            }
            catch (ApplicationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        // ---------------- VERIFY EMAIL ----------------
        [HttpPost("verify-email")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
        {
            try
            {
                var message = await _authService.VerifyEmailAsync(dto.Email, dto.Otp);
                return Ok(new { message });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ---------------- VERIFY INSTITUTION ----------------
        [HttpPost("verify-institution")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyInstitution([FromBody] VerifyInstitutionDto dto)
        {
            try
            {
                var message = await _authService.VerifyInstitutionAsync(dto.Email, dto.InviteCode);
                return Ok(new { message });
            }
            catch (ApplicationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ---------------- LOGIN ----------------
        [HttpPost("login")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                return Unauthorized("Invalid credentials.");

            if (!user.IsEmailVerified || !user.IsInstitutionVerified)
                return Unauthorized("Account not fully verified.");

            var result = _authService.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Invalid credentials.");

            var token = JwtHelper.GenerateToken(user, _config);
            return Ok(new { token });
        }
    }
}
