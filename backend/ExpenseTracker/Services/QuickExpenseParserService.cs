using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Services
{
    public class QuickExpenseParserService
    {
        private readonly ExpenseTrackerDbContext _context;

        public QuickExpenseParserService(ExpenseTrackerDbContext context)
        {
            _context = context;
        }

        public async Task<QuickExpenseParseResultDto> ParseAsync(string input, string defaultPaymentMethod = "UPI")
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be empty.");

            var cleanInput = input.Trim();

            // 1. Extract Amount using Regex (e.g. 250, 250.50, ₹250, Rs. 250, 250rs)
            var amountMatch = Regex.Match(cleanInput, @"(?:\$|₹|rs\.?|inr)?\s*(\d+(?:\.\d{1,2})?)\s*(?:rs|inr)?", RegexOptions.IgnoreCase);
            if (!amountMatch.Success || !decimal.TryParse(amountMatch.Groups[1].Value, out var amount) || amount <= 0)
            {
                throw new ArgumentException("Could not detect a valid amount from input. Example format: 'Suji 250'");
            }

            // 2. Extract Description (remove amount from text)
            var description = Regex.Replace(cleanInput, @"(?:\$|₹|rs\.?|inr)?\s*\d+(?:\.\d{1,2})?\s*(?:rs|inr)?", "", RegexOptions.IgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                description = "Quick Expense";
            }

            // 3. Keyword Matching against Categories
            var lowerDesc = description.ToLowerInvariant();
            var descWords = lowerDesc.Split(new[] { ' ', ',', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Query DB keywords
            var allKeywords = await _context.CategoryKeywords
                .Include(k => k.Category)
                .AsNoTracking()
                .ToListAsync();

            Category? matchedCategory = null;
            string matchedKeyword = string.Empty;

            // First check whole phrase substring match
            foreach (var kw in allKeywords)
            {
                if (lowerDesc.Contains(kw.Keyword.ToLowerInvariant()))
                {
                    matchedCategory = kw.Category;
                    matchedKeyword = kw.Keyword;
                    break;
                }
            }

            // If not found, match by words
            if (matchedCategory == null)
            {
                foreach (var word in descWords)
                {
                    var kwMatch = allKeywords.FirstOrDefault(k => k.Keyword.ToLowerInvariant() == word);
                    if (kwMatch != null)
                    {
                        matchedCategory = kwMatch.Category;
                        matchedKeyword = kwMatch.Keyword;
                        break;
                    }
                }
            }

            // Fallback: Default to "Other" or "Food"
            if (matchedCategory == null)
            {
                matchedCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Name == "Other" && c.IsDefault)
                    ?? await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Food" && c.IsDefault)
                    ?? await _context.Categories.FirstAsync();
                matchedKeyword = "Default/Other";
            }

            // Capitalize first letter of description
            var formattedDesc = char.ToUpper(description[0]) + (description.Length > 1 ? description[1..] : "");

            return new QuickExpenseParseResultDto
            {
                Description = formattedDesc,
                Amount = amount,
                CategoryId = matchedCategory.CategoryId,
                CategoryName = matchedCategory.Name,
                MatchedKeyword = matchedKeyword,
                PaymentMethod = defaultPaymentMethod
            };
        }
    }
}
