namespace Kura.Application.Services;

using System.Net.Http.Json;
using Kura.Application.DTOs.Teleconsulta;
using Kura.Application.Services.Interfaces;
using Microsoft.Extensions.Logging;

public sealed class DailyService : IDailyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DailyService> _logger;

    public DailyService(HttpClient httpClient, ILogger<DailyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DailyRoomResult> CriarSalaAsync(string nomeSala)
    {
        try
        {
            // enable_recording é omitido de propósito: gravação exige consentimento próprio (CFMV
            // 1.465/2022) que este fluxo não coleta — a sala nasce sem gravação habilitada.
            var payload = new
            {
                name = nomeSala,
                properties = new
                {
                    exp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds()
                }
            };

            var response = await _httpClient.PostAsJsonAsync("rooms", payload);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Daily.co retornou {StatusCode} ao criar sala. Aplicando fallback de link manual.",
                    response.StatusCode);
                return DailyRoomResult.Falha();
            }

            var body = await response.Content.ReadFromJsonAsync<DailyRoomApiResponse>();
            if (body is null || string.IsNullOrWhiteSpace(body.Url))
            {
                _logger.LogWarning("Daily.co retornou resposta sem url. Aplicando fallback de link manual.");
                return DailyRoomResult.Falha();
            }

            return DailyRoomResult.ComSucesso(body.Url);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Falha ao chamar Daily.co para criar sala. Aplicando fallback de link manual.");
            return DailyRoomResult.Falha();
        }
    }

    private sealed class DailyRoomApiResponse
    {
        public string? Name { get; set; }
        public string? Url { get; set; }
    }
}
