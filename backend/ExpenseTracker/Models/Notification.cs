namespace ExpenseTracker.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = "Info"; // "BudgetWarning", "BudgetExceeded", "GoalAchieved"
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ReferenceKey { get; set; } // Used to prevent creating duplicate notifications for the same budget/goal condition

        public virtual User User { get; set; } = null!;
    }
}
