namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Documento;

public interface IReceituarioPdfService
{
    /// <summary>
    /// Gera o PDF do receituário de uma prescrição (CRMV, pet, medicamento/posologia/
    /// duração e data), salva o arquivo em storage e persiste um Documento apontando
    /// para o path (DsCaminho) — nunca BLOB (ver D-9).
    /// </summary>
    Task<DocumentoResponseDto> GerarReceituarioAsync(long idEventoClinico);
}
