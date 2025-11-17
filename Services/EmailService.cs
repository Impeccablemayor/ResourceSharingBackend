using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace AcademicResourceApp.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var settings = _config.GetSection("EmailSettings");
            using var client = new SmtpClient(settings["Host"], int.Parse(settings["Port"]))
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(settings["Username"], settings["Password"]),
                EnableSsl = true
            };

            var mail = new MailMessage
            {
                From = new MailAddress(settings["FromEmail"], settings["FromName"]),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
        }
    }
}
