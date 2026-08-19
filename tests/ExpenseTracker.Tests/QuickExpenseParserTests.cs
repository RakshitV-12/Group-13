using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpenseTracker.Tests
{
    public class QuickExpenseParserTests
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

        [Theory]
        [InlineData("Suji 250", "Suji", 250, "Food")]
        [InlineData("Rice 500", "Rice", 500, "Food")]
        [InlineData("Amazon 1200", "Amazon", 1200, "Shopping")]
        [InlineData("Shirt 999", "Shirt", 999, "Shopping")]
        [InlineData("Uber 180", "Uber", 180, "Transport")]
        [InlineData("Netflix 649", "Netflix", 649, "Entertainment")]
        [InlineData("Electricity 1450", "Electricity", 1450, "Bills")]
        [InlineData("Doctor 500", "Doctor", 500, "Healthcare")]
        [InlineData("Books 350", "Books", 350, "Education")]
        [InlineData("Stocks 5000", "Stocks", 5000, "Investment")]
        public async Task ParseAsync_ShouldIdentifyCategoryAndAmountCorrectly(string input, string expectedDesc, decimal expectedAmount, string expectedCategory)
        {
            // Arrange
            var context = await CreateSeededDbContextAsync();
            var parser = new QuickExpenseParserService(context);

            // Act
            var result = await parser.ParseAsync(input);

            // Assert
            result.Should().NotBeNull();
            result.Amount.Should().Be(expectedAmount);
            result.CategoryName.Should().Be(expectedCategory);
            result.Description.ToLowerInvariant().Should().Contain(expectedDesc.ToLowerInvariant());
        }

        [Fact]
        public async Task ParseAsync_WithUnmatchedKeyword_ShouldFallbackToOtherCategory()
        {
            var context = await CreateSeededDbContextAsync();
            var parser = new QuickExpenseParserService(context);

            var result = await parser.ParseAsync("RandomWidget 300");

            result.Should().NotBeNull();
            result.Amount.Should().Be(300);
            result.CategoryName.Should().Be("Other");
        }
    }
}
