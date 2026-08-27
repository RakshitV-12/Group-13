using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services
{
    public class CsvImportService
    {
        private readonly ExpenseTrackerDbContext _context;
        private readonly AICategorizationService _aiService;
        private readonly BudgetService _budgetService;

        public CsvImportService(
            ExpenseTrackerDbContext context,
            AICategorizationService aiService,
            BudgetService budgetService)
        {
            _context = context;
            _aiService = aiService;
            _budgetService = budgetService;
        }

        public async Task<CsvPreviewResponseDto> PreviewCsvAsync(string userId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("CSV file is required.");

            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();

            return await PreviewCsvContentAsync(userId, content);
        }

        public async Task<CsvPreviewResponseDto> PreviewCsvContentAsync(string userId, string csvContent)
        {
            var lines = csvContent
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
                throw new ArgumentException("CSV file is empty.");

            // Check if first row is header
            int startIndex = 0;
            var firstLine = lines[0].ToLowerInvariant();
            if (firstLine.Contains("date") || firstLine.Contains("name") || firstLine.Contains("amount") || firstLine.Contains("merchant") || firstLine.Contains("description"))
            {
                startIndex = 1;
            }

            var parsedRows = new List<CsvPreviewRowDto>();
            var uncategorizedItems = new List<AICategorizeRequestDto>();

            var userCategories = await _context.Categories
                .Where(c => c.UserId == null || c.UserId == userId)
                .AsNoTracking()
                .ToListAsync();

            var categoryMap = userCategories.ToDictionary(c => c.Name, c => c.CategoryId, StringComparer.OrdinalIgnoreCase);

            for (int i = startIndex; i < lines.Count; i++)
            {
                var line = lines[i];
                var parts = ParseCsvLine(line);
                if (parts.Count < 2) continue;

                var row = new CsvPreviewRowDto
                {
                    RowId = Guid.NewGuid().ToString("N")
                };

                // Extract fields: Date, Name/Description, Amount, optional Category, PaymentMethod, Description
                string dateStr = parts[0];
                string nameStr = parts.Count > 1 ? parts[1] : "Expense";
                string amountStr = parts.Count > 2 ? parts[2] : "0";
                string? explicitCategory = parts.Count > 3 ? parts[3] : null;
                string? explicitPaymentMethod = parts.Count > 4 ? parts[4] : null;

                row.DateRaw = dateStr;
                row.Name = nameStr;
                row.PaymentMethod = !string.IsNullOrWhiteSpace(explicitPaymentMethod) ? explicitPaymentMethod.Trim() : "UPI";

                // Validate Date
                if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
                    DateTime.TryParse(dateStr, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
                {
                    row.ParsedDate = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }
                else
                {
                    row.IsValid = false;
                    row.StatusBadge = "Invalid";
                    row.StatusReason = "Invalid date format.";
                }

                // Validate Amount
                var cleanAmountStr = Regex.Replace(amountStr, @"[^\d\.\-]", "");
                if (decimal.TryParse(cleanAmountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt) && amt > 0)
                {
                    row.Amount = amt;
                }
                else
                {
                    row.IsValid = false;
                    row.StatusBadge = "Invalid";
                    row.StatusReason = string.IsNullOrEmpty(row.StatusReason) ? "Amount must be greater than 0." : $"{row.StatusReason} Amount must be greater than 0.";
                }

                // Explicit category match
                if (!string.IsNullOrWhiteSpace(explicitCategory))
                {
                    var matchedName = userCategories.FirstOrDefault(c => string.Equals(c.Name, explicitCategory.Trim(), StringComparison.OrdinalIgnoreCase))?.Name;
                    if (matchedName != null)
                    {
                        row.CategoryName = matchedName;
                        row.CategoryId = categoryMap[matchedName];
                        row.StatusBadge = "Valid";
                        if (string.Equals(matchedName, "Income", StringComparison.OrdinalIgnoreCase) || string.Equals(matchedName, "Salary", StringComparison.OrdinalIgnoreCase))
                        {
                            row.Type = "Income";
                        }
                    }
                }

                parsedRows.Add(row);

                if (row.IsValid && string.IsNullOrEmpty(row.CategoryName))
                {
                    uncategorizedItems.Add(new AICategorizeRequestDto { Name = row.Name, Amount = row.Amount });
                }
            }

            // Run RAG AI Categorization for uncategorized rows
            if (uncategorizedItems.Count > 0)
            {
                var aiClassifications = await _aiService.CategorizeBatchAsync(userId, uncategorizedItems);
                var aiMap = aiClassifications.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var r in parsedRows)
                {
                    if (r.IsValid && string.IsNullOrEmpty(r.CategoryName))
                    {
                        if (aiMap.TryGetValue(r.Name, out var ai))
                        {
                            r.CategoryName = ai.Category;
                            r.CategoryId = ai.CategoryId;
                            r.Confidence = ai.Confidence;

                            if (ai.NeedsReview || ai.Category == "Needs Review" || ai.CategoryId == null)
                            {
                                r.StatusBadge = "Needs Review";
                                r.StatusReason = string.IsNullOrWhiteSpace(ai.Reason) ? "Low AI confidence" : ai.Reason;
                            }
                            else
                            {
                                r.StatusBadge = "AI Suggested";
                                r.StatusReason = ai.Reason;
                            }
                        }
                        else
                        {
                            r.StatusBadge = "Needs Review";
                            r.CategoryName = "Needs Review";
                        }
                    }
                }
            }

            // Duplicate Detection against SQL Server transactions
            var existingTxns = await _context.Transactions
                .Where(t => t.UserId == userId)
                .Select(t => new { t.TransactionDate, Description = t.Description ?? "", t.Amount })
                .AsNoTracking()
                .ToListAsync();

            foreach (var r in parsedRows)
            {
                if (r.IsValid && r.ParsedDate.HasValue)
                {
                    var isDup = existingTxns.Any(t =>
                        t.TransactionDate.Date == r.ParsedDate.Value.Date &&
                        t.Amount == r.Amount &&
                        string.Equals(t.Description.Trim(), r.Name.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (isDup)
                    {
                        r.IsDuplicate = true;
                        r.StatusBadge = "Possible Duplicate";
                        r.StatusReason = "Matches existing date, name, and amount in SQL Server.";
                    }
                }
            }

            return new CsvPreviewResponseDto
            {
                Rows = parsedRows
            };
        }

        public async Task<int> ConfirmImportAsync(string userId, CsvConfirmImportDto dto)
        {
            if (dto.Transactions == null || dto.Transactions.Count == 0)
                throw new ArgumentException("No transactions provided for import.");

            var userCategories = await _context.Categories
                .Where(c => c.UserId == null || c.UserId == userId)
                .AsNoTracking()
                .ToListAsync();

            var categoryIds = userCategories.Select(c => c.CategoryId).ToHashSet();
            var defaultExpenseCategory = userCategories.FirstOrDefault(c => c.Name == "Other") ?? userCategories.First();

            var newTransactions = new List<Transaction>();

            foreach (var item in dto.Transactions)
            {
                if (item.Amount <= 0)
                    throw new ArgumentException($"Invalid transaction amount for '{item.Name}'. Must be greater than 0.");

                var categoryId = categoryIds.Contains(item.CategoryId) ? item.CategoryId : defaultExpenseCategory.CategoryId;

                var tx = new Transaction
                {
                    UserId = userId,
                    CategoryId = categoryId,
                    Amount = item.Amount,
                    Type = string.IsNullOrWhiteSpace(item.Type) ? "Expense" : item.Type,
                    TransactionDate = item.TransactionDate == default ? DateTime.UtcNow : item.TransactionDate,
                    PaymentMethod = string.IsNullOrWhiteSpace(item.PaymentMethod) ? "UPI" : item.PaymentMethod,
                    Description = item.Name.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                newTransactions.Add(tx);
            }

            await _context.Transactions.AddRangeAsync(newTransactions);
            await _context.SaveChangesAsync();

            // Re-evaluate budgets for warning/exceeded status & generate notifications
            try
            {
                await _budgetService.GetBudgetStatusesAsync(userId);
            }
            catch
            {
                // Non-blocking for import response
            }

            return newTransactions.Count;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString().Trim());
            return result;
        }
    }
}
