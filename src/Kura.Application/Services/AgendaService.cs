namespace Kura.Application.Services;

using Kura.Application.DTOs.Agenda;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class AgendaService : IAgendaService
{
    private readonly IAgendamentoReadRepository _repository;
    private readonly IClinicaContext _clinicaContext;

    public AgendaService(IAgendamentoReadRepository repository, IClinicaContext clinicaContext)
    {
        _repository = repository;
        _clinicaContext = clinicaContext;
    }

    public async Task<AgendaResponseDto> GetAgendaAsync(
        DateTime dataInicio, DateTime dataFim, long? idVeterinario)
    {
        if (dataFim < dataInicio)
            throw new RegraDeNegocioException("DataFim não pode ser anterior à DataInicio.");

        if ((dataFim - dataInicio).TotalDays > 31)
            throw new RegraDeNegocioException("Intervalo máximo de 31 dias.");

        var agendamentos = await _repository.GetByIntervaloAsync(
            _clinicaContext.IdClinica, dataInicio, dataFim, idVeterinario);

        var itens = agendamentos.Select(a => new AgendamentoItemDto
        {
            IdAgendamento = a.Id,
            DtAgendamento = a.DtAgendamento,
            DuracaoMinutos = a.NrDuracaoMinutos,
            NmTutor = a.Tutor?.NmTutor ?? string.Empty,
            NmPet = a.Pet?.NmPet ?? string.Empty,
            IdVeterinario = a.IdVeterinario ?? 0,
            NmVeterinario = a.Veterinario?.NmVeterinario ?? string.Empty,
            DsTipoConsulta = a.DsTipoConsulta,
            DsStatus = a.DsStatus
        }).ToList();

        return new AgendaResponseDto
        {
            DataInicio = dataInicio,
            DataFim = dataFim,
            Agendamentos = itens
        };
    }
}
