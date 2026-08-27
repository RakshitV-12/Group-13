namespace ExpenseTracker.DTOs
{
    public class AICategorizeRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class AICategorizeBatchRequestDto
    {
        public List<AICategorizeRequestDto> Items { get; set; } = new();
    }

    public class AICategorizeResponseDto
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "Other";
        public int? CategoryId { get; set; }
        public decimal Confidence { get; set; } = 0.90m;
        public string Reason { get; set; } = string.Empty;
        public bool NeedsReview => Confidence < 0.70m || Category == "Needs Review";
    }
}
