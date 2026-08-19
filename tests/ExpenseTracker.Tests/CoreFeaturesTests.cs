using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpenseTracker.Tests
{
    public class CoreFeaturesTests
    {
        private async Task<ExpenseTrackerDbContext> CreateSeededDbContextAsync()
        {
            var options = new DbContextOptionsBuilder<ExpenseTrackerDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new ExpenseTrackerDbContext(options);
            await DbInitializer.InitializeAsync(context);
            return context;
        }

        [Fact]
        public async Task TransactionCreation_ManualAndQuick_ShouldPersistCorrectly()
        {
            // Arrange
            var context = await CreateSeededDbContextAsync();
            var parser = new QuickExpenseParserService(context);
            var txService = new TransactionService(context, parser);

            var foodCategory = await context.Categories.FirstAsync(c => c.Name == "Food");

            // 1. Manual Entry
            var manualDto = new CreateTransactionDto
            {
                Amount = 350m,
                CategoryId = foodCategory.CategoryId,
                Type = "Expense",
                PaymentMethod = "UPI",
                Description = "Lunch with friends"
            };
            var manualTx = await txService.CreateManualAsync("user-1", manualDto);
            manualTx.Should().NotBeNull();
            manualTx.Amount.Should().Be(350m);
            manualTx.CategoryName.Should().Be("Food");

            // 2. Quick Entry
            var quickDto = new QuickExpenseDto { Input = "Uber 220", PaymentMethod = "Cash" };
            var quickTx = await txService.CreateQuickAsync("user-1", quickDto);
            quickTx.Should().NotBeNull();
            quickTx.Amount.Should().Be(220m);
            quickTx.CategoryName.Should().Be("Transport");
            quickTx.CreatedTransactionId.Should().NotBeNull();
        }

        [Fact]
        public async Task BudgetCalculation_ShouldDetectThresholdWarningAndExceededLimits()
        {
            // Arrange
            var context = await CreateSeededDbContextAsync();
            var budgetService = new BudgetService(context);
            var foodCat = await context.Categories.FirstAsync(c => c.Name == "Food");
            var now = DateTime.UtcNow;

            // Set Budget: 5,000 with 80% threshold (4,000)
            await budgetService.SetBudgetAsync("user-1", new CreateBudgetDto
            {
                CategoryId = foodCat.CategoryId,
                Amount = 5000m,
                PeriodMonth = now.Month,
                PeriodYear = now.Year,
                ThresholdPercent = 80m
            });

            // Add transactions: 4,200 (84% -> Warning, Not Exceeded)
            context.Transactions.Add(new Transaction
            {
                UserId = "user-1",
                CategoryId = foodCat.CategoryId,
                Amount = 4200m,
                Type = "Expense",
                TransactionDate = new DateTime(now.Year, now.Month, 5)
            });
            await context.SaveChangesAsync();

            // Act
            var statuses = await budgetService.GetBudgetStatusesAsync("user-1", now.Year, now.Month);

            // Assert
            statuses.Should().HaveCount(1);
            var status = statuses[0];
            status.TotalSpent.Should().Be(4200m);
            status.UtilizationPercentage.Should().Be(84.00m);
            status.IsWarning.Should().BeTrue();
            status.IsExceeded.Should().BeFalse();
            status.RemainingBudget.Should().Be(800m);
        }

        [Fact]
        public async Task DashboardCalculation_ShouldCalculateTotalsAnd50_30_20Rule()
        {
            // Arrange
            var context = await CreateSeededDbContextAsync();
            var budgetService = new BudgetService(context);
            var dashboardService = new DashboardService(context, budgetService);

            var salaryCat = await context.Categories.FirstAsync(c => c.Name == "Salary");
            var foodCat = await context.Categories.FirstAsync(c => c.Name == "Food");
            var shoppingCat = await context.Categories.FirstAsync(c => c.Name == "Shopping");
            var now = DateTime.UtcNow;

            // Income: 50,000 | Needs (Food): 15,000 (30%) | Wants (Shopping): 10,000 (20%) | Savings: 25,000 (50%)
            context.Transactions.AddRange(
                new Transaction { UserId = "user-1", CategoryId = salaryCat.CategoryId, Amount = 50000m, Type = "Income", TransactionDate = new DateTime(now.Year, now.Month, 1) },
                new Transaction { UserId = "user-1", CategoryId = foodCat.CategoryId, Amount = 15000m, Type = "Expense", TransactionDate = new DateTime(now.Year, now.Month, 3) },
                new Transaction { UserId = "user-1", CategoryId = shoppingCat.CategoryId, Amount = 10000m, Type = "Expense", TransactionDate = new DateTime(now.Year, now.Month, 5) }
            );
            await context.SaveChangesAsync();

            // Act
            var summary = await dashboardService.GetSummaryAsync("user-1", now.Year, now.Month);

            // Assert
            summary.TotalIncome.Should().Be(50000m);
            summary.TotalExpenses.Should().Be(25000m);
            summary.TotalSavings.Should().Be(25000m);
            summary.SavingsPercentage.Should().Be(50.00m);

            summary.FinancialRule.NeedsPercentage.Should().Be(30.0m);
            summary.FinancialRule.WantsPercentage.Should().Be(20.0m);
            summary.FinancialRule.SavingsPercentage.Should().Be(50.0m);
            summary.FinancialRule.Status.Should().Be("Excellent");
        }

        [Fact]
        public async Task UserDataIsolation_FR17_UserCannotAccessOtherUserData()
        {
            // Arrange
            var context = await CreateSeededDbContextAsync();
            var parser = new QuickExpenseParserService(context);
            var txService = new TransactionService(context, parser);
            var foodCat = await context.Categories.FirstAsync(c => c.Name == "Food");

            // User A creates transaction
            var txA = await txService.CreateManualAsync("user-A", new CreateTransactionDto
            {
                Amount = 1000m,
                CategoryId = foodCat.CategoryId,
                Type = "Expense",
                Description = "User A Private Grocery"
            });

            // User B lists transactions
            var userBList = await txService.GetTransactionsAsync("user-B", null, null, null, null, null, 1, 20);
            userBList.TotalCount.Should().Be(0);
            userBList.Items.Should().BeEmpty();

            // User B attempts to delete User A's transaction
            var deleteAttempt = await txService.DeleteAsync("user-B", txA.TransactionId);
            deleteAttempt.Should().BeFalse();

            // Confirm User A's transaction is still safe
            var inDb = await context.Transactions.FindAsync(txA.TransactionId);
            inDb.Should().NotBeNull();
        }

        [Fact]
        public async Task AIChatbot_ShouldAnswerRealDatabaseQuestionsWithoutHallucination()
        {
            // Arrange
            var context = await CreateSeededDbContextAsync();
            var chatbot = new AIChatbotService(context);
            var foodCat = await context.Categories.FirstAsync(c => c.Name == "Food");
            var now = DateTime.UtcNow;

            context.Transactions.Add(new Transaction
            {
                UserId = "user-1",
                CategoryId = foodCat.CategoryId,
                Amount = 450m,
                Type = "Expense",
                TransactionDate = new DateTime(now.Year, now.Month, 2)
            });
            await context.SaveChangesAsync();

            // Act
            var foodResponse = await chatbot.AnswerUserQueryAsync("user-1", "How much did I spend on food?");
            var totalResponse = await chatbot.AnswerUserQueryAsync("user-1", "How much did I spend this month?");

            // Assert
            foodResponse.ComputedAmount.Should().Be(450m);
            foodResponse.Answer.Should().Contain("450.00");
            totalResponse.ComputedAmount.Should().Be(450m);
        }
    }
}
