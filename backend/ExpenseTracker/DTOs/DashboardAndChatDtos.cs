namespace ExpenseTracker.DTOs
{
    public class DashboardSummaryDto
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalSavings => TotalIncome - TotalExpenses;
        public decimal SavingsPercentage => TotalIncome > 0 ? Math.Round(((TotalIncome - TotalExpenses) / TotalIncome) * 100, 2) : 0;
        public List<CategorySpendingDto> CategoryExpenses { get; set; } = new();
        public List<MonthlyTrendDto> MonthlySpending { get; set; } = new();
        public List<TransactionResponseDto> RecentTransactions { get; set; } = new();
        public FinancialRule50_30_20Dto FinancialRule { get; set; } = new();
        public List<BudgetStatusDto> ActiveBudgets { get; set; } = new();
    }

    public class CategorySpendingDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? ColorCode { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
        public int Count { get; set; }
    }

    public class MonthlyTrendDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
    }

    public class FinancialRule50_30_20Dto
    {
        public decimal TotalIncome { get; set; }
        public decimal NeedsAmount { get; set; }
        public decimal NeedsPercentage { get; set; } // Target: <= 50%
        public decimal WantsAmount { get; set; }
        public decimal WantsPercentage { get; set; } // Target: <= 30%
        public decimal SavingsAmount { get; set; }
        public decimal SavingsPercentage { get; set; } // Target: >= 20%
        public string Status { get; set; } = "On Track";
        public string Recommendation { get; set; } = string.Empty;
    }

    public class ChatQueryDto
    {
        public string Message { get; set; } = string.Empty;
    }

    public class ChatResponseDto
    {
        public string Answer { get; set; } = string.Empty;
        public string? Category { get; set; }
        public decimal? ComputedAmount { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
