namespace Kura.Application.Services;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kura.Application.DTOs.Transcricao;
using Kura.Application.Services.Interfaces;
using Microsoft.Extensions.Logging;

public sealed class LunaTranscricaoService : ILunaTranscricaoService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ILogger<LunaTranscricaoService> _logger;

    public LunaTranscricaoService(HttpClient httpClient, ILogger<LunaTranscricaoService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TranscricaoResultDto> TranscreverAsync(Stream audio, string nomeArquivo, string contentType)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var audioContent = new StreamContent(audio);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            content.Add(audioContent, "audio", nomeArquivo);

            var response = await _httpClient.PostAsync("transcricao", content);
            if (!response.IsSuccessStatusCode)
            {
                // LGPD: logar só o status — nunca o conteúdo do áudio/transcrição.
                _logger.LogWarning(
                    "Luna retornou {StatusCode} ao transcrever áudio. Draft ficará vazio para edição manual.",
                    response.StatusCode);
                return new TranscricaoResultDto();
            }

            var body = await response.Content.ReadFromJsonAsync<TranscricaoResultDto>(JsonOptions);
            return body ?? new TranscricaoResultDto();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Falha ao chamar a Luna para transcrever áudio. Draft ficará vazio para edição manual.");
            return new TranscricaoResultDto();
        }
    }
}
