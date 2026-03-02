using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChatApp.Services;

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private string ExpenseApiBaseUrl => _configuration["ExpenseApiBaseUrl"] ?? "https://localhost:7001";

    private static readonly List<ChatTool> ExpenseTools = new()
    {
        ChatTool.CreateFunctionTool(
            "get_expenses",
            "Retrieves all expenses from the system. Can optionally filter by status (Draft, Submitted, Approved, Rejected) and search by keyword.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "status": { "type": "string", "description": "Filter by status: Draft, Submitted, Approved, or Rejected", "enum": ["Draft","Submitted","Approved","Rejected"] },
                "search": { "type": "string", "description": "Keyword to search in description or category" }
              },
              "required": []
            }
            """)
        ),
        ChatTool.CreateFunctionTool(
            "get_submitted_expenses",
            "Retrieves all expenses pending manager approval (status = Submitted).",
            BinaryData.FromString("""{"type":"object","properties":{},"required":[]}""")
        ),
        ChatTool.CreateFunctionTool(
            "create_expense",
            "Creates a new expense in Draft status.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "categoryId": { "type": "integer", "description": "Category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)" },
                "amountGBP": { "type": "number", "description": "Amount in GBP pounds (e.g. 25.50)" },
                "expenseDate": { "type": "string", "description": "Expense date in YYYY-MM-DD format" },
                "description": { "type": "string", "description": "Description of the expense" }
              },
              "required": ["categoryId", "amountGBP", "expenseDate"]
            }
            """)
        ),
        ChatTool.CreateFunctionTool(
            "submit_expense",
            "Submits a Draft expense for manager approval.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "expenseId": { "type": "integer", "description": "The ID of the expense to submit" }
              },
              "required": ["expenseId"]
            }
            """)
        ),
        ChatTool.CreateFunctionTool(
            "approve_expense",
            "Approves a Submitted expense. Manager action only.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "expenseId": { "type": "integer", "description": "The ID of the expense to approve" }
              },
              "required": ["expenseId"]
            }
            """)
        ),
        ChatTool.CreateFunctionTool(
            "reject_expense",
            "Rejects a Submitted expense. Manager action only.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "expenseId": { "type": "integer", "description": "The ID of the expense to reject" }
              },
              "required": ["expenseId"]
            }
            """)
        ),
        ChatTool.CreateFunctionTool(
            "get_categories",
            "Retrieves all available expense categories.",
            BinaryData.FromString("""{"type":"object","properties":{},"required":[]}""")
        )
    };

    public async Task<string> GetResponseAsync(string userMessage, List<(string role, string content)> history)
    {
        var openAIEndpoint = _configuration["OpenAI:Endpoint"];
        var deploymentName = _configuration["OpenAI:DeploymentName"] ?? "gpt-4o";

        if (string.IsNullOrEmpty(openAIEndpoint))
        {
            return "⚠️ **GenAI services are not configured.**\n\n" +
                   "The Azure OpenAI endpoint has not been set up yet. To enable the AI chat assistant:\n\n" +
                   "1. Run **`./deploy-with-chat.sh`** to deploy Azure OpenAI and AI Search resources\n" +
                   "2. The script will automatically configure the required environment variables\n" +
                   "3. Restart the application after deployment completes\n\n" +
                   "In the meantime, you can use the main expense app at the **/Index** page to manage expenses directly.";
        }

        try
        {
            // Use ManagedIdentityCredential with explicit client ID for user-assigned MI
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
            Azure.Core.TokenCredential credential;

            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            var client = new AzureOpenAIClient(new Uri(openAIEndpoint), credential);
            var chatClient = client.GetChatClient(deploymentName);

            // Build message history
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    "You are a helpful AI assistant for the Expense Management System. " +
                    "You can help users manage their expenses using the available functions. " +
                    "Available capabilities:\n" +
                    "- List and filter expenses by status or keyword\n" +
                    "- Create new expense entries\n" +
                    "- Submit expenses for approval\n" +
                    "- Approve or reject submitted expenses (manager action)\n" +
                    "- List available expense categories\n\n" +
                    "When listing expenses or categories, always format the output as a numbered or bulleted list. " +
                    "Always show amounts in GBP format (£X.XX). " +
                    "Be concise and helpful. If an operation succeeds, confirm it clearly.")
            };

            // Add conversation history
            foreach (var (role, content) in history)
            {
                if (role == "user")
                    messages.Add(new UserChatMessage(content));
                else if (role == "assistant")
                    messages.Add(new AssistantChatMessage(content));
            }

            // Add current user message
            messages.Add(new UserChatMessage(userMessage));

            var options = new ChatCompletionOptions();
            foreach (var tool in ExpenseTools)
                options.Tools.Add(tool);

            // Agentic loop - handle function calling
            int maxIterations = 5;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                var response = await chatClient.CompleteChatAsync(messages, options);
                var completion = response.Value;

                if (completion.FinishReason == ChatFinishReason.ToolCalls)
                {
                    // Add assistant message with tool calls
                    messages.Add(new AssistantChatMessage(completion));

                    // Process each tool call
                    foreach (var toolCall in completion.ToolCalls)
                    {
                        var result = await ExecuteToolCallAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                        messages.Add(new ToolChatMessage(toolCall.Id, result));
                    }
                    // Continue the loop to get the final response
                    continue;
                }

                // Got a final text response
                if (completion.Content.Count > 0)
                    return completion.Content[0].Text;

                return "I couldn't generate a response. Please try again.";
            }

            return "I reached the maximum number of tool calls. Please try a more specific request.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Azure OpenAI");
            return $"❌ Error communicating with AI service: {ex.Message}\n\nPlease check that Azure OpenAI is configured correctly and the managed identity has the 'Cognitive Services OpenAI User' role.";
        }
    }

    private async Task<string> ExecuteToolCallAsync(string functionName, string argumentsJson)
    {
        _logger.LogInformation("Executing tool: {FunctionName} with args: {Args}", functionName, argumentsJson);
        var httpClient = _httpClientFactory.CreateClient();

        try
        {
            switch (functionName)
            {
                case "get_expenses":
                {
                    var args = JsonNode.Parse(argumentsJson);
                    var status = args?["status"]?.GetValue<string>();
                    var search = args?["search"]?.GetValue<string>();
                    var url = $"{ExpenseApiBaseUrl}/api/expenses";
                    var queryParams = new List<string>();
                    if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={Uri.EscapeDataString(status)}");
                    if (!string.IsNullOrEmpty(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");
                    if (queryParams.Any()) url += "?" + string.Join("&", queryParams);
                    var resp = await httpClient.GetStringAsync(url);
                    return resp;
                }

                case "get_submitted_expenses":
                {
                    var resp = await httpClient.GetStringAsync($"{ExpenseApiBaseUrl}/api/expenses/submitted");
                    return resp;
                }

                case "create_expense":
                {
                    var args = JsonNode.Parse(argumentsJson);
                    var body = new
                    {
                        // userId=1 (Alice) is used as the default for demo purposes.
                        // In a production system, this would come from authenticated user context.
                        userId = 1,
                        categoryId = args?["categoryId"]?.GetValue<int>() ?? 1,
                        amountGBP = args?["amountGBP"]?.GetValue<double>() ?? 0,
                        expenseDate = args?["expenseDate"]?.GetValue<string>() ?? DateTime.Today.ToString("yyyy-MM-dd"),
                        description = args?["description"]?.GetValue<string>()
                    };
                    var json = JsonSerializer.Serialize(body);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    var resp = await httpClient.PostAsync($"{ExpenseApiBaseUrl}/api/expenses", content);
                    var respBody = await resp.Content.ReadAsStringAsync();
                    return resp.IsSuccessStatusCode ? $"Created: {respBody}" : $"Error: {respBody}";
                }

                case "submit_expense":
                {
                    var args = JsonNode.Parse(argumentsJson);
                    var expenseId = args?["expenseId"]?.GetValue<int>() ?? 0;
                    var resp = await httpClient.PutAsync($"{ExpenseApiBaseUrl}/api/expenses/{expenseId}/submit", null);
                    return resp.IsSuccessStatusCode ? "Expense submitted successfully." : $"Error: {await resp.Content.ReadAsStringAsync()}";
                }

                case "approve_expense":
                {
                    var args = JsonNode.Parse(argumentsJson);
                    var expenseId = args?["expenseId"]?.GetValue<int>() ?? 0;
                    // reviewedBy=2 (Bob Manager) is used as the default for demo purposes.
                    // In a production system, this would come from authenticated user context.
                    var body = new StringContent("""{"reviewedBy":2}""", System.Text.Encoding.UTF8, "application/json");
                    var resp = await httpClient.PutAsync($"{ExpenseApiBaseUrl}/api/expenses/{expenseId}/approve", body);
                    return resp.IsSuccessStatusCode ? "Expense approved successfully." : $"Error: {await resp.Content.ReadAsStringAsync()}";
                }

                case "reject_expense":
                {
                    var args = JsonNode.Parse(argumentsJson);
                    var expenseId = args?["expenseId"]?.GetValue<int>() ?? 0;
                    // reviewedBy=2 (Bob Manager) is used as the default for demo purposes.
                    // In a production system, this would come from authenticated user context.
                    var body = new StringContent("""{"reviewedBy":2}""", System.Text.Encoding.UTF8, "application/json");
                    var resp = await httpClient.PutAsync($"{ExpenseApiBaseUrl}/api/expenses/{expenseId}/reject", body);
                    return resp.IsSuccessStatusCode ? "Expense rejected successfully." : $"Error: {await resp.Content.ReadAsStringAsync()}";
                }

                case "get_categories":
                {
                    var resp = await httpClient.GetStringAsync($"{ExpenseApiBaseUrl}/api/categories");
                    return resp;
                }

                default:
                    return $"Unknown function: {functionName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {FunctionName}", functionName);
            return $"Error executing {functionName}: {ex.Message}";
        }
    }
}
