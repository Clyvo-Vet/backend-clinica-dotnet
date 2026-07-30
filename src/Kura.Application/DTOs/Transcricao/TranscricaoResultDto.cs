namespace Kura.Application.DTOs.Transcricao;

/// <summary>
/// Resultado bruto retornado pela Luna (POST /transcricao). Ambos os campos nulos
/// indicam falha de transcrição — o vet edita o SOAP manualmente (sem 500 fatal).
/// </summary>
public sealed class TranscricaoResultDto
{
    public string? Transcricao { get; init; }
    public SoapDraftDto? Soap { get; init; }
}
