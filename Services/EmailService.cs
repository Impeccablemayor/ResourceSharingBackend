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
            var host = settings["Host"];
            var port = int.TryParse(settings["Port"], out var p) ? p : 587;
            var username = settings["Username"];
            var password = settings["Password"];
            var fromEmail = settings["FromEmail"];
            var fromName = settings["FromName"];

            // If Username is not a full email, fall back to FromEmail to avoid misconfiguration
            if (string.IsNullOrWhiteSpace(username) || !username.Contains("@"))
            {
                username = fromEmail;
            }

            using var client = new SmtpClient(host, port)
            {
                UseDefaultCredentials = false, // must be false when supplying explicit credentials
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 20000
            };

            var mail = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
        }
    }
}
