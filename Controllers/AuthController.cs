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
        private readonly IConfiguration _config;
        private readonly AuthService _authService;

        public AuthController(AppDbContext context, IConfiguration config, AuthService authService)
        {
            _context = context;
            _config = config;
            _authService = authService;
        }

        [HttpPost("register")]
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

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                return Unauthorized("Invalid credentials.");

            var result = _authService.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Invalid credentials.");

            var token = JwtHelper.GenerateToken(user, _config);
            return Ok(new { token });
        }


    }

}
