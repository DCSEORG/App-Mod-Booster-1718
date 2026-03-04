using ExpenseApp.Models;

namespace ExpenseApp.Services;

public interface IUserService
{
    Task<(List<User> users, string? error)> GetAllUsersAsync();
    Task<(List<Category> categories, string? error)> GetAllCategoriesAsync();
}
