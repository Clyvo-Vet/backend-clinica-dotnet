namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.LeituraTemperatura;

public interface ILeituraTemperaturaService
{
    Task<LeituraTemperaturaResponseDto> RegistrarLeituraAsync(LeituraTemperaturaCreateDto dto);
    Task<IEnumerable<LeituraTemperaturaResponseDto>> GetByDispositivoAsync(
        long idDispositivo, DateTime? dataInicio, DateTime? dataFim);
}
