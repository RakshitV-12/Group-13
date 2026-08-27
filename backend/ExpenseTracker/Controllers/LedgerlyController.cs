using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("api/ledgerly")]
public class LedgerlyController : ControllerBase
{
    private readonly string _databasePath;
    private readonly string _bucketRoot;
    private readonly ILogger<LedgerlyController> _logger;

    private static readonly string[] DefaultCategories =
    [
        "Housing","Groceries","Shopping","Dining","Transportation","Utilities","Insurance","Health","Entertainment","Income","Needs review","Other"
    ];

    private static readonly string[] DefaultAccounts =
    [
        "Credit Card","Debit Card","UPI","Cash","Other"
    ];

    public LedgerlyController(IWebHostEnvironment env, ILogger<LedgerlyController> logger)
    {
        _logger = logger;
        var root = Path.Combine(env.ContentRootPath, "ledgerly-data");
        Directory.CreateDirectory(root);
        _databasePath = Path.Combine(root, "ledgerly.db");
        _bucketRoot = Path.Combine(root, "bucket");
        Directory.CreateDirectory(_bucketRoot);
        InitializeDatabase();
        EnsureStructuralSettings();
    }

    [HttpGet("state")]
    public IActionResult GetState()
    {
        var transactions = ReadTransactions();
        var tags = ReadTags();
        var rules = ReadRules();
        var documents = ReadDocuments();
        var settings = ReadSettings();

        return Ok(new
        {
            transactions,
            tags,
            rules,
            settings,
            documents,
            selectedPeriod = settings.TryGetValue("selectedPeriod", out var selected) ? selected?.ToString() ?? "all-time" : "all-time"
        });
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransactions()
    {
        using var document = await JsonDocument.ParseAsync(Request.Body);
        var root = document.RootElement;

        var items = new List<JsonElement>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray()) items.Add(item);
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("transactions", out var batchArray) && batchArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in batchArray.EnumerateArray()) items.Add(item);
            }
            else
            {
                items.Add(root);
            }
        }

        if (items.Count == 0) return BadRequest(new { error = "No transactions were supplied." });

        var insertedRows = new List<object>();
        var duplicateCount = 0;
        var skippedCount = 0;

        foreach (var element in items)
        {
            var item = ParseTransactionInput(element);
            if (!item.Valid)
            {
                skippedCount++;
                continue;
            }

            var fingerprint = BuildFingerprint(item.Date, item.Merchant, item.Amount, item.Account);
            if (TransactionExists(fingerprint))
            {
                duplicateCount++;
                continue;
            }

            var initialCat = string.IsNullOrWhiteSpace(item.Category) ? "Needs review" : item.Category.Trim();
            var finalCat = ApplyCategorizationRules(item.Merchant, initialCat);

            var tx = new
            {
                id = Guid.NewGuid().ToString("N"),
                date = item.Date,
                merchant = item.Merchant.Trim(),
                category = finalCat,
                amount = item.Amount,
                type = item.Type,
                account = string.IsNullOrWhiteSpace(item.Account) ? "Imported account" : item.Account.Trim(),
                tags = JsonSerializer.Serialize(item.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList()),
                receipt = item.Receipt ? 1 : 0,
                source = string.IsNullOrWhiteSpace(item.Source) ? "manual" : item.Source.Trim(),
                fingerprint = fingerprint,
                createdAt = DateTime.UtcNow.ToString("o")
            };

            SaveTransaction(tx);
            foreach (var tag in item.Tags) UpsertTag(tag);
            insertedRows.Add(new
            {
                id = tx.id,
                date = tx.date,
                merchant = tx.merchant,
                category = tx.category,
                amount = tx.amount,
                type = tx.type,
                account = tx.account,
                tags = item.Tags,
                source = tx.source
            });
        }

        return Ok(new
        {
            inserted = insertedRows.Count,
            duplicates = duplicateCount,
            skipped = skippedCount,
            rows = insertedRows
        });
    }

    [HttpPatch("transactions/{id}")]
    public IActionResult UpdateTransaction(string id, [FromBody] JsonElement body)
    {
        using var connection = OpenConnection();
        var currentRow = ReadSingleTransaction(connection, id);
        if (currentRow == null)
        {
            return NotFound(new { error = "Transaction not found." });
        }

        var updatedCategory = (string?)currentRow["category"] ?? "Needs review";
        var updatedTags = currentRow["tags"] as List<string> ?? ParseTagList(currentRow["tags"] is string raw ? raw : "[]");

        if (body.TryGetProperty("category", out var categoryElement) && categoryElement.ValueKind != JsonValueKind.Null)
        {
            updatedCategory = categoryElement.GetString() ?? updatedCategory;
        }

        if (body.TryGetProperty("tags", out var tagsElement))
        {
            if (tagsElement.ValueKind == JsonValueKind.Array)
            {
                updatedTags = tagsElement.EnumerateArray()
                    .Select(x => x.GetString() ?? "")
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        var update = new SqliteCommand(
            "UPDATE transactions SET category=@category, tags=@tags WHERE id=@id",
            connection);
        update.Parameters.AddWithValue("@category", updatedCategory);
        update.Parameters.AddWithValue("@tags", JsonSerializer.Serialize(updatedTags));
        update.Parameters.AddWithValue("@id", id);
        update.ExecuteNonQuery();

        foreach (var tag in updatedTags) UpsertTag(tag);

        var updated = ReadSingleTransaction(connection, id);
        return Ok(updated);
    }

    [HttpDelete("transactions/{id}")]
    public IActionResult DeleteTransaction(string id)
    {
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand("DELETE FROM transactions WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        return Ok(new { success = true, deletedId = id });
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> SavePreferences()
    {
        using var document = await JsonDocument.ParseAsync(Request.Body);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return BadRequest(new { error = "Preferences payload must be an object." });
        }

        foreach (var property in root.EnumerateObject())
        {
            var key = property.Name;
            var value = property.Value;
            if (value.ValueKind == JsonValueKind.Object || value.ValueKind == JsonValueKind.Array)
            {
                SaveSetting(key, value.GetRawText());

                if (string.Equals(key, "rules", StringComparison.OrdinalIgnoreCase) && value.ValueKind == JsonValueKind.Array)
                {
                    SyncRulesTable(value);
                }
                else if (string.Equals(key, "tags", StringComparison.OrdinalIgnoreCase) && value.ValueKind == JsonValueKind.Array)
                {
                    SyncTagsTable(value);
                }
            }
            else if (value.ValueKind == JsonValueKind.String)
            {
                SaveSetting(key, value.GetString() ?? "");
            }
            else if (value.ValueKind == JsonValueKind.Number)
            {
                SaveSetting(key, value.ToString());
            }
            else if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            {
                SaveSetting(key, value.GetBoolean().ToString().ToLowerInvariant());
            }
            else if (value.ValueKind == JsonValueKind.Null)
            {
                SaveSetting(key, "");
            }
        }

        return Ok(new { success = true, saved = root.EnumerateObject().Count() });
    }

    [HttpPost("rules")]
    public IActionResult CreateRule([FromBody] JsonElement body)
    {
        var whenText = body.TryGetProperty("whenText", out var w) ? w.GetString() ?? "" : "";
        var thenText = body.TryGetProperty("thenText", out var t) ? t.GetString() ?? "" : "";
        var enabled = !body.TryGetProperty("enabled", out var e) || e.GetBoolean();
        var id = body.TryGetProperty("id", out var idElem) ? idElem.GetString() ?? $"rule_{Guid.NewGuid():N}" : $"rule_{Guid.NewGuid():N}";

        if (string.IsNullOrWhiteSpace(whenText) || string.IsNullOrWhiteSpace(thenText))
        {
            return BadRequest(new { error = "Both 'When' and 'Then' texts are required." });
        }

        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(
            "INSERT INTO rules (id, whenText, thenText, enabled, createdAt) VALUES (@id, @whenText, @thenText, @enabled, @createdAt) ON CONFLICT(id) DO UPDATE SET whenText=@whenText, thenText=@thenText, enabled=@enabled",
            conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@whenText", whenText.Trim());
        cmd.Parameters.AddWithValue("@thenText", thenText.Trim());
        cmd.Parameters.AddWithValue("@enabled", enabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();

        return Ok(new { id, whenText, thenText, enabled, createdAt = DateTime.UtcNow.ToString("o") });
    }

    [HttpPut("rules/{id}")]
    public IActionResult UpdateRule(string id, [FromBody] JsonElement body)
    {
        using var conn = OpenConnection();
        string? whenText = body.TryGetProperty("whenText", out var w) ? w.GetString() : null;
        string? thenText = body.TryGetProperty("thenText", out var t) ? t.GetString() : null;
        bool? enabled = body.TryGetProperty("enabled", out var e) ? e.GetBoolean() : null;

        using var cmd = new SqliteCommand("UPDATE rules SET whenText=COALESCE(@whenText, whenText), thenText=COALESCE(@thenText, thenText), enabled=COALESCE(@enabled, enabled) WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@whenText", (object?)whenText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@thenText", (object?)thenText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@enabled", enabled.HasValue ? (enabled.Value ? 1 : 0) : DBNull.Value);
        var count = cmd.ExecuteNonQuery();
        if (count == 0) return NotFound(new { error = "Rule not found." });
        return Ok(new { success = true, id });
    }

    [HttpDelete("rules/{id}")]
    public IActionResult DeleteRule(string id)
    {
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand("DELETE FROM rules WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        return Ok(new { success = true, deletedId = id });
    }

    [HttpPost("tags")]
    public IActionResult CreateTag([FromBody] JsonElement body)
    {
        var name = body.TryGetProperty("name", out var n) ? n.GetString() : null;
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { error = "Tag name is required." });
        UpsertTag(name);
        return Ok(new { name = name.Trim(), createdAt = DateTime.UtcNow.ToString("o") });
    }

    [HttpDelete("tags/{name}")]
    public IActionResult DeleteTag(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { error = "Tag name is required." });
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand("DELETE FROM tags WHERE name=@name", conn);
        cmd.Parameters.AddWithValue("@name", name.Trim());
        cmd.ExecuteNonQuery();
        return Ok(new { success = true, deletedTag = name });
    }

    [HttpPost("chat")]
    public IActionResult ChatQuery([FromBody] JsonElement body)
    {
        var message = body.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(message)) return BadRequest(new { error = "Message cannot be empty." });

        var transactions = ReadTransactions();
        var rules = ReadRules();

        var totalTx = transactions.Count;
        double totalExpenses = 0;
        double totalIncome = 0;
        int needsReview = 0;

        foreach (var row in transactions)
        {
            var dict = row.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(row));
            var type = dict.TryGetValue("type", out var t) ? t?.ToString() : "expense";
            var amount = dict.TryGetValue("amount", out var a) ? Convert.ToDouble(a) : 0;
            var cat = dict.TryGetValue("category", out var c) ? c?.ToString() : "";

            if (type == "income") totalIncome += amount;
            else totalExpenses += amount;

            if (string.Equals(cat, "Needs review", StringComparison.OrdinalIgnoreCase)) needsReview++;
        }

        string responseText;
        var lower = message.ToLowerInvariant();
        if (lower.Contains("total") || lower.Contains("balance") || lower.Contains("summary"))
        {
            responseText = $"You currently have **{totalTx} total transactions**. Total Income is **₹{totalIncome:N2}**, Total Expenses are **₹{totalExpenses:N2}**, and Net Cash Flow is **₹{(totalIncome - totalExpenses):N2}**.";
        }
        else if (lower.Contains("review") || lower.Contains("uncategorized"))
        {
            responseText = $"You have **{needsReview} transactions needing review**. Visit the Transactions tab to categorize them!";
        }
        else if (lower.Contains("rule"))
        {
            responseText = $"You have **{rules.Count} categorization rules** configured to automate transaction categories.";
        }
        else
        {
            responseText = $"Based on your financial records: You have **{totalTx} transactions** logged with total income of **₹{totalIncome:N2}** and expenses of **₹{totalExpenses:N2}**. Net savings stands at **₹{(totalIncome - totalExpenses):N2}**.";
        }

        return Ok(new { answer = responseText });
    }

    private void SyncRulesTable(JsonElement rulesArray)
    {
        using var conn = OpenConnection();
        using var deleteCmd = new SqliteCommand("DELETE FROM rules", conn);
        deleteCmd.ExecuteNonQuery();

        foreach (var item in rulesArray.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idElem) ? idElem.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            var whenText = item.TryGetProperty("whenText", out var w) ? w.GetString() ?? "" : "";
            var thenText = item.TryGetProperty("thenText", out var t) ? t.GetString() ?? "" : "";
            var enabled = !item.TryGetProperty("enabled", out var e) || e.GetBoolean();
            var createdAt = item.TryGetProperty("createdAt", out var c) ? c.GetString() ?? DateTime.UtcNow.ToString("o") : DateTime.UtcNow.ToString("o");

            if (!string.IsNullOrWhiteSpace(whenText) && !string.IsNullOrWhiteSpace(thenText))
            {
                using var insertCmd = new SqliteCommand("INSERT INTO rules (id, whenText, thenText, enabled, createdAt) VALUES (@id, @whenText, @thenText, @enabled, @createdAt)", conn);
                insertCmd.Parameters.AddWithValue("@id", id);
                insertCmd.Parameters.AddWithValue("@whenText", whenText.Trim());
                insertCmd.Parameters.AddWithValue("@thenText", thenText.Trim());
                insertCmd.Parameters.AddWithValue("@enabled", enabled ? 1 : 0);
                insertCmd.Parameters.AddWithValue("@createdAt", createdAt);
                insertCmd.ExecuteNonQuery();
            }
        }
    }

    private void SyncTagsTable(JsonElement tagsArray)
    {
        foreach (var item in tagsArray.EnumerateArray())
        {
            var name = item.ValueKind == JsonValueKind.String ? item.GetString() : item.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (!string.IsNullOrWhiteSpace(name)) UpsertTag(name);
        }
    }

    [HttpPost("documents")]
    public async Task<IActionResult> UploadDocuments()
    {
        var form = await Request.ReadFormAsync();
        var files = form.Files;
        if (files.Count == 0) return BadRequest(new { error = "No files were provided." });

        var stored = new List<object>();
        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            if (file.Length > 20 * 1024 * 1024)
            {
                return BadRequest(new { error = $"{file.FileName} exceeds the 20 MB limit." });
            }

            var safeName = MakeSafeFileName(file.FileName);
            var objectKey = $"uploads/{Guid.NewGuid():N}-{safeName}";
            var filePath = Path.Combine(_bucketRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            await using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            var id = Guid.NewGuid().ToString("N");
            var record = new
            {
                id,
                filename = file.FileName,
                mimeType = file.ContentType ?? "application/octet-stream",
                size = file.Length,
                objectKey,
                status = "stored",
                source = "upload",
                createdAt = DateTime.UtcNow.ToString("o")
            };

            using var conn = OpenConnection();
            using var cmd = new SqliteCommand(
                "INSERT INTO documents (id, filename, mimeType, size, objectKey, status, source, createdAt) VALUES (@id,@filename,@mimeType,@size,@objectKey,@status,@source,@createdAt)",
                conn);
            cmd.Parameters.AddWithValue("@id", record.id);
            cmd.Parameters.AddWithValue("@filename", record.filename);
            cmd.Parameters.AddWithValue("@mimeType", record.mimeType);
            cmd.Parameters.AddWithValue("@size", record.size);
            cmd.Parameters.AddWithValue("@objectKey", record.objectKey);
            cmd.Parameters.AddWithValue("@status", record.status);
            cmd.Parameters.AddWithValue("@source", record.source);
            cmd.Parameters.AddWithValue("@createdAt", record.createdAt);
            cmd.ExecuteNonQuery();

            stored.Add(record);
        }

        return Ok(new { success = true, files = stored });
    }

    [HttpDelete("state")]
    public IActionResult ResetState([FromBody] JsonElement body)
    {
        var confirmation = body.ValueKind == JsonValueKind.String
            ? body.GetString()
            : body.TryGetProperty("confirmation", out var c) ? c.GetString() : null;

        if (confirmation != "DELETE ALL LEDGERLY DATA")
        {
            return BadRequest(new { error = "The exact confirmation string is required." });
        }

        using (var conn = OpenConnection())
        {
            using (var cmd = new SqliteCommand("DELETE FROM transactions", conn)) cmd.ExecuteNonQuery();
            using (var cmd = new SqliteCommand("DELETE FROM documents", conn)) cmd.ExecuteNonQuery();
            using (var cmd = new SqliteCommand("DELETE FROM rules", conn)) cmd.ExecuteNonQuery();
            using (var cmd = new SqliteCommand("DELETE FROM tags", conn)) cmd.ExecuteNonQuery();
            using (var cmd = new SqliteCommand("DELETE FROM settings", conn)) cmd.ExecuteNonQuery();
        }

        DeleteAllFilesInBucket();

        var now = DateTime.UtcNow.ToString("o");
        SaveSetting("freshStart", true);
        SaveSetting("driveResetAt", now);
        SaveSetting("assetsTotal", 0m);
        SaveSetting("liabilitiesTotal", 0m);
        SaveSetting("netWorthConfigured", false);
        SaveSetting("selectedPeriod", "all-time");
        SaveSetting("currency", "INR");
        SaveSetting("categories", DefaultCategories);
        SaveSetting("accounts", DefaultAccounts);
        SaveSetting("goals", Array.Empty<object>());
        SaveSetting("budgets", Array.Empty<object>());
        SaveSetting("recurring", Array.Empty<object>());
        SaveSetting("dismissedPatterns", Array.Empty<object>());

        return Ok(new { success = true, message = "Ledgerly data was wiped successfully." });
    }

    [HttpGet("drive-sync")]
    public IActionResult GetDriveSync()
    {
        var folderName = ReadSetting("driveFolderName") ?? "Ledgerly Financial Inbox";
        var folderId = ReadSetting("driveFolderId") ?? "not-set";
        var folderUrl = ReadSetting("driveFolderUrl") ?? "";
        var lastSyncAt = ReadSetting("driveLastSyncedAt");
        var status = ReadSetting("driveSyncStatus") ?? "idle";
        var imported = ReadSetting("driveImportedCount") ?? "0";
        var duplicates = ReadSetting("driveDuplicateCount") ?? "0";
        var review = ReadSetting("driveReviewCount") ?? "0";
        var errors = ReadSetting("driveErrors") ?? "";
        var processed = ReadJsonSetting<List<string>>("processedFileIds", new List<string>());

        return Ok(new
        {
            folder = new
            {
                id = folderId,
                name = folderName,
                url = folderUrl
            },
            schedule = new
            {
                time = "08:00",
                timezone = TimeZoneInfo.Local.Id,
                cadence = "daily"
            },
            lastSyncAt = lastSyncAt,
            status = status,
            imported = int.TryParse(imported, out var i) ? i : 0,
            duplicates = int.TryParse(duplicates, out var d) ? d : 0,
            review = int.TryParse(review, out var r) ? r : 0,
            errors = errors,
            processedFileIds = processed.Take(5000).ToList(),
            resetAt = ReadSetting("driveResetAt")
        });
    }

    [HttpPost("drive-sync")]
    public async Task<IActionResult> PostDriveSync()
    {
        using var document = await JsonDocument.ParseAsync(Request.Body);
        var root = document.RootElement;
        var importedTransactions = 0;
        var duplicatesSkipped = 0;
        var filesStored = 0;
        var fileReview = 0;
        var errors = new List<string>();
        var processedIds = ReadJsonSetting<List<string>>("processedFileIds", new List<string>());

        if (root.TryGetProperty("transactions", out var txList) && txList.ValueKind == JsonValueKind.Array)
        {
            foreach (var tx in txList.EnumerateArray())
            {
                var parsed = ParseTransactionInput(tx);
                if (!parsed.Valid)
                {
                    errors.Add("A Drive transaction was skipped because required values were missing.");
                    continue;
                }

                var fingerprint = BuildFingerprint(parsed.Date, parsed.Merchant, parsed.Amount, parsed.Account);
                if (TransactionExists(fingerprint))
                {
                    duplicatesSkipped++;
                    continue;
                }

                var row = new
                {
                    id = Guid.NewGuid().ToString("N"),
                    date = parsed.Date,
                    merchant = parsed.Merchant.Trim(),
                    category = string.IsNullOrWhiteSpace(parsed.Category) ? "Needs review" : parsed.Category.Trim(),
                    amount = parsed.Amount,
                    type = parsed.Type,
                    account = string.IsNullOrWhiteSpace(parsed.Account) ? "Drive import" : parsed.Account.Trim(),
                    tags = JsonSerializer.Serialize(new[] { "Drive import" }.Concat(parsed.Tags).Distinct(StringComparer.OrdinalIgnoreCase).ToList()),
                    receipt = parsed.Receipt ? 1 : 0,
                    source = "google-drive",
                    fingerprint = fingerprint,
                    createdAt = DateTime.UtcNow.ToString("o")
                };

                SaveTransaction(row);
                importedTransactions++;
            }
        }

        if (root.TryGetProperty("files", out var filesList) && filesList.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in filesList.EnumerateArray())
            {
                var id = file.TryGetProperty("id", out var fileId) ? fileId.GetString() : null;
                var name = file.TryGetProperty("filename", out var fileName) ? fileName.GetString() ?? "document" : "document";
                var mimeType = file.TryGetProperty("mimeType", out var mime) ? mime.GetString() ?? "application/octet-stream" : "application/octet-stream";
                var modifiedAt = file.TryGetProperty("modifiedTime", out var mod) ? mod.GetString() : DateTime.UtcNow.ToString("o");
                var status = file.TryGetProperty("status", out var statusValue) ? statusValue.GetString() ?? "stored" : "stored";
                var base64Content = file.TryGetProperty("content", out var content) ? content.GetString() : null;

                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add("One Drive file was missing an ID and was skipped.");
                    continue;
                }

                if (processedIds.Contains(id))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(base64Content))
                {
                    var raw = Convert.FromBase64String(base64Content);
                    if (raw.Length > 20 * 1024 * 1024)
                    {
                        errors.Add($"{name} exceeded the 20 MB limit and was not stored.");
                        continue;
                    }

                    var safeName = MakeSafeFileName(name);
                    var objectKey = $"drive-inbox/{id}-{safeName}";
                    var filePath = Path.Combine(_bucketRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                    await System.IO.File.WriteAllBytesAsync(filePath, raw);

                    using var conn = OpenConnection();
                    var cmd = new SqliteCommand(
                        "INSERT INTO documents (id, filename, mimeType, size, objectKey, status, source, createdAt) VALUES (@id,@filename,@mimeType,@size,@objectKey,@status,@source,@createdAt)",
                        conn);
                    cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
                    cmd.Parameters.AddWithValue("@filename", name);
                    cmd.Parameters.AddWithValue("@mimeType", mimeType);
                    cmd.Parameters.AddWithValue("@size", raw.Length);
                    cmd.Parameters.AddWithValue("@objectKey", objectKey);
                    cmd.Parameters.AddWithValue("@status", status == "review" ? "review" : "stored");
                    cmd.Parameters.AddWithValue("@source", "google-drive");
                    cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
                    cmd.ExecuteNonQuery();

                    filesStored++;
                    processedIds.Add(id);
                    SaveSetting("processedFileIds", processedIds.Take(5000).ToList());
                }
                else if (status == "review")
                {
                    fileReview++;
                }
            }
        }

        var now = DateTime.UtcNow.ToString("o");
        SaveSetting("driveLastSyncedAt", now);
        SaveSetting("driveSyncStatus", importedTransactions > 0 || filesStored > 0 ? "complete" : "idle");
        SaveSetting("driveImportedCount", int.TryParse(ReadSetting("driveImportedCount") ?? "0", out var existingImported) ? existingImported + importedTransactions : importedTransactions);
        SaveSetting("driveDuplicateCount", int.TryParse(ReadSetting("driveDuplicateCount") ?? "0", out var existingDup) ? existingDup + duplicatesSkipped : duplicatesSkipped);
        SaveSetting("driveReviewCount", int.TryParse(ReadSetting("driveReviewCount") ?? "0", out var existingReview) ? existingReview + fileReview : fileReview);
        SaveSetting("driveErrors", string.Join("; ", errors));

        return Ok(new
        {
            status = errors.Count == 0 ? "complete" : "partial",
            lastSyncedAt = now,
            transactionsImported = importedTransactions,
            duplicatesSkipped,
            filesStored,
            filesNeedingReview = fileReview,
            errors
        });
    }

    private void InitializeDatabase()
    {
        using var conn = OpenConnection();
        using var command = new SqliteCommand(@"
            CREATE TABLE IF NOT EXISTS transactions (
                id TEXT PRIMARY KEY,
                date TEXT NOT NULL,
                merchant TEXT NOT NULL,
                category TEXT NOT NULL DEFAULT 'Needs review',
                amount REAL NOT NULL,
                type TEXT NOT NULL,
                account TEXT NOT NULL DEFAULT 'Imported account',
                tags TEXT NOT NULL DEFAULT '[]',
                receipt INTEGER NOT NULL DEFAULT 0,
                source TEXT NOT NULL,
                fingerprint TEXT NOT NULL UNIQUE,
                createdAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS tags (
                name TEXT PRIMARY KEY,
                createdAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS rules (
                id TEXT PRIMARY KEY,
                whenText TEXT NOT NULL,
                thenText TEXT NOT NULL,
                enabled INTEGER NOT NULL DEFAULT 1,
                createdAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS documents (
                id TEXT PRIMARY KEY,
                filename TEXT NOT NULL,
                mimeType TEXT NOT NULL,
                size INTEGER NOT NULL,
                objectKey TEXT NOT NULL UNIQUE,
                status TEXT NOT NULL,
                source TEXT NOT NULL,
                createdAt TEXT NOT NULL
            );
        ", conn);
        command.ExecuteNonQuery();
    }

    private void EnsureStructuralSettings()
    {
        var now = DateTime.UtcNow.ToString("o");
        SaveSetting("categories", DefaultCategories);
        SaveSetting("accounts", DefaultAccounts);
        SaveSetting("tags", Array.Empty<object>());
        SaveSetting("goals", Array.Empty<object>());
        SaveSetting("budgets", Array.Empty<object>());
        SaveSetting("recurring", Array.Empty<object>());
        SaveSetting("dismissedPatterns", Array.Empty<object>());
        SaveSetting("assetsTotal", 0m);
        SaveSetting("liabilitiesTotal", 0m);
        SaveSetting("netWorthConfigured", false);
        SaveSetting("selectedPeriod", "all-time");
        SaveSetting("freshStart", true);
        SaveSetting("driveResetAt", now);
        SaveSetting("currency", "INR");
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }

    private void SaveSetting(string key, object value)
    {
        var json = value is string s ? s : JsonSerializer.Serialize(value);
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(
            "INSERT INTO settings(key, value, updatedAt) VALUES(@key,@value,@updatedAt) ON CONFLICT(key) DO UPDATE SET value=@value, updatedAt=@updatedAt",
            conn);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", json);
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private string? ReadSetting(string key)
    {
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand("SELECT value FROM settings WHERE key=@key", conn);
        cmd.Parameters.AddWithValue("@key", key);
        var result = cmd.ExecuteScalar();
        return result == null ? null : result.ToString();
    }

    private T ReadJsonSetting<T>(string key, T fallback)
    {
        var value = ReadSetting(key);
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        try
        {
            return JsonSerializer.Deserialize<T>(value) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private Dictionary<string, object?> ReadSettings()
    {
        var settings = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand("SELECT key, value FROM settings ORDER BY key ASC", conn);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var key = reader.GetString(0);
            var value = reader.GetString(1);
            settings[key] = TryParseJson(value, out var jsonValue) ? jsonValue : value;
        }

        return settings;
    }

    private static bool TryParseJson(string raw, out object? value)
    {
        try
        {
            var element = JsonDocument.Parse(raw);
            value = element.RootElement.Clone();
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private List<object> ReadTransactions()
    {
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand("SELECT * FROM transactions ORDER BY date DESC, createdAt DESC LIMIT 5000", conn);
        using var reader = cmd.ExecuteReader();

        var rows = new List<object>();
        while (reader.Read())
        {
            rows.Add(new
            {
                id = reader.GetString(0),
                date = reader.GetString(1),
                merchant = reader.GetString(2),
                category = reader.GetString(3),
                amount = reader.GetDouble(4),
                type = reader.GetString(5),
                account = reader.GetString(6),
                tags = ParseTagList(reader.GetString(7)),
                receipt = reader.GetInt32(8) == 1,
                source = reader.GetString(9),
                fingerprint = reader.GetString(10),
                createdAt = reader.GetString(11)
            });
        }
        return rows;
    }

    private Dictionary<string, object?>? ReadSingleTransaction(SqliteConnection conn, string id)
    {
        using var cmd = new SqliteCommand("SELECT * FROM transactions WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = reader.GetString(0),
            ["date"] = reader.GetString(1),
            ["merchant"] = reader.GetString(2),
            ["category"] = reader.GetString(3),
            ["amount"] = reader.GetDouble(4),
            ["type"] = reader.GetString(5),
            ["account"] = reader.GetString(6),
            ["tags"] = ParseTagList(reader.GetString(7)),
            ["receipt"] = reader.GetInt32(8) == 1,
            ["source"] = reader.GetString(9),
            ["fingerprint"] = reader.GetString(10),
            ["createdAt"] = reader.GetString(11)
        };

        return row;
    }

    private List<object> ReadTags()
    {
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand("SELECT name, createdAt FROM tags ORDER BY name ASC", conn);
        using var reader = cmd.ExecuteReader();
        var output = new List<object>();
        while (reader.Read())
        {
            output.Add(new { name = reader.GetString(0), createdAt = reader.GetString(1) });
        }
        return output;
    }

    private List<object> ReadRules()
    {
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand("SELECT id, whenText, thenText, enabled, createdAt FROM rules ORDER BY createdAt DESC", conn);
        using var reader = cmd.ExecuteReader();
        var output = new List<object>();
        while (reader.Read())
        {
            output.Add(new
            {
                id = reader.GetString(0),
                whenText = reader.GetString(1),
                thenText = reader.GetString(2),
                enabled = reader.GetInt32(3) == 1,
                createdAt = reader.GetString(4)
            });
        }
        return output;
    }

    private List<object> ReadDocuments()
    {
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand("SELECT id, filename, mimeType, size, objectKey, status, source, createdAt FROM documents ORDER BY createdAt DESC LIMIT 100", conn);
        using var reader = cmd.ExecuteReader();
        var output = new List<object>();
        while (reader.Read())
        {
            output.Add(new
            {
                id = reader.GetString(0),
                filename = reader.GetString(1),
                mimeType = reader.GetString(2),
                size = reader.GetInt64(3),
                objectKey = reader.GetString(4),
                status = reader.GetString(5),
                source = reader.GetString(6),
                createdAt = reader.GetString(7)
            });
        }
        return output;
    }

    private void SaveTransaction(object row)
    {
        var data = row.GetType().GetProperties();
        var parameters = new Dictionary<string, object>();
        foreach (var property in data)
        {
            parameters[property.Name] = property.GetValue(row)!;
        }

        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(
            "INSERT INTO transactions(id,date,merchant,category,amount,type,account,tags,receipt,source,fingerprint,createdAt) VALUES(@id,@date,@merchant,@category,@amount,@type,@account,@tags,@receipt,@source,@fingerprint,@createdAt)",
            conn);
        cmd.Parameters.AddWithValue("@id", parameters["id"]);
        cmd.Parameters.AddWithValue("@date", parameters["date"]);
        cmd.Parameters.AddWithValue("@merchant", parameters["merchant"]);
        cmd.Parameters.AddWithValue("@category", parameters["category"]);
        cmd.Parameters.AddWithValue("@amount", Convert.ToDouble(parameters["amount"]));
        cmd.Parameters.AddWithValue("@type", parameters["type"]);
        cmd.Parameters.AddWithValue("@account", parameters["account"]);
        cmd.Parameters.AddWithValue("@tags", parameters["tags"]);
        cmd.Parameters.AddWithValue("@receipt", (int)Convert.ToInt64(parameters["receipt"]));
        cmd.Parameters.AddWithValue("@source", parameters["source"]);
        cmd.Parameters.AddWithValue("@fingerprint", parameters["fingerprint"]);
        cmd.Parameters.AddWithValue("@createdAt", parameters["createdAt"]);
        cmd.ExecuteNonQuery();
    }

    private void UpsertTag(string tag)
    {
        var clean = tag.Trim();
        if (string.IsNullOrWhiteSpace(clean)) return;
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(
            "INSERT INTO tags(name, createdAt) VALUES(@name,@createdAt) ON CONFLICT(name) DO NOTHING",
            conn);
        cmd.Parameters.AddWithValue("@name", clean);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private bool TransactionExists(string fingerprint)
    {
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand("SELECT COUNT(1) FROM transactions WHERE fingerprint=@fingerprint", conn);
        cmd.Parameters.AddWithValue("@fingerprint", fingerprint);
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        return count > 0;
    }

    private static string BuildFingerprint(string date, string merchant, decimal amount, string account)
    {
        var normalizedDate = date.Trim();
        var normalizedMerchant = merchant.Trim();
        var normalizedAccount = account.Trim();
        var amountText = amount.ToString("0.00", CultureInfo.InvariantCulture);
        return $"{normalizedDate}|{normalizedMerchant.ToLowerInvariant()}|{amountText}|{normalizedAccount.ToLowerInvariant()}";
    }

    private static List<string> ParseTagList(string raw)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string MakeSafeFileName(string fileName)
    {
        var safe = Path.GetFileName(fileName).Trim();
        safe = safe.Replace(" ", "-");
        safe = new string(safe.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '_').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "uploaded-document" : safe;
    }

    private void DeleteAllFilesInBucket()
    {
        if (!System.IO.Directory.Exists(_bucketRoot)) return;
        foreach (var file in System.IO.Directory.GetFiles(_bucketRoot, "*", SearchOption.AllDirectories))
        {
            try
            {
                System.IO.File.Delete(file);
            }
            catch
            {
                // Best effort local cleanup.
            }
        }
    }

    private static TransactionParseResult ParseTransactionInput(JsonElement element)
    {
        var merchant = element.TryGetProperty("merchant", out var merchantElement) ? merchantElement.GetString() : (element.TryGetProperty("description", out var desc) ? desc.GetString() : null);
        var source = element.TryGetProperty("source", out var sourceElement) ? sourceElement.GetString() : "manual";
        var date = element.TryGetProperty("date", out var dateElement) ? dateElement.GetString() : null;
        var type = element.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : "expense";
        JsonElement? amountValue = null;
        if (element.TryGetProperty("amount", out var amountElement)) amountValue = amountElement;
        decimal amount;

        if (amountValue == null)
        {
            if (element.TryGetProperty("debit", out var debit) || element.TryGetProperty("credit", out var credit))
            {
                var value = element.TryGetProperty("debit", out var d) ? d : element.GetProperty("credit");
                amount = value.GetDecimal();
            }
            else
            {
                return new TransactionParseResult(false, null, null, null, 0m, "expense", "", new List<string>(), false, source ?? "manual");
            }
        }
        else
        {
            var value = amountValue.Value;
            amount = value.ValueKind switch
            {
                JsonValueKind.Number => value.GetDecimal(),
                JsonValueKind.String => decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m,
                _ => 0m
            };
        }

        if (string.IsNullOrWhiteSpace(merchant) || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(date) || amount <= 0m || (type != "expense" && type != "income"))
        {
            return new TransactionParseResult(false, null, null, null, 0m, "expense", "", new List<string>(), false, source ?? "manual");
        }

        var category = element.TryGetProperty("category", out var categoryElement) ? categoryElement.GetString() ?? "Needs review" : "Needs review";
        var account = element.TryGetProperty("account", out var accountElement) ? accountElement.GetString() ?? "Imported account" : "Imported account";
        var tags = new List<string>();
        if (element.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsElement.EnumerateArray())
            {
                var tagText = tag.GetString();
                if (!string.IsNullOrWhiteSpace(tagText)) tags.Add(tagText.Trim());
            }
        }

        var receipt = element.TryGetProperty("receipt", out var receiptElement) && receiptElement.ValueKind == JsonValueKind.True;
        return new TransactionParseResult(true, merchant.Trim(), date.Trim(), category.Trim(), amount, type.ToLowerInvariant(), account.Trim(), tags, receipt, source ?? "manual");
    }

    private sealed class TransactionParseResult
    {
        public TransactionParseResult(bool valid, string? merchant, string? date, string? category, decimal amount, string type, string account, List<string> tags, bool receipt, string source)
        {
            Valid = valid;
            Merchant = merchant;
            Date = date;
            Category = category;
            Amount = amount;
            Type = type;
            Account = account;
            Tags = tags;
            Receipt = receipt;
            Source = source;
        }

        public bool Valid { get; }
        public string? Merchant { get; }
        public string? Date { get; }
        public string? Category { get; }
        public decimal Amount { get; }
        public string Type { get; }
        public string Account { get; }
        public List<string> Tags { get; }
        public bool Receipt { get; }
        public string Source { get; }
    }

    private string ApplyCategorizationRules(string merchant, string existingCategory)
    {
        if (!string.IsNullOrWhiteSpace(existingCategory) &&
            !string.Equals(existingCategory, "Needs review", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(existingCategory, "Other", StringComparison.OrdinalIgnoreCase))
        {
            return existingCategory;
        }

        if (string.IsNullOrWhiteSpace(merchant)) return "Needs review";

        var rules = ReadRules();
        foreach (var rule in rules)
        {
            var dict = rule.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(rule));
            var enabledObj = dict.TryGetValue("enabled", out var e) ? e : true;
            bool isEnabled = enabledObj is bool b ? b : Convert.ToInt32(enabledObj) == 1;

            if (!isEnabled) continue;

            string whenText = dict.TryGetValue("whenText", out var w) ? w?.ToString() ?? "" : "";
            string thenText = dict.TryGetValue("thenText", out var t) ? t?.ToString() ?? "" : "";

            if (!string.IsNullOrWhiteSpace(whenText) && merchant.Contains(whenText, StringComparison.OrdinalIgnoreCase))
            {
                return thenText;
            }
        }

        return string.IsNullOrWhiteSpace(existingCategory) ? "Needs review" : existingCategory;
    }
}
