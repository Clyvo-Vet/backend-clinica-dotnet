namespace Kura.Application.DTOs.Iot;

using Kura.Application.DTOs.LeituraTemperatura;

public sealed class DispositivoStatusDto
{
    public long IdDispositivo { get; init; }
    public string CdDispositivo { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public LeituraTemperaturaResponseDto? UltimaLeitura { get; init; }
}
