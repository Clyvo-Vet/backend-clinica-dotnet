namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.DispositivoIot;
using Kura.Application.DTOs.Iot;

public interface IDispositivoIotService
{
    Task<IEnumerable<DispositivoIotResponseDto>> GetAllAsync();
    Task<IEnumerable<DispositivoIotResponseDto>> GetByClinicaAsync(long idClinica);
    Task<DispositivoIotResponseDto> GetByIdAsync(long id);
    Task<DispositivoIotResponseDto> CreateAsync(DispositivoIotCreateDto dto);
    Task SoftDeleteAsync(long id);
    Task<DispositivoStatusDto> GetStatusAsync(long id);
}
