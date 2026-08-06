namespace Kura.Application.DTOs.Documento;

/// <summary>
/// Bytes de um arquivo já persistido em storage, prontos para serem devolvidos ao
/// cliente (ex.: download do PDF de um receituário — TASK-51). Nunca carrega caminho
/// de disco: quem consome isto só enxerga o conteúdo e os metadados de resposta HTTP.
/// </summary>
public sealed class ArquivoBinarioDto
{
    public byte[] Conteudo { get; init; } = Array.Empty<byte>();
    public string NomeArquivo { get; init; } = string.Empty;
    public string DsTipoMime { get; init; } = string.Empty;
}
