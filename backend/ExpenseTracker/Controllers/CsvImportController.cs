using ExpenseTracker.DTOs;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    [Route("api/import/csv")]
    public class CsvImportController : BaseApiController
    {
        private readonly CsvImportService _csvImportService;

        public CsvImportController(CsvImportService csvImportService)
        {
            _csvImportService = csvImportService;
        }

        [HttpPost("preview")]
        public async Task<IActionResult> PreviewCsv(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "CSV file is required." });
            }

            var preview = await _csvImportService.PreviewCsvAsync(GetUserId(), file);
            return Ok(preview);
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmImport([FromBody] CsvConfirmImportDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var count = await _csvImportService.ConfirmImportAsync(GetUserId(), dto);
            return Ok(new
            {
                message = $"{count} transactions imported successfully.",
                importedCount = count
            });
        }
    }
}
