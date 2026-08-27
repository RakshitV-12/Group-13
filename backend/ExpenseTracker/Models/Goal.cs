namespace ExpenseTracker.Models
{
    public class Goal
    {
        public int GoalId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public DateTime DueDate { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = "In Progress"; // "In Progress" or "Achieved"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
