namespace Finara.Application.Interfaces;

// Faz 5'in ilk adımı: Ollama'ya temel bir bağlantının gerçekten çalıştığını doğrulamak
// için minimal bir soyutlama. MCP tool'ları eklendiğinde bu arayüz genişleyecek.
public interface IAiChatService
{
    Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default);
}
