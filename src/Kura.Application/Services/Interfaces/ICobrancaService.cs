namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Cobranca;

/// <summary>FD-10 — lançamento e leitura de cobranças de um evento clínico.</summary>
public interface ICobrancaService
{
    /// <summary>
    /// Lança uma cobrança no evento clínico informado. O evento tem de pertencer à clínica
    /// do JWT (<c>404</c> caso contrário) e o serviço de tabela, quando informado, tem de
    /// ser desta clínica e estar ativo (<c>422</c> caso contrário).
    /// </summary>
    Task<CobrancaResponseDto> LancarAsync(long idEventoClinico, CobrancaCreateDto dto);

    /// <summary>Cobranças ativas lançadas num evento clínico da clínica do JWT.</summary>
    Task<IEnumerable<CobrancaResponseDto>> ListarDoEventoAsync(long idEventoClinico);

    /// <summary>Uma cobrança da clínica do JWT, pelo id.</summary>
    Task<CobrancaResponseDto> ObterPorIdAsync(long id);
}
