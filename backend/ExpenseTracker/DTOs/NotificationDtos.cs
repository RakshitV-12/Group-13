namespace ExpenseTracker.DTOs
{
    public class NotificationResponseDto
    {
        public int NotificationId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = "Info";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
