namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Common;
using Kura.Application.DTOs.Medicamento;

public interface IMedicamentoService
{
    Task<PagedResultDto<MedicamentoResponseDto>> ListarAsync(string? busca, int page, int pageSize);
    Task<IEnumerable<MedicamentoResponseDto>> SearchAsync(string? busca);
    Task<MedicamentoResponseDto> GetByIdAsync(long id);
    Task<MedicamentoResponseDto> CreateAsync(MedicamentoCreateDto dto);
    Task SoftDeleteAsync(long id);
}
