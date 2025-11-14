using AcademicResourceApp.Data;
using AcademicResourceApp.DTOs;
using AcademicResourceApp.Models;
using AcademicResourceApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly EmailService _emailService;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext context, IPasswordHasher<User> passwordHasher, EmailService emailService, IConfiguration config)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _config = config;
    }

    public IPasswordHasher<User> PasswordHasher => _passwordHasher;

    // ---------------- REGISTER ----------------
    public async Task<string> RegisterAsync(RegisterDto request)
    {
        var exists = await _context.Users.AnyAsync(u => u.Email == request.Email);
        if (exists) throw new ApplicationException("Email already exists");

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            SchoolEmail = request.SchoolEmail
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        // Generate OTP
        user.EmailVerificationCode = new Random().Next(100000, 999999).ToString();
        user.CodeExpiry = DateTime.UtcNow.AddMinutes(15);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Send OTP via Gmail SMTP
        var htmlContent = $"<p>Your PeerShelf verification code is: <b>{user.EmailVerificationCode}</b></p>";
        await _emailService.SendEmailAsync(user.Email, "Verify your email", htmlContent);

        return "Registration successful! Please check your email for the OTP.";
    }

    // ---------------- VERIFY EMAIL ----------------
    public async Task<string> VerifyEmailAsync(string email, string otp)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) throw new ApplicationException("User not found");
        if (user.IsEmailVerified) return "Email already verified";

        if (user.EmailVerificationCode != otp || user.CodeExpiry < DateTime.UtcNow)
            throw new ApplicationException("Invalid or expired OTP");

        user.IsEmailVerified = true;
        user.EmailVerificationCode = null;
        user.CodeExpiry = null;

        await _context.SaveChangesAsync();
        return "Email verified successfully!";
    }

    // ---------------- VERIFY INSTITUTION ----------------
    public async Task<string> VerifyInstitutionAsync(string email, string inviteCode)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) throw new ApplicationException("User not found");
        if (!user.IsEmailVerified) throw new ApplicationException("Email not verified yet");
        if (user.IsInstitutionVerified) return "Institution already verified";

        // Hardcoded Unilorin code (can later be in DB/config)
        var secretCode = _config["Institution:InviteCode"];
        if (inviteCode != secretCode) throw new ApplicationException("Invalid institution invite code");

        user.IsInstitutionVerified = true;
        await _context.SaveChangesAsync();

        return "Institution verified successfully!";
    }
}
