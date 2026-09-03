namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.ServicoPreco;

/// <summary>
/// FD-09 — CRUD da tabela de preços. Nenhum método recebe <c>IdClinica</c>: o escopo sai do
/// JWT, dentro do service.
/// </summary>
public interface IServicoPrecoService
{
    /// <summary>
    /// FD-16 — <c>incluirInativos=false</c> (default) preserva o comportamento anterior: só
    /// os ativos. <c>true</c> inclui também os desativados, sem deixar de escopar por
    /// clínica — ver
    /// <see cref="Kura.Domain.Interfaces.IServicoPrecoRepository.ListarDaClinicaAsync"/>.
    /// </summary>
    Task<IEnumerable<ServicoPrecoResponseDto>> ListarAsync(bool incluirInativos = false);

    Task<ServicoPrecoResponseDto> ObterPorIdAsync(long id);

    Task<ServicoPrecoResponseDto> CriarAsync(ServicoPrecoCreateDto dto);

    Task<ServicoPrecoResponseDto> AtualizarAsync(long id, ServicoPrecoUpdateDto dto);

    Task DesativarAsync(long id);

    Task<ServicoPrecoResponseDto> ReativarAsync(long id);
}
