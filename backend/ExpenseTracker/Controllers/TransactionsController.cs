using ExpenseTracker.DTOs;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class TransactionsController : BaseApiController
    {
        private readonly TransactionService _transactionService;

        public TransactionsController(TransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int? categoryId,
            [FromQuery] string? type,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _transactionService.GetTransactionsAsync(
                GetUserId(), startDate, endDate, categoryId, type, search, page, pageSize);
            return Ok(result);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var tx = await _transactionService.GetByIdAsync(GetUserId(), id);
            if (tx == null) return NotFound(new { message = $"Transaction with ID {id} not found." });
            return Ok(tx);
        }

        // Option A: Manual Entry
        [HttpPost]
        public async Task<IActionResult> CreateManual([FromBody] CreateTransactionDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _transactionService.CreateManualAsync(GetUserId(), dto);
            return CreatedAtAction(nameof(GetById), new { id = result.TransactionId }, result);
        }

        // Option B: Quick Entry (e.g. "Suji 250")
        [HttpPost("quick")]
        public async Task<IActionResult> CreateQuick([FromBody] QuickExpenseDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _transactionService.CreateQuickAsync(GetUserId(), dto);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateTransactionDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _transactionService.UpdateAsync(GetUserId(), id, dto);
            if (updated == null) return NotFound(new { message = $"Transaction with ID {id} not found." });
            return Ok(updated);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var success = await _transactionService.DeleteAsync(GetUserId(), id);
            if (!success) return NotFound(new { message = $"Transaction with ID {id} not found." });
            return NoContent();
        }
    }
}
