using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

/// <summary>Builds the exception anchor (party + reason) used by older tests that only had a Bill id.</summary>
internal static class ExposureAnchor
{
    public const string Note = "Căn cứ ngoại lệ kiểm thử";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static async Task<Guid> PartyAsync(HttpClient client, Guid tenantId, bool receivable)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/business-parties")
        {
            Content = JsonContent.Create(new
            {
                code = (receivable ? "CUS-" : "VEN-") + Guid.NewGuid().ToString("N")[..8],
                name = receivable ? "Khách ngoại lệ kiểm thử" : "Nhà cung cấp ngoại lệ kiểm thử",
                roleCodes = receivable
                    ? new[] { "customer", "payer", "bill_to" }
                    : new[] { "vendor", "payee" }
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private sealed record IdBody(Guid Id);
}
