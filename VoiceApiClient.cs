using System.Net.Http;
using System.Text.Json;

namespace VoiceNotifier;
internal sealed class VoiceApiClient : IDisposable
{
    private readonly HttpClient _client;
    public VoiceApiClient(int timeoutSeconds) { _client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) }; }
    public async Task<IReadOnlyList<string>> GetMessagesAsync(string url, CancellationToken token)
    {
        using var response = await _client.GetAsync(url, token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        if (!document.RootElement.TryGetProperty("code", out var code) || code.ToString() != "200" || !document.RootElement.TryGetProperty("data", out var data)) return Array.Empty<string>();
        if (data.ValueKind == JsonValueKind.Null || data.ValueKind == JsonValueKind.Undefined) return Array.Empty<string>();
        if (data.ValueKind == JsonValueKind.String)
        {
            var text = data.GetString(); if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
            try { using var nested = JsonDocument.Parse(text); data = nested.RootElement.Clone(); } catch { return new[] { text }; }
        }
        if (data.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        return data.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : "").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }
    public void Dispose() => _client.Dispose();
}
