using System;
using System.Collections.Generic;

namespace ExpenseTracker.Models
{
    public class FinancialRule
    {
        public int RuleId { get; set; }
        public string? UserId { get; set; } // NULL for system default rules
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string StrategyType { get; set; } = "PercentageBased"; // PercentageBased, PayYourselfFirst, ZeroBased, Custom
        public bool IsSystemDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User? User { get; set; }
        public virtual ICollection<FinancialRuleAllocation> Allocations { get; set; } = new List<FinancialRuleAllocation>();
    }

    public class FinancialRuleAllocation
    {
        public int AllocationId { get; set; }
        public int RuleId { get; set; }
        public string BucketName { get; set; } = string.Empty; // Needs, Wants, Savings, Investment, Entertainment, Emergency, Other
        public decimal Percentage { get; set; } // e.g. 50.00
        public decimal? TargetAmount { get; set; } // Optional fixed target amount
        public string? CategoryNamesCsv { get; set; } // e.g. "Food,Rent,Bills,Healthcare,Education"

        public virtual FinancialRule Rule { get; set; } = null!;
    }

    public class UserActiveRule
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int RuleId { get; set; }
        public decimal MonthlyIncome { get; set; } = 0.00m;
        public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
        public virtual FinancialRule Rule { get; set; } = null!;
    }

    public class RecurringExpense
    {
        public long RecurringId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Frequency { get; set; } = "Monthly"; // Monthly, Annual
        public int CategoryId { get; set; }
        public DateTime LastObservedDate { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;
    }
}
