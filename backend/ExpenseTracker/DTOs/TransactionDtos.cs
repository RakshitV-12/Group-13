using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.DTOs
{
    public class CreateTransactionDto
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required]
        public string Type { get; set; } = "Expense"; // "Income" or "Expense"

        [Required]
        public int CategoryId { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        public string PaymentMethod { get; set; } = "UPI";

        [MaxLength(500)]
        public string? Description { get; set; }
    }

    public class QuickExpenseDto
    {
        [Required(ErrorMessage = "Input text is required (e.g. 'Suji 250').")]
        public string Input { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = "UPI";

        public DateTime? TransactionDate { get; set; }
    }

    public class QuickExpenseParseResultDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string MatchedKeyword { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "UPI";
        public long? CreatedTransactionId { get; set; }
    }

    public class UpdateTransactionDto
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required]
        public string Type { get; set; } = "Expense";

        [Required]
        public int CategoryId { get; set; }

        public DateTime TransactionDate { get; set; }

        public string PaymentMethod { get; set; } = "UPI";

        [MaxLength(500)]
        public string? Description { get; set; }
    }

    public class TransactionResponseDto
    {
        public long TransactionId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Expense";
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryIcon { get; set; }
        public string? CategoryColor { get; set; }
        public DateTime TransactionDate { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 1));
    }
}
