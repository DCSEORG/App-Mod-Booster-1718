using ExpenseApp.Models;
using ExpenseApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>Get all expenses with optional filtering</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Expense>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? search)
    {
        var (expenses, error) = await _expenseService.GetAllExpensesAsync(status, search);
        if (error != null)
            return Ok(new { data = expenses, warning = error });
        return Ok(expenses);
    }

    /// <summary>Get a single expense by ID</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
        if (error != null)
            return StatusCode(500, new { message = error });
        if (expense == null)
            return NotFound(new { message = $"Expense {id} not found" });
        return Ok(expense);
    }

    /// <summary>Create a new expense (Draft status)</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] ExpenseDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (expense, error) = await _expenseService.CreateExpenseAsync(dto);
        if (error != null)
            return StatusCode(500, new { message = error });
        if (expense == null)
            return StatusCode(500, new { message = "Failed to create expense" });

        return CreatedAtAction(nameof(GetById), new { id = expense.ExpenseId }, expense);
    }

    /// <summary>Submit an expense for approval</summary>
    [HttpPut("{id:int}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Submit(int id)
    {
        var (success, error) = await _expenseService.SubmitExpenseAsync(id);
        if (!success)
            return StatusCode(500, new { message = error ?? "Failed to submit expense" });
        return Ok(new { message = "Expense submitted successfully" });
    }

    /// <summary>Approve a submitted expense</summary>
    [HttpPut("{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveRejectDto dto)
    {
        var (success, error) = await _expenseService.ApproveExpenseAsync(id, dto.ReviewedBy);
        if (!success)
            return StatusCode(500, new { message = error ?? "Failed to approve expense" });
        return Ok(new { message = "Expense approved successfully" });
    }

    /// <summary>Reject a submitted expense</summary>
    [HttpPut("{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Reject(int id, [FromBody] ApproveRejectDto dto)
    {
        var (success, error) = await _expenseService.RejectExpenseAsync(id, dto.ReviewedBy);
        if (!success)
            return StatusCode(500, new { message = error ?? "Failed to reject expense" });
        return Ok(new { message = "Expense rejected successfully" });
    }

    /// <summary>Delete an expense</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _expenseService.DeleteExpenseAsync(id);
        if (!success)
            return StatusCode(500, new { message = error ?? "Failed to delete expense" });
        return Ok(new { message = "Expense deleted successfully" });
    }

    /// <summary>Get all submitted expenses (for manager approval)</summary>
    [HttpGet("submitted")]
    [ProducesResponseType(typeof(IEnumerable<Expense>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmitted()
    {
        var (expenses, error) = await _expenseService.GetSubmittedExpensesAsync();
        if (error != null)
            return Ok(new { data = expenses, warning = error });
        return Ok(expenses);
    }
}
