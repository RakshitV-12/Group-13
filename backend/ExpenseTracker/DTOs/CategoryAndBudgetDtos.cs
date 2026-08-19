using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.DTOs
{
    public class CreateCategoryDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = "Expense"; // "Income" or "Expense"

        public string? Icon { get; set; } = "tag";
        public string? ColorCode { get; set; } = "#6c757d";
    }

    public class CategoryResponseDto
    {
        public int CategoryId { get; set; }
        public string? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? ColorCode { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateBudgetDto
    {
        public int? CategoryId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required]
        [Range(1, 12)]
        public int PeriodMonth { get; set; }

        [Required]
        [Range(2020, 2100)]
        public int PeriodYear { get; set; }

        public decimal ThresholdPercent { get; set; } = 80.00m;
    }

    public class BudgetResponseDto
    {
        public int BudgetId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = "Overall";
        public string? CategoryIcon { get; set; }
        public string? CategoryColor { get; set; }
        public decimal Amount { get; set; }
        public int PeriodMonth { get; set; }
        public int PeriodYear { get; set; }
        public decimal ThresholdPercent { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BudgetStatusDto
    {
        public int BudgetId { get; set; }
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = "Overall";
        public decimal BudgetAmount { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal RemainingBudget => Math.Max(0, BudgetAmount - TotalSpent);
        public decimal UtilizationPercentage { get; set; }
        public decimal ThresholdPercent { get; set; }
        public bool IsWarning { get; set; }
        public bool IsExceeded { get; set; }
        public int PeriodMonth { get; set; }
        public int PeriodYear { get; set; }
    }
}
