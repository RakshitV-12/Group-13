using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services
{
    public class AICategorizationService
    {
        private readonly ExpenseTrackerDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AICategorizationService> _logger;

        public AICategorizationService(
            ExpenseTrackerDbContext context,
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<AICategorizationService> logger)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<AICategorizeResponseDto> CategorizeSingleAsync(string userId, string name, decimal amount)
        {
            var batch = await CategorizeBatchAsync(userId, new List<AICategorizeRequestDto>
            {
                new AICategorizeRequestDto { Name = name, Amount = amount }
            });

            return batch.FirstOrDefault() ?? new AICategorizeResponseDto
            {
                Name = name,
                Category = "Needs Review",
                Confidence = 0.5m,
                Reason = "Classification failed"
            };
        }

        public async Task<List<AICategorizeResponseDto>> CategorizeBatchAsync(string userId, List<AICategorizeRequestDto> items)
        {
            var userCategories = await _context.Categories
                .Where(c => c.UserId == null || c.UserId == userId)
                .AsNoTracking()
                .ToListAsync();

            var categoryMap = userCategories
                .ToDictionary(c => c.Name, c => c.CategoryId, StringComparer.OrdinalIgnoreCase);

            var categoryNames = userCategories.Select(c => c.Name).Distinct().ToList();

            // Fetch User RAG Context (Historical transactions for current user)
            var userHistory = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Description != null)
                .OrderByDescending(t => t.TransactionDate)
                .Take(50)
                .Select(t => new { Description = t.Description!, CategoryName = t.Category.Name })
                .AsNoTracking()
                .ToListAsync();

            var historyContext = userHistory
                .GroupBy(h => h.Description)
                .Select(g => $"{g.Key} -> {g.First().CategoryName}")
                .Take(25)
                .ToList();

            var apiKey = _configuration["Groq:ApiKey"] ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");
            var endpoint = _configuration["Groq:Endpoint"] ?? "https://api.groq.com/openai/v1/chat/completions";
            var model = _configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";

            var results = new List<AICategorizeResponseDto>();

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    results = await CallGroqApiBatchAsync(endpoint, apiKey, model, items, categoryNames, historyContext, categoryMap);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Groq API categorization failed, using local RAG fallback.");
                    results = LocalRagFallback(items, categoryNames, userHistory, categoryMap);
                }
            }

            if (results.Count == 0)
            {
                results = LocalRagFallback(items, categoryNames, userHistory, categoryMap);
            }

            return results;
        }

        private async Task<List<AICategorizeResponseDto>> CallGroqApiBatchAsync(
            string endpoint,
            string apiKey,
            string model,
            List<AICategorizeRequestDto> items,
            List<string> categoryNames,
            List<string> historyContext,
            Dictionary<string, int> categoryMap)
        {
            var systemPrompt = $@"You are an AI financial categorizer. Classify transaction item names into one of the user's available categories.
Available Categories: [{string.Join(", ", categoryNames)}].

User's Historical Transactions Context (RAG):
{(historyContext.Count > 0 ? string.Join("\n", historyContext) : "No prior history available.")}

CRITICAL RULES:
1. You MUST match spelling mistakes or variations intelligently (e.g., 'Ubr' -> 'Transportation', 'swigy' -> 'Food', 'chai' -> 'Food').
2. Return ONLY a raw JSON array of objects. Do NOT include markdown code blocks or additional text.
3. Output format for each item:
{{
  ""name"": ""item name"",
  ""category"": ""Category Name"",
  ""confidence"": 0.95,
  ""reason"": ""Explanation""
}}
4. If category is uncertain or confidence < 0.70, output category as 'Needs Review'.";

            var userContent = JsonSerializer.Serialize(items.Select(i => new { name = i.Name, amount = i.Amount }));

            var payload = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userContent }
                },
                temperature = 0.1,
                response_format = new { type = "json_object" }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Groq API responded with status {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("Groq returned empty response");
            }

            var list = ParseGroqJsonResponse(content, items, categoryNames, categoryMap);
            return list;
        }

        private List<AICategorizeResponseDto> ParseGroqJsonResponse(
            string content,
            List<AICategorizeRequestDto> items,
            List<string> categoryNames,
            Dictionary<string, int> categoryMap)
        {
            var results = new List<AICategorizeResponseDto>();
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                JsonElement arrayElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    arrayElement = root;
                }
                else if (root.TryGetProperty("items", out var itemsArr) && itemsArr.ValueKind == JsonValueKind.Array)
                {
                    arrayElement = itemsArr;
                }
                else if (root.TryGetProperty("results", out var resArr) && resArr.ValueKind == JsonValueKind.Array)
                {
                    arrayElement = resArr;
                }
                else
                {
                    // Look for first array property
                    arrayElement = root.EnumerateObject().FirstOrDefault(p => p.Value.ValueKind == JsonValueKind.Array).Value;
                }

                if (arrayElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arrayElement.EnumerateArray())
                    {
                        var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var cat = item.TryGetProperty("category", out var c) ? c.GetString() ?? "Needs Review" : "Needs Review";
                        var conf = item.TryGetProperty("confidence", out var f) && f.TryGetDecimal(out var dec) ? dec : 0.90m;
                        var reason = item.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";

                        // Validate category exists
                        var matchedName = categoryNames.FirstOrDefault(cn =>
                            string.Equals(cn, cat, StringComparison.OrdinalIgnoreCase) ||
                            (cn.StartsWith("Transport", StringComparison.OrdinalIgnoreCase) && cat.StartsWith("Transport", StringComparison.OrdinalIgnoreCase)) ||
                            (cn.StartsWith("Food", StringComparison.OrdinalIgnoreCase) && cat.StartsWith("Food", StringComparison.OrdinalIgnoreCase)) ||
                            (cn.StartsWith("Shop", StringComparison.OrdinalIgnoreCase) && cat.StartsWith("Shop", StringComparison.OrdinalIgnoreCase)));

                        int? catId = matchedName != null && categoryMap.TryGetValue(matchedName, out var id) ? id : null;

                        if (matchedName == null || conf < 0.70m)
                        {
                            cat = "Needs Review";
                            catId = null;
                            if (conf >= 0.70m) conf = 0.65m;
                        }
                        else
                        {
                            cat = matchedName;
                        }

                        results.Add(new AICategorizeResponseDto
                        {
                            Name = name,
                            Category = cat,
                            CategoryId = catId,
                            Confidence = conf,
                            Reason = reason
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing Groq JSON output");
            }

            return results;
        }

        private List<AICategorizeResponseDto> LocalRagFallback(
            List<AICategorizeRequestDto> items,
            List<string> categoryNames,
            IEnumerable<dynamic> userHistory,
            Dictionary<string, int> categoryMap)
        {
            var results = new List<AICategorizeResponseDto>();
            var keywords = _context.CategoryKeywords
                .Include(k => k.Category)
                .AsNoTracking()
                .ToList();

            foreach (var item in items)
            {
                var clean = item.Name.Trim().ToLowerInvariant();
                string category = "Needs Review";
                int? categoryId = null;
                decimal confidence = 0.50m;
                string reason = "Uncertain category";

                // 1. Direct RAG match with user's past transactions
                var pastMatch = userHistory.FirstOrDefault(h => clean.Contains(h.Description.ToLowerInvariant()) || h.Description.ToLowerInvariant().Contains(clean));
                if (pastMatch != null)
                {
                    category = pastMatch.CategoryName;
                    if (categoryMap.TryGetValue(category, out var id)) categoryId = id;
                    confidence = 0.94m;
                    reason = $"Matched previous user transaction: '{pastMatch.Description}' -> {category}";
                }
                else
                {
                    // 2. Keyword match
                    var kwMatch = keywords.FirstOrDefault(k => clean.Contains(k.Keyword.ToLowerInvariant()) || k.Keyword.ToLowerInvariant().Contains(clean));
                    if (kwMatch != null)
                    {
                        category = kwMatch.Category.Name;
                        categoryId = kwMatch.CategoryId;
                        confidence = 0.90m;
                        reason = $"Matched keyword rule '{kwMatch.Keyword}'";
                    }
                    else
                    {
                        // 3. Fallback common heuristics
                        if (clean.Contains("ubr") || clean.Contains("uber") || clean.Contains("ola") || clean.Contains("metro") || clean.Contains("auto"))
                        {
                            category = "Transport";
                            reason = "Recognized transportation service";
                            confidence = 0.88m;
                        }
                        else if (clean.Contains("chai") || clean.Contains("coffee") || clean.Contains("food") || clean.Contains("lunch") || clean.Contains("dinner") || clean.Contains("zomato") || clean.Contains("swiggy"))
                        {
                            category = "Food";
                            reason = "Recognized dining/food purchase";
                            confidence = 0.88m;
                        }
                        else if (clean.Contains("amazon") || clean.Contains("flipkart") || clean.Contains("myntra") || clean.Contains("shop"))
                        {
                            category = "Shopping";
                            reason = "Recognized merchant marketplace";
                            confidence = 0.88m;
                        }

                        if (category != "Needs Review")
                        {
                            var targetName = categoryNames.FirstOrDefault(cn =>
                                string.Equals(cn, category, StringComparison.OrdinalIgnoreCase) ||
                                (cn.StartsWith("Transport", StringComparison.OrdinalIgnoreCase) && category.StartsWith("Transport", StringComparison.OrdinalIgnoreCase)) ||
                                (cn.StartsWith("Food", StringComparison.OrdinalIgnoreCase) && category.StartsWith("Food", StringComparison.OrdinalIgnoreCase)) ||
                                (cn.StartsWith("Shop", StringComparison.OrdinalIgnoreCase) && category.StartsWith("Shop", StringComparison.OrdinalIgnoreCase)));

                            if (targetName != null && categoryMap.TryGetValue(targetName, out var id))
                            {
                                category = targetName;
                                categoryId = id;
                            }
                            else
                            {
                                category = "Needs Review";
                                categoryId = null;
                                confidence = 0.60m;
                            }
                        }
                    }
                }

                results.Add(new AICategorizeResponseDto
                {
                    Name = item.Name,
                    Category = category,
                    CategoryId = categoryId,
                    Confidence = confidence,
                    Reason = reason
                });
            }

            return results;
        }
    }
}
