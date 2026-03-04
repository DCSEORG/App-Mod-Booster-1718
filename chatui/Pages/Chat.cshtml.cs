using ChatApp.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatApp.Pages;

public class ChatModel : PageModel
{
    private readonly IChatService _chatService;

    public ChatModel(IChatService chatService)
    {
        _chatService = chatService;
    }

    public void OnGet() { }
}
