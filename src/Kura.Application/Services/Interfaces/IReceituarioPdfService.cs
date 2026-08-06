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

    /// <summary>
    /// Lê do disco os bytes de um receituário já gerado (TASK-51). O caminho físico é
    /// resolvido inteiramente a partir do <c>Documento</c> no banco — nunca a partir de
    /// um parâmetro de path/query vindo do cliente. O isolamento de tenant vem de
    /// carregar o <c>EventoClinico</c> primeiro (que já está em
    /// <c>KuraDbContext.ApplyTenantFilters</c>): se o evento não existir ou for de outra
    /// clínica, esta chamada lança <see cref="Kura.Domain.Exceptions.EntidadeNaoEncontradaException"/>
    /// sem diferenciar os dois casos (evita vazar existência via resposta diferenciada).
    /// </summary>
    Task<ArquivoBinarioDto> ObterArquivoReceituarioAsync(long idEventoClinico, long idDocumento);
}
