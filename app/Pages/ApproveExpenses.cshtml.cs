using ExpenseApp.Models;
using ExpenseApp.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseApp.Pages;

public class ApproveExpensesModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<Expense> Expenses { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public ApproveExpensesModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        var (expenses, error) = await _expenseService.GetSubmittedExpensesAsync();
        Expenses = expenses;
        ErrorMessage = error;
    }
}
