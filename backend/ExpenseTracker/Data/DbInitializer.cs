using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(ExpenseTrackerDbContext context)
        {
            if (context.Database.IsRelational())
            {
                await context.Database.EnsureCreatedAsync();
            }

            if (!await context.Categories.AnyAsync(c => c.IsDefault))
            {
                var categories = new List<Category>
                {
                    new Category { Name = "Food", Type = "Expense", Icon = "utensils", ColorCode = "#fd7e14", IsDefault = true },
                    new Category { Name = "Shopping", Type = "Expense", Icon = "shopping-bag", ColorCode = "#6610f2", IsDefault = true },
                    new Category { Name = "Transport", Type = "Expense", Icon = "truck", ColorCode = "#0d6efd", IsDefault = true },
                    new Category { Name = "Bills", Type = "Expense", Icon = "zap", ColorCode = "#20c997", IsDefault = true },
                    new Category { Name = "Entertainment", Type = "Expense", Icon = "film", ColorCode = "#e83e8c", IsDefault = true },
                    new Category { Name = "Healthcare", Type = "Expense", Icon = "heart", ColorCode = "#dc3545", IsDefault = true },
                    new Category { Name = "Education", Type = "Expense", Icon = "book", ColorCode = "#198754", IsDefault = true },
                    new Category { Name = "Travel", Type = "Expense", Icon = "compass", ColorCode = "#0dcaf0", IsDefault = true },
                    new Category { Name = "Investment", Type = "Expense", Icon = "trending-up", ColorCode = "#ffc107", IsDefault = true },
                    new Category { Name = "Other", Type = "Expense", Icon = "tag", ColorCode = "#6c757d", IsDefault = true },
                    new Category { Name = "Salary", Type = "Income", Icon = "briefcase", ColorCode = "#28a745", IsDefault = true },
                    new Category { Name = "Freelance & Others", Type = "Income", Icon = "dollar-sign", ColorCode = "#17a2b8", IsDefault = true }
                };

                await context.Categories.AddRangeAsync(categories);
                await context.SaveChangesAsync();

                // Seed Keywords for Quick Entry
                var food = categories.First(c => c.Name == "Food");
                var shopping = categories.First(c => c.Name == "Shopping");
                var transport = categories.First(c => c.Name == "Transport");
                var bills = categories.First(c => c.Name == "Bills");
                var entertainment = categories.First(c => c.Name == "Entertainment");
                var healthcare = categories.First(c => c.Name == "Healthcare");
                var education = categories.First(c => c.Name == "Education");
                var travel = categories.First(c => c.Name == "Travel");
                var investment = categories.First(c => c.Name == "Investment");

                var keywords = new List<CategoryKeyword>
                {
                    // Food
                    new() { CategoryId = food.CategoryId, Keyword = "suji" },
                    new() { CategoryId = food.CategoryId, Keyword = "rice" },
                    new() { CategoryId = food.CategoryId, Keyword = "lunch" },
                    new() { CategoryId = food.CategoryId, Keyword = "dinner" },
                    new() { CategoryId = food.CategoryId, Keyword = "breakfast" },
                    new() { CategoryId = food.CategoryId, Keyword = "swiggy" },
                    new() { CategoryId = food.CategoryId, Keyword = "zomato" },
                    new() { CategoryId = food.CategoryId, Keyword = "milk" },
                    new() { CategoryId = food.CategoryId, Keyword = "bread" },
                    new() { CategoryId = food.CategoryId, Keyword = "tea" },
                    new() { CategoryId = food.CategoryId, Keyword = "coffee" },
                    new() { CategoryId = food.CategoryId, Keyword = "burger" },
                    new() { CategoryId = food.CategoryId, Keyword = "pizza" },
                    new() { CategoryId = food.CategoryId, Keyword = "groceries" },
                    new() { CategoryId = food.CategoryId, Keyword = "fruits" },
                    new() { CategoryId = food.CategoryId, Keyword = "vegetables" },

                    // Shopping
                    new() { CategoryId = shopping.CategoryId, Keyword = "amazon" },
                    new() { CategoryId = shopping.CategoryId, Keyword = "flipkart" },
                    new() { CategoryId = shopping.CategoryId, Keyword = "myntra" },
                    new() { CategoryId = shopping.CategoryId, Keyword = "shirt" },
                    new() { CategoryId = shopping.CategoryId, Keyword = "shoes" },
                    new() { CategoryId = shopping.CategoryId, Keyword = "jeans" },
                    new() { CategoryId = shopping.CategoryId, Keyword = "tshirt" },
                    new() { CategoryId = shopping.CategoryId, Keyword = "clothes" },

                    // Transport
                    new() { CategoryId = transport.CategoryId, Keyword = "uber" },
                    new() { CategoryId = transport.CategoryId, Keyword = "ola" },
                    new() { CategoryId = transport.CategoryId, Keyword = "rapido" },
                    new() { CategoryId = transport.CategoryId, Keyword = "petrol" },
                    new() { CategoryId = transport.CategoryId, Keyword = "diesel" },
                    new() { CategoryId = transport.CategoryId, Keyword = "fuel" },
                    new() { CategoryId = transport.CategoryId, Keyword = "auto" },
                    new() { CategoryId = transport.CategoryId, Keyword = "metro" },
                    new() { CategoryId = transport.CategoryId, Keyword = "bus" },

                    // Bills
                    new() { CategoryId = bills.CategoryId, Keyword = "electricity" },
                    new() { CategoryId = bills.CategoryId, Keyword = "wifi" },
                    new() { CategoryId = bills.CategoryId, Keyword = "water" },
                    new() { CategoryId = bills.CategoryId, Keyword = "recharge" },
                    new() { CategoryId = bills.CategoryId, Keyword = "mobile" },
                    new() { CategoryId = bills.CategoryId, Keyword = "gas" },
                    new() { CategoryId = bills.CategoryId, Keyword = "rent" },

                    // Entertainment
                    new() { CategoryId = entertainment.CategoryId, Keyword = "netflix" },
                    new() { CategoryId = entertainment.CategoryId, Keyword = "prime" },
                    new() { CategoryId = entertainment.CategoryId, Keyword = "spotify" },
                    new() { CategoryId = entertainment.CategoryId, Keyword = "movie" },
                    new() { CategoryId = entertainment.CategoryId, Keyword = "cinema" },
                    new() { CategoryId = entertainment.CategoryId, Keyword = "game" },

                    // Healthcare
                    new() { CategoryId = healthcare.CategoryId, Keyword = "doctor" },
                    new() { CategoryId = healthcare.CategoryId, Keyword = "medicine" },
                    new() { CategoryId = healthcare.CategoryId, Keyword = "pharmacy" },
                    new() { CategoryId = healthcare.CategoryId, Keyword = "hospital" },
                    new() { CategoryId = healthcare.CategoryId, Keyword = "clinic" },

                    // Education
                    new() { CategoryId = education.CategoryId, Keyword = "book" },
                    new() { CategoryId = education.CategoryId, Keyword = "books" },
                    new() { CategoryId = education.CategoryId, Keyword = "course" },
                    new() { CategoryId = education.CategoryId, Keyword = "tuition" },
                    new() { CategoryId = education.CategoryId, Keyword = "fees" },

                    // Travel
                    new() { CategoryId = travel.CategoryId, Keyword = "flight" },
                    new() { CategoryId = travel.CategoryId, Keyword = "hotel" },
                    new() { CategoryId = travel.CategoryId, Keyword = "trip" },
                    new() { CategoryId = travel.CategoryId, Keyword = "train" },

                    // Investment
                    new() { CategoryId = investment.CategoryId, Keyword = "stocks" },
                    new() { CategoryId = investment.CategoryId, Keyword = "mutual fund" },
                    new() { CategoryId = investment.CategoryId, Keyword = "sip" },
                    new() { CategoryId = investment.CategoryId, Keyword = "gold" },
                    new() { CategoryId = investment.CategoryId, Keyword = "crypto" }
                };

                await context.CategoryKeywords.AddRangeAsync(keywords);
                await context.SaveChangesAsync();
            }
        }
    }
}
