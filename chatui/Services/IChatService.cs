namespace ChatApp.Services;

public interface IChatService
{
    Task<string> GetResponseAsync(string userMessage, List<(string role, string content)> history);
}
