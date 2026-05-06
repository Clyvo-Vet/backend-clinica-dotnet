namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.EventoClinico;

public interface IConsultaService
{
    Task<ConsultaResponseDto> CriarConsultaAsync(ConsultaCreateDto dto);
}
