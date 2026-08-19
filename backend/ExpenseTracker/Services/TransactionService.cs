using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services
{
    public class TransactionService
    {
        private readonly ExpenseTrackerDbContext _context;
        private readonly QuickExpenseParserService _parserService;

        public TransactionService(ExpenseTrackerDbContext context, QuickExpenseParserService parserService)
        {
            _context = context;
            _parserService = parserService;
        }

        public async Task<PagedResult<TransactionResponseDto>> GetTransactionsAsync(
            string userId,
            DateTime? startDate,
            DateTime? endDate,
            int? categoryId,
            string? type,
            string? search,
            int page = 1,
            int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            // Strict User Isolation
            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId)
                .AsNoTracking();

            if (startDate.HasValue)
                query = query.Where(t => t.TransactionDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.TransactionDate <= endDate.Value);

            if (categoryId.HasValue)
                query = query.Where(t => t.CategoryId == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(t => t.Type == type);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(t => (t.Description != null && t.Description.ToLower().Contains(s)) ||
                                         t.Category.Name.ToLower().Contains(s));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.TransactionId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionResponseDto
                {
                    TransactionId = t.TransactionId,
                    UserId = t.UserId,
                    Amount = t.Amount,
                    Type = t.Type,
                    CategoryId = t.CategoryId,
                    CategoryName = t.Category.Name,
                    CategoryIcon = t.Category.Icon,
                    CategoryColor = t.Category.ColorCode,
                    TransactionDate = t.TransactionDate,
                    PaymentMethod = t.PaymentMethod,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<TransactionResponseDto>
            {
                Items = items,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<TransactionResponseDto?> GetByIdAsync(string userId, long id)
        {
            return await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.TransactionId == id)
                .Select(t => new TransactionResponseDto
                {
                    TransactionId = t.TransactionId,
                    UserId = t.UserId,
                    Amount = t.Amount,
                    Type = t.Type,
                    CategoryId = t.CategoryId,
                    CategoryName = t.Category.Name,
                    CategoryIcon = t.Category.Icon,
                    CategoryColor = t.Category.ColorCode,
                    TransactionDate = t.TransactionDate,
                    PaymentMethod = t.PaymentMethod,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<TransactionResponseDto> CreateManualAsync(string userId, CreateTransactionDto dto)
        {
            if (dto.Amount <= 0)
                throw new ArgumentException("Amount must be greater than zero.");

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == dto.CategoryId && (c.UserId == null || c.UserId == userId));

            if (category == null)
                throw new KeyNotFoundException("Category not found.");

            var transaction = new Transaction
            {
                UserId = userId,
                CategoryId = dto.CategoryId,
                Amount = dto.Amount,
                Type = dto.Type,
                TransactionDate = dto.TransactionDate == default ? DateTime.UtcNow : dto.TransactionDate,
                PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "UPI" : dto.PaymentMethod,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Transactions.AddAsync(transaction);
            await _context.SaveChangesAsync();

            return new TransactionResponseDto
            {
                TransactionId = transaction.TransactionId,
                UserId = transaction.UserId,
                Amount = transaction.Amount,
                Type = transaction.Type,
                CategoryId = transaction.CategoryId,
                CategoryName = category.Name,
                CategoryIcon = category.Icon,
                CategoryColor = category.ColorCode,
                TransactionDate = transaction.TransactionDate,
                PaymentMethod = transaction.PaymentMethod,
                Description = transaction.Description,
                CreatedAt = transaction.CreatedAt
            };
        }

        public async Task<QuickExpenseParseResultDto> CreateQuickAsync(string userId, QuickExpenseDto dto)
        {
            var parseResult = await _parserService.ParseAsync(dto.Input, dto.PaymentMethod);

            var transaction = new Transaction
            {
                UserId = userId,
                CategoryId = parseResult.CategoryId,
                Amount = parseResult.Amount,
                Type = "Expense",
                TransactionDate = dto.TransactionDate ?? DateTime.UtcNow,
                PaymentMethod = parseResult.PaymentMethod,
                Description = parseResult.Description,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Transactions.AddAsync(transaction);
            await _context.SaveChangesAsync();

            parseResult.CreatedTransactionId = transaction.TransactionId;
            return parseResult;
        }

        public async Task<TransactionResponseDto?> UpdateAsync(string userId, long id, UpdateTransactionDto dto)
        {
            var transaction = await _context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);

            if (transaction == null) return null;

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == dto.CategoryId && (c.UserId == null || c.UserId == userId));

            if (category == null)
                throw new KeyNotFoundException("Category not found.");

            transaction.Amount = dto.Amount;
            transaction.Type = dto.Type;
            transaction.CategoryId = dto.CategoryId;
            transaction.TransactionDate = dto.TransactionDate;
            transaction.PaymentMethod = dto.PaymentMethod;
            transaction.Description = dto.Description;

            await _context.SaveChangesAsync();

            return new TransactionResponseDto
            {
                TransactionId = transaction.TransactionId,
                UserId = transaction.UserId,
                Amount = transaction.Amount,
                Type = transaction.Type,
                CategoryId = transaction.CategoryId,
                CategoryName = category.Name,
                CategoryIcon = category.Icon,
                CategoryColor = category.ColorCode,
                TransactionDate = transaction.TransactionDate,
                PaymentMethod = transaction.PaymentMethod,
                Description = transaction.Description,
                CreatedAt = transaction.CreatedAt
            };
        }

        public async Task<bool> DeleteAsync(string userId, long id)
        {
            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);

            if (transaction == null) return false;

            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
