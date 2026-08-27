using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Models
{
    public class User : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public virtual ICollection<Budget> Budgets { get; set; } = new List<Budget>();
        public virtual ICollection<AIInsight> AIInsights { get; set; } = new List<AIInsight>();
        public virtual ICollection<FinancialRule> FinancialRules { get; set; } = new List<FinancialRule>();
        public virtual ICollection<UserActiveRule> UserActiveRules { get; set; } = new List<UserActiveRule>();
        public virtual ICollection<RecurringExpense> RecurringExpenses { get; set; } = new List<RecurringExpense>();
        public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
        public virtual ICollection<Goal> Goals { get; set; } = new List<Goal>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
