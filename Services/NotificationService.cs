using AcademicResourceApp.Data;
using AcademicResourceApp.Hubs;
using AcademicResourceApp.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AcademicResourceApp.Services
{
    /// <summary>
    /// Handles creating, storing, and sending real-time notifications using SignalR.
    /// </summary>
    public class NotificationService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hub;

        public NotificationService(AppDbContext context, IHubContext<NotificationHub> hub)
        {
            _context = context;
            _hub = hub;
        }

        /// <summary>
        /// Creates a notification for a user and broadcasts it in real-time using SignalR.
        /// </summary>
        /// <param name="userId">GUID of the user receiving the notification</param>
        /// <param name="title">Short title of the notification</param>
        /// <param name="message">Main content of the notification</param>
        /// <param name="type">Notification type (Request, Approval, Pending, etc.)</param>
        /// <returns>The saved Notification object</returns>
        /// <response code="200">Notification sent successfully</response>
        public async Task<Notification> NotifyAsync(Guid userId, string title, string message, NotificationType type)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            // 1. Save notification to the database
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // 2. Try to broadcast via SignalR
            try
            {
                await _hub.Clients.Group(userId.ToString()).SendAsync("ReceiveNotification", new
                {
                    notification.Title,
                    notification.Message,
                    Type = notification.Type.ToString(),
                    notification.CreatedAt
                });
            }
            catch (Exception ex)
            {
                // Prevent SignalR failure from breaking app
                Console.WriteLine($"SignalR Error: {ex.Message}");
            }

            return notification;
        }

        /// <summary>
        /// Retrieves all unread notifications for a specific user.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of unread notifications</returns>
        public async Task<List<Notification>> GetUnreadAsync(Guid userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves all notifications (read + unread) for a user.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of all notifications</returns>
        public async Task<List<Notification>> GetAllAsync(Guid userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Marks a specific notification as read.
        /// </summary>
        /// <param name="id">Notification ID</param>
        /// <returns>Task</returns>
        public async Task MarkAsReadAsync(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null) return;

            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }
}
