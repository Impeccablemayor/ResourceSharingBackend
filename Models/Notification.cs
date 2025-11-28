namespace AcademicResourceApp.Models
{
    public class Notification
    {
        public int Id { get; set; }

        // ID of the user who should receive the notification
        public Guid UserId { get; set; }
        public User User { get; set; }

        // What the notification is about
        public string Title { get; set; }          // e.g. "New Borrow Request"
        public string Message { get; set; }        // e.g. "John requested your 'Data Structures' book."

        public NotificationType Type { get; set; } // Enum

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum NotificationType
    {
        Request,     // A user requested a resource
        Approval,    // A user's request was approved
        Pending,     // Waiting for approval
        Upload,      // A resource was uploaded
        Reminder     // Any reminder notification
    }
}
