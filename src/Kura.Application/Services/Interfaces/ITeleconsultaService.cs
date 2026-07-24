namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Teleconsulta;

public interface ITeleconsultaService
{
    Task<TeleconsultaResponseDto> CriarOuObterSalaAsync(long idAgendamento);
    Task<TeleconsultaResponseDto> ObterSalaAsync(long idAgendamento);
}
