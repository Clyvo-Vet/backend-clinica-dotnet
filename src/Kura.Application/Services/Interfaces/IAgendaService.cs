namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Agenda;

public interface IAgendaService
{
    Task<AgendaResponseDto> GetAgendaAsync(DateTime dataInicio, DateTime dataFim, long? idVeterinario);
}
