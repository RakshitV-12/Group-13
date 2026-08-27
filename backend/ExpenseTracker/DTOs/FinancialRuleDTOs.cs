using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.DTOs
{
    public class FinancialRuleDto
    {
        public int RuleId { get; set; }
        public string? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string StrategyType { get; set; } = "PercentageBased";
        public bool IsSystemDefault { get; set; }
        public List<FinancialRuleAllocationDto> Allocations { get; set; } = new();
    }

    public class FinancialRuleAllocationDto
    {
        public int AllocationId { get; set; }
        public string BucketName { get; set; } = string.Empty;
        public decimal Percentage { get; set; }
        public decimal? TargetAmount { get; set; }
        public string? CategoryNamesCsv { get; set; }
    }

    public class CreateCustomRuleRequestDto : IValidatableObject
    {
        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public List<CreateRuleAllocationRequestDto> Allocations { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Allocations == null || !Allocations.Any())
            {
                yield return new ValidationResult("At least one allocation bucket is required.", new[] { nameof(Allocations) });
                yield break;
            }

            var totalPercentage = Allocations.Sum(a => a.Percentage);
            if (Math.Abs(totalPercentage - 100.00m) > 0.01m)
            {
                yield return new ValidationResult($"Allocation percentages must total 100%. Current total is {totalPercentage:F2}%.", new[] { nameof(Allocations) });
            }
        }
    }

    public class CreateRuleAllocationRequestDto
    {
        [Required]
        public string BucketName { get; set; } = string.Empty;

        [Range(0.01, 100.00)]
        public decimal Percentage { get; set; }

        public string? CategoryNamesCsv { get; set; }
    }

    public class ActivateRuleRequestDto
    {
        [Required]
        public int RuleId { get; set; }

        [Range(0.01, 100000000.00)]
        public decimal MonthlyIncome { get; set; }
    }

    public class UserActiveRuleStatusDto
    {
        public int ActiveRuleId { get; set; }
        public int RuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string StrategyType { get; set; } = string.Empty;
        public string RuleDescription { get; set; } = string.Empty;
        public decimal MonthlyIncome { get; set; }
        public decimal TotalActualExpenses { get; set; }
        public decimal TotalActualSavings { get; set; }
        public List<RuleBucketStatusDto> Buckets { get; set; } = new();
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RuleBucketStatusDto
    {
        public string BucketName { get; set; } = string.Empty;
        public decimal TargetPercentage { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal ActualSpent { get; set; }
        public decimal VarianceAmount { get; set; }
        public string Status { get; set; } = "WithinTarget"; // WithinTarget, OverTarget, OnTrack, Warning
        public string CategoriesCsv { get; set; } = string.Empty;
    }
}
