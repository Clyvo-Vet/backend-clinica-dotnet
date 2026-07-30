namespace Kura.Application.DTOs.Transcricao;

public sealed class EventoClinicoSoapResponseDto
{
    public long IdEventoClinico { get; init; }
    public string? DsTranscricao { get; init; }
    public SoapDraftDto Soap { get; init; } = new();
    public bool StSoapConfirmado { get; init; }
}
