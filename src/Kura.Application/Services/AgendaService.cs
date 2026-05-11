namespace Kura.Application.Services;

using Kura.Application.DTOs.Agenda;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class AgendaService : IAgendaService
{
    private readonly IAgendamentoReadRepository _readRepository;
    private readonly IAgendamentoRepository _agendamentoRepository;
    private readonly IClinicaContext _clinicaContext;
    private readonly IUnitOfWork _uow;

    private static readonly HashSet<string> StatusFinais = ["REALIZADO", "CANCELADO"];

    public AgendaService(
        IAgendamentoReadRepository readRepository,
        IClinicaContext clinicaContext,
        IAgendamentoRepository agendamentoRepository,
        IUnitOfWork uow)
    {
        _readRepository = readRepository;
        _clinicaContext = clinicaContext;
        _agendamentoRepository = agendamentoRepository;
        _uow = uow;
    }

    public async Task<AgendaResponseDto> GetAgendaAsync(
        DateTime dataInicio, DateTime dataFim, long? idVeterinario)
    {
        if (dataFim < dataInicio)
            throw new RegraDeNegocioException("DataFim não pode ser anterior à DataInicio.");

        if ((dataFim - dataInicio).TotalDays > 31)
            throw new RegraDeNegocioException("Intervalo máximo de 31 dias.");

        var agendamentos = await _readRepository.GetByIntervaloAsync(
            _clinicaContext.IdClinica, dataInicio, dataFim, idVeterinario);

        var itens = agendamentos.Select(ToItemDto).ToList();

        return new AgendaResponseDto
        {
            DataInicio = dataInicio,
            DataFim = dataFim,
            Agendamentos = itens
        };
    }

    public async Task<AgendamentoItemDto> AtualizarStatusAsync(long id, AtualizarStatusAgendamentoDto dto)
    {
        var agendamento = await _agendamentoRepository.GetByIdAsync(id, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("Agendamento", id);

        if (StatusFinais.Contains(agendamento.DsStatus))
            throw new RegraDeNegocioException(
                $"Agendamento {id} já está em estado final ({agendamento.DsStatus}) e não pode ser alterado.");

        if (dto.NrVersion != agendamento.NrVersion)
            throw new ConflitoConcorrenciaException("Agendamento", id);

        agendamento.DsStatus = dto.DsStatus;
        agendamento.NrVersion = dto.NrVersion + 1;

        _agendamentoRepository.Update(agendamento);
        await _uow.CommitAsync();
        return ToItemDto(agendamento);
    }

    private static AgendamentoItemDto ToItemDto(Domain.Entities.Agendamento a) => new()
    {
        IdAgendamento = a.Id,
        DtAgendamento = a.DtAgendamento,
        DuracaoMinutos = a.NrDuracaoMinutos,
        NmTutor = a.Tutor?.NmTutor ?? string.Empty,
        NmPet = a.Pet?.NmPet ?? string.Empty,
        IdVeterinario = a.IdVeterinario ?? 0,
        NmVeterinario = a.Veterinario?.NmVeterinario ?? string.Empty,
        DsTipoConsulta = a.DsTipoConsulta,
        DsStatus = a.DsStatus,
        NrVersion = a.NrVersion
    };
}
