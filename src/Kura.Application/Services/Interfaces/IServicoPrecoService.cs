namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.ServicoPreco;

/// <summary>
/// FD-09 — CRUD da tabela de preços. Nenhum método recebe <c>IdClinica</c>: o escopo sai do
/// JWT, dentro do service.
/// </summary>
public interface IServicoPrecoService
{
    Task<IEnumerable<ServicoPrecoResponseDto>> ListarAsync();

    Task<ServicoPrecoResponseDto> ObterPorIdAsync(long id);

    Task<ServicoPrecoResponseDto> CriarAsync(ServicoPrecoCreateDto dto);

    Task<ServicoPrecoResponseDto> AtualizarAsync(long id, ServicoPrecoUpdateDto dto);

    Task DesativarAsync(long id);

    Task<ServicoPrecoResponseDto> ReativarAsync(long id);
}
