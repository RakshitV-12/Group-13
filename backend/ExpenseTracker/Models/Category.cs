namespace ExpenseTracker.Models
{
    public class Category
    {
        public int CategoryId { get; set; }
        public string? UserId { get; set; } // NULL for system default categories
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Expense"; // "Income" or "Expense"
        public string? Icon { get; set; }
        public string? ColorCode { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User? User { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public virtual ICollection<Budget> Budgets { get; set; } = new List<Budget>();
        public virtual ICollection<CategoryKeyword> Keywords { get; set; } = new List<CategoryKeyword>();
    }

    public class CategoryKeyword
    {
        public int KeywordId { get; set; }
        public int CategoryId { get; set; }
        public string Keyword { get; set; } = string.Empty;

        public virtual Category Category { get; set; } = null!;
    }
}
