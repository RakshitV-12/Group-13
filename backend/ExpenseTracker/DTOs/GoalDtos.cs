using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.DTOs
{
    public class CreateGoalDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Target amount must be greater than zero.")]
        public decimal TargetAmount { get; set; }

        [Range(0.0, double.MaxValue, ErrorMessage = "Current amount cannot be negative.")]
        public decimal CurrentAmount { get; set; } = 0m;

        [Required]
        public DateTime DueDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class UpdateGoalDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Target amount must be greater than zero.")]
        public decimal TargetAmount { get; set; }

        [Range(0.0, double.MaxValue, ErrorMessage = "Current amount cannot be negative.")]
        public decimal CurrentAmount { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public string? Status { get; set; }
    }

    public class GoalResponseDto
    {
        public int GoalId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public decimal RemainingAmount => Math.Max(0, TargetAmount - CurrentAmount);
        public decimal ProgressPercentage => TargetAmount > 0 ? Math.Min(100.0m, Math.Round((CurrentAmount / TargetAmount) * 100, 2)) : 0m;
        public DateTime DueDate { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = "In Progress";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
