using Finara.Application.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace Finara.Infrastructure.Services;

// Yerel makinede çalışan Ollama'ya (llama3.1:8b) bağlanır — bulut tabanlı bir
// LLM API'sine (ChatGPT/Claude vb.) bilerek bağlanılmıyor, kullanıcının kendi
// donanımında barındırılan bir model kullanılıyor.
public class AiChatService : IAiChatService
{
    private readonly IChatClient _chatClient;

    public AiChatService(IConfiguration configuration)
    {
        var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var model = configuration["Ollama:Model"] ?? "llama3.1:8b";
        _chatClient = new OllamaChatClient(new Uri(baseUrl), model);
    }

    public async Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var response = await _chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
        return response.Text;
    }
}
