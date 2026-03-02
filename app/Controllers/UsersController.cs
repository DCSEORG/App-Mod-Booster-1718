using ExpenseApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>Get all active users</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var (users, error) = await _userService.GetAllUsersAsync();
        if (error != null)
            return Ok(new { data = users, warning = error });
        return Ok(users);
    }

    /// <summary>Get all expense categories</summary>
    [HttpGet("/api/categories")]
    public async Task<IActionResult> GetCategories()
    {
        var (categories, error) = await _userService.GetAllCategoriesAsync();
        if (error != null)
            return Ok(new { data = categories, warning = error });
        return Ok(categories);
    }
}
