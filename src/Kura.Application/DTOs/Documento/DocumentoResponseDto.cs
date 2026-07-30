namespace Kura.Application.DTOs.Documento;

public sealed class DocumentoResponseDto
{
    public long Id { get; init; }
    public long IdEventoClinico { get; init; }
    public string NmArquivo { get; init; } = string.Empty;
    public string DsTipoMime { get; init; } = string.Empty;
    public string DsCaminho { get; init; } = string.Empty;
    public long NrTamanhoBytes { get; init; }
}
