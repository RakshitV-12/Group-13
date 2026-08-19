namespace ExpenseTracker.Models
{
    public class Transaction
    {
        public long TransactionId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Expense"; // "Income" or "Expense"
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        public string PaymentMethod { get; set; } = "UPI"; // Cash, Credit Card, Debit Card, UPI, Net Banking, Other
        public string? Description { get; set; }
        public bool IsRecurring { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;
    }

    public class Budget
    {
        public int BudgetId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int? CategoryId { get; set; } // NULL = overall monthly budget
        public decimal Amount { get; set; }
        public int PeriodMonth { get; set; }
        public int PeriodYear { get; set; }
        public decimal ThresholdPercent { get; set; } = 80.00m;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
        public virtual Category? Category { get; set; }
    }

    public class AIInsight
    {
        public long InsightId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string InsightType { get; set; } = "Analysis"; // Anomaly, Prediction, Recommendation, 50-30-20
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public decimal? ConfidenceScore { get; set; } = 0.90m;
        public bool IsRead { get; set; } = false;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
    }
}
