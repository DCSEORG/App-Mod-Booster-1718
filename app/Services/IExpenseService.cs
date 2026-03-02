using ExpenseApp.Models;

namespace ExpenseApp.Services;

public interface IExpenseService
{
    Task<(List<Expense> expenses, string? error)> GetAllExpensesAsync(string? statusFilter = null, string? search = null);
    Task<(Expense? expense, string? error)> GetExpenseByIdAsync(int id);
    Task<(Expense? expense, string? error)> CreateExpenseAsync(ExpenseDto dto);
    Task<(bool success, string? error)> SubmitExpenseAsync(int id);
    Task<(bool success, string? error)> ApproveExpenseAsync(int id, int reviewedBy);
    Task<(bool success, string? error)> RejectExpenseAsync(int id, int reviewedBy);
    Task<(bool success, string? error)> DeleteExpenseAsync(int id);
    Task<(List<Expense> expenses, string? error)> GetSubmittedExpensesAsync();
}
