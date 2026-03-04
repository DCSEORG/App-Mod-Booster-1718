using ExpenseApp.Models;
using ExpenseApp.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseApp.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly IUserService _userService;

    public List<Expense> Expenses { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public IndexModel(IExpenseService expenseService, IUserService userService)
    {
        _expenseService = expenseService;
        _userService = userService;
    }

    public async Task OnGetAsync()
    {
        var (expenses, expError) = await _expenseService.GetAllExpensesAsync();
        Expenses = expenses;
        if (expError != null)
            ErrorMessage = expError;

        var (categories, catError) = await _userService.GetAllCategoriesAsync();
        Categories = categories;
        if (catError != null && ErrorMessage == null)
            ErrorMessage = catError;
    }
}
