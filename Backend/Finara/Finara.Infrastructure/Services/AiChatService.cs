using Finara.Application.Interfaces;
using Microsoft.Extensions.AI;

namespace Finara.Infrastructure.Services;

// Yerel makinede çalışan Ollama'ya (llama3.1:8b) bağlanır — bulut tabanlı bir
// LLM API'sine bilerek bağlanılmıyor, kullanıcının kendi donanımında
// barındırılan bir model kullanılıyor.
//
// IChatClient DI'dan Singleton olarak enjekte ediliyor (bkz. Program.cs) —
// AiInsightService ile PAYLAŞILAN tek bir bağlantı. Öncesinde AiInsightService
// her çağrıda kendi client'ını oluşturup hiç Dispose etmiyordu (kaynak sızıntısı,
// denetimde bulundu); artık tek yerden, bir kez oluşturuluyor.
public class AiChatService(IChatClient chatClient) : IAiChatService
{
    public async Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
        return response.Text;
    }
}
