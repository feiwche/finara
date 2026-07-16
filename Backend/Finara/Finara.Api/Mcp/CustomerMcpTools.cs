using System.ComponentModel;
using Finara.Infrastructure.Ai;
using ModelContextProtocol.Server;

namespace Finara.Api.Mcp;

// Dış MCP istemcilerine (Claude Desktop, başka bir AI aracı vb.) açılan gerçek
// MCP tool'ları — CustomerContextTools'un ince bir sarmalayıcısı. Parametreler
// arasındaki CustomerContextTools, MCP SDK tarafından DI'dan otomatik çözülüyor;
// yalnızca customerId/days gibi ilkel tipler LLM'in doldurduğu gerçek argümanlar.
[McpServerToolType]
public static class CustomerMcpTools
{
    [McpServerTool, Description("Bir müşterinin profil bilgilerini (yaş, yatırım/SWIFT bayrakları) ve aktif segmentini (RFM skoru, harcama eğilimi, baskın kategori) getirir.")]
    public static async Task<string> GetCustomerProfile(
        CustomerContextTools tools,
        [Description("Müşteri kimliği (GUID formatında string)")] string customerId)
    {
        if (!Guid.TryParse(customerId, out var id))
            return "Geçersiz müşteri kimliği formatı.";
        return await tools.GetCustomerProfile(id);
    }

    [McpServerTool, Description("Bir müşterinin son harcama işlemlerini listeler.")]
    public static async Task<string> GetCustomerTransactions(
        CustomerContextTools tools,
        [Description("Müşteri kimliği (GUID formatında string)")] string customerId,
        [Description("Kaç gün geriye bakılacağı, varsayılan 90")] int days = 90)
    {
        if (!Guid.TryParse(customerId, out var id))
            return "Geçersiz müşteri kimliği formatı.";
        return await tools.GetCustomerTransactions(id, days);
    }

    [McpServerTool, Description("Bir müşteriye sunulmuş kampanya tekliflerini ve güven skorlarını listeler.")]
    public static async Task<string> GetCustomerOffers(
        CustomerContextTools tools,
        [Description("Müşteri kimliği (GUID formatında string)")] string customerId)
    {
        if (!Guid.TryParse(customerId, out var id))
            return "Geçersiz müşteri kimliği formatı.";
        return await tools.GetCustomerOffers(id);
    }

    [McpServerTool, Description("Şu anda aktif olan tüm kampanyaları listeler.")]
    public static async Task<string> GetActiveCampaigns(CustomerContextTools tools)
        => await tools.GetActiveCampaigns();
}
