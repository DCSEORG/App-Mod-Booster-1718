using ExpenseApp.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Runtime.CompilerServices;

namespace ExpenseApp.Services;

public class UserService : IUserService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserService> _logger;

    public UserService(IConfiguration configuration, ILogger<UserService> logger)
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

    private static List<User> GetDummyUsers() => new()
    {
        new User { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true },
        new User { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true }
    };

    private static List<Category> GetDummyCategories() => new()
    {
        new Category { CategoryId = 1, CategoryName = "Travel", IsActive = true },
        new Category { CategoryId = 2, CategoryName = "Meals", IsActive = true },
        new Category { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
        new Category { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
        new Category { CategoryId = 5, CategoryName = "Other", IsActive = true }
    };

    public async Task<(List<User> users, string? error)> GetAllUsersAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetAllUsers", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            var users = new List<User>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32("UserId"),
                    UserName = reader.GetString("UserName"),
                    Email = reader.GetString("Email"),
                    RoleId = reader.GetInt32("RoleId"),
                    RoleName = reader.IsDBNull("RoleName") ? "" : reader.GetString("RoleName"),
                    ManagerId = reader.IsDBNull("ManagerId") ? null : reader.GetInt32("ManagerId"),
                    IsActive = reader.GetBoolean("IsActive"),
                    CreatedAt = reader.GetDateTime("CreatedAt")
                });
            }
            return (users, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users");
            return (GetDummyUsers(), BuildErrorMessage(ex));
        }
    }

    public async Task<(List<Category> categories, string? error)> GetAllCategoriesAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetAllCategories", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            var categories = new List<Category>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new Category
                {
                    CategoryId = reader.GetInt32("CategoryId"),
                    CategoryName = reader.GetString("CategoryName"),
                    IsActive = reader.GetBoolean("IsActive")
                });
            }
            return (categories, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching categories");
            return (GetDummyCategories(), BuildErrorMessage(ex));
        }
    }

    private static string BuildErrorMessage(Exception ex, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var fileName = System.IO.Path.GetFileName(filePath);
        return $"Database error in {fileName} (line ~{lineNumber}): {ex.GetType().Name} - {ex.Message}";
    }
}
