namespace ExpenseTracker.DTOs
{
    public class CsvPreviewRowDto
    {
        public string RowId { get; set; } = Guid.NewGuid().ToString("N");
        public string DateRaw { get; set; } = string.Empty;
        public DateTime? ParsedDate { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string StatusBadge { get; set; } = "Valid"; // "Valid", "Needs Review", "Possible Duplicate", "AI Suggested", "Invalid"
        public string? StatusReason { get; set; }
        public bool IsDuplicate { get; set; }
        public bool IsValid { get; set; } = true;
        public decimal Confidence { get; set; } = 1.0m;
        public string PaymentMethod { get; set; } = "UPI";
        public string Type { get; set; } = "Expense";
    }

    public class CsvPreviewResponseDto
    {
        public List<CsvPreviewRowDto> Rows { get; set; } = new();
        public int TotalTransactions => Rows.Count;
        public decimal TotalAmount => Rows.Sum(r => r.Amount);
        public int ValidCount => Rows.Count(r => r.IsValid);
        public int ReviewCount => Rows.Count(r => r.StatusBadge == "Needs Review");
        public int DuplicateCount => Rows.Count(r => r.IsDuplicate);
    }

    public class CsvConfirmImportDto
    {
        public List<CsvTransactionItemDto> Transactions { get; set; } = new();
    }

    public class CsvTransactionItemDto
    {
        public DateTime TransactionDate { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int CategoryId { get; set; }
        public string PaymentMethod { get; set; } = "UPI";
        public string Type { get; set; } = "Expense";
    }
}
