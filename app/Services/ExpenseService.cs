using ExpenseApp.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Runtime.CompilerServices;

namespace ExpenseApp.Services;

public class ExpenseService : IExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private SqlConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        return new SqlConnection(connectionString);
    }

    // Dummy data for fallback when DB is unavailable
    private static List<Expense> GetDummyExpenses() => new()
    {
        new Expense
        {
            ExpenseId = 1, UserId = 1, UserName = "Alice Example",
            CategoryId = 1, CategoryName = "Travel",
            StatusId = 2, StatusName = "Submitted",
            AmountMinor = 2540, Currency = "GBP",
            ExpenseDate = new DateTime(2025, 10, 20),
            Description = "Taxi from airport to client site",
            SubmittedAt = DateTime.UtcNow.AddDays(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        },
        new Expense
        {
            ExpenseId = 2, UserId = 1, UserName = "Alice Example",
            CategoryId = 2, CategoryName = "Meals",
            StatusId = 3, StatusName = "Approved",
            AmountMinor = 1425, Currency = "GBP",
            ExpenseDate = new DateTime(2025, 9, 15),
            Description = "Client lunch meeting",
            SubmittedAt = DateTime.UtcNow.AddDays(-30),
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        },
        new Expense
        {
            ExpenseId = 3, UserId = 1, UserName = "Alice Example",
            CategoryId = 3, CategoryName = "Supplies",
            StatusId = 1, StatusName = "Draft",
            AmountMinor = 799, Currency = "GBP",
            ExpenseDate = new DateTime(2025, 11, 1),
            Description = "Office stationery",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        },
        new Expense
        {
            ExpenseId = 4, UserId = 1, UserName = "Alice Example",
            CategoryId = 4, CategoryName = "Accommodation",
            StatusId = 3, StatusName = "Approved",
            AmountMinor = 12300, Currency = "GBP",
            ExpenseDate = new DateTime(2025, 8, 10),
            Description = "Hotel during client visit",
            SubmittedAt = DateTime.UtcNow.AddDays(-60),
            CreatedAt = DateTime.UtcNow.AddDays(-60)
        }
    };

    public async Task<(List<Expense> expenses, string? error)> GetAllExpensesAsync(
        string? statusFilter = null, string? search = null)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetAllExpenses", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@StatusFilter", (object?)statusFilter ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);

            var expenses = new List<Expense>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return (expenses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all expenses");
            return (GetDummyExpenses(), BuildErrorMessage(ex));
        }
    }

    public async Task<(Expense? expense, string? error)> GetExpenseByIdAsync(int id)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenseById", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (MapExpense(reader), null);
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching expense {ExpenseId}", id);
            return (null, BuildErrorMessage(ex));
        }
    }

    public async Task<(Expense? expense, string? error)> CreateExpenseAsync(ExpenseDto dto)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_CreateExpense", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UserId", dto.UserId);
            cmd.Parameters.AddWithValue("@CategoryId", dto.CategoryId);
            cmd.Parameters.AddWithValue("@AmountMinor", dto.AmountMinor);
            cmd.Parameters.AddWithValue("@Currency", "GBP");
            cmd.Parameters.AddWithValue("@ExpenseDate", dto.ExpenseDate.Date);
            cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReceiptFile", (object?)dto.ReceiptFile ?? DBNull.Value);

            var newId = await cmd.ExecuteScalarAsync();
            if (newId != null)
            {
                return await GetExpenseByIdAsync(Convert.ToInt32(newId));
            }
            return (null, "Failed to create expense");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return (null, BuildErrorMessage(ex));
        }
    }

    public async Task<(bool success, string? error)> SubmitExpenseAsync(int id)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_SubmitExpense", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", id);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {ExpenseId}", id);
            return (false, BuildErrorMessage(ex));
        }
    }

    public async Task<(bool success, string? error)> ApproveExpenseAsync(int id, int reviewedBy)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_ApproveExpense", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", id);
            cmd.Parameters.AddWithValue("@ReviewedBy", reviewedBy);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {ExpenseId}", id);
            return (false, BuildErrorMessage(ex));
        }
    }

    public async Task<(bool success, string? error)> RejectExpenseAsync(int id, int reviewedBy)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_RejectExpense", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", id);
            cmd.Parameters.AddWithValue("@ReviewedBy", reviewedBy);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {ExpenseId}", id);
            return (false, BuildErrorMessage(ex));
        }
    }

    public async Task<(bool success, string? error)> DeleteExpenseAsync(int id)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_DeleteExpense", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", id);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", id);
            return (false, BuildErrorMessage(ex));
        }
    }

    public async Task<(List<Expense> expenses, string? error)> GetSubmittedExpensesAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetSubmittedExpenses", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            var expenses = new List<Expense>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return (expenses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching submitted expenses");
            var dummySubmitted = GetDummyExpenses()
                .Where(e => e.StatusName == "Submitted").ToList();
            return (dummySubmitted, BuildErrorMessage(ex));
        }
    }

    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32("ExpenseId"),
            UserId = reader.GetInt32("UserId"),
            UserName = reader.IsDBNull("UserName") ? "" : reader.GetString("UserName"),
            CategoryId = reader.GetInt32("CategoryId"),
            CategoryName = reader.IsDBNull("CategoryName") ? "" : reader.GetString("CategoryName"),
            StatusId = reader.GetInt32("StatusId"),
            StatusName = reader.IsDBNull("StatusName") ? "" : reader.GetString("StatusName"),
            AmountMinor = reader.GetInt32("AmountMinor"),
            Currency = reader.IsDBNull("Currency") ? "GBP" : reader.GetString("Currency"),
            ExpenseDate = reader.GetDateTime("ExpenseDate"),
            Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
            ReceiptFile = reader.IsDBNull("ReceiptFile") ? null : reader.GetString("ReceiptFile"),
            SubmittedAt = reader.IsDBNull("SubmittedAt") ? null : reader.GetDateTime("SubmittedAt"),
            ReviewedBy = reader.IsDBNull("ReviewedBy") ? null : reader.GetInt32("ReviewedBy"),
            ReviewedByName = reader.IsDBNull("ReviewedByName") ? null : reader.GetString("ReviewedByName"),
            ReviewedAt = reader.IsDBNull("ReviewedAt") ? null : reader.GetDateTime("ReviewedAt"),
            CreatedAt = reader.GetDateTime("CreatedAt")
        };
    }

    private static string BuildErrorMessage(Exception ex, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var fileName = System.IO.Path.GetFileName(filePath);
        var message = ex.Message;
        var isManagedIdentityError = message.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Active Directory", StringComparison.OrdinalIgnoreCase)
            || message.Contains("authentication", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Login failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unable to load", StringComparison.OrdinalIgnoreCase);

        var remediation = isManagedIdentityError
            ? " FIX: Ensure the managed identity is assigned to the App Service, the User Id (client ID) in the connection string is correct, " +
              "the managed identity has been added to the SQL database as an external user with db_datareader/db_datawriter roles, " +
              "and the AZURE_CLIENT_ID app setting matches the managed identity client ID. " +
              "For local dev, change connection string to use 'Authentication=Active Directory Default' and run 'az login'."
            : " Check that the SQL Server is running, the firewall allows your IP, and the connection string placeholder values have been replaced.";

        return $"Database error in {fileName} (line ~{lineNumber}): {ex.GetType().Name} - {message}.{remediation}";
    }
}
