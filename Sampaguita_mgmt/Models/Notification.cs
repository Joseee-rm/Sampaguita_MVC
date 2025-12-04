// Models/Notification.cs
namespace SeniorManagement.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserRole { get; set; }
        public string Type { get; set; } // info, warning, success, danger
        public string Title { get; set; }
        public string Message { get; set; }
        public string Url { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        // Helper property
        public string TimeAgo => GetTimeAgo(CreatedAt);

        private string GetTimeAgo(DateTime date)
        {
            var timeSpan = DateTime.Now - date;

            if (timeSpan.Days > 0)
                return $"{timeSpan.Days}d ago";
            if (timeSpan.Hours > 0)
                return $"{timeSpan.Hours}h ago";
            if (timeSpan.Minutes > 0)
                return $"{timeSpan.Minutes}m ago";

            return "Just now";
        }
    }
}