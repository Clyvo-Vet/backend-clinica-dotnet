namespace Kura.Application.Services;

using Kura.Application.DTOs.Dashboard;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;

public sealed class DashboardService : IDashboardService
{
    private readonly IEventoClinicoRepository _eventoRepository;
    private readonly IRepository<AlertaTemperatura> _alertaRepository;
    private readonly IRepository<Pet> _petRepository;
    private readonly IRepository<Vacina> _vacinaRepository;
    private readonly IAgendamentoRepository _agendamentoRepository;

    public DashboardService(
        IEventoClinicoRepository eventoRepository,
        IRepository<AlertaTemperatura> alertaRepository,
        IRepository<Pet> petRepository,
        IRepository<Vacina> vacinaRepository,
        IAgendamentoRepository agendamentoRepository)
    {
        _eventoRepository = eventoRepository;
        _alertaRepository = alertaRepository;
        _petRepository = petRepository;
        _vacinaRepository = vacinaRepository;
        _agendamentoRepository = agendamentoRepository;
    }

    public async Task<DashboardHojeDto> GetHojeAsync()
    {
        var hoje = DateTime.UtcNow.Date;
        var todosEventos = await _eventoRepository.GetByFiltersAsync(null, null, null, null, null);
        var consultasHoje = todosEventos.Count(e => e.DtEvento.Date == hoje);
        var retornosPendentes = todosEventos.Count(e => e.DtEvento.Date > hoje);

        var alertas = await _alertaRepository.GetAllAsync();
        var alertasAtivos = alertas.Count(a => a.StResolvido == 'N');

        var ultimosPets = todosEventos
            .OrderByDescending(e => e.DtEvento)
            .DistinctBy(e => e.IdPet)
            .Take(5)
            .Select(e => new PetResumoDto
            {
                Id = e.IdPet,
                NmPet = e.Pet?.NmPet ?? string.Empty,
                UltimoAtendimento = e.DtEvento
            })
            .ToList();

        var proximosAgendamentos = (await _agendamentoRepository.GetProximosDoDiaAsync(DateTime.UtcNow, 3))
            .Select(a => new AgendamentoResumoDto
            {
                Id = a.Id,
                NmPaciente = a.NmPaciente,
                DtAgendamento = a.DtAgendamento,
                DsServico = a.DsServico,
                StStatus = a.StStatus
            })
            .ToList();

        return new DashboardHojeDto
        {
            TotalConsultasHoje = consultasHoje,
            TotalAlertasAtivos = alertasAtivos,
            TotalRetornosPendentes = retornosPendentes,
            UltimosPetsAtendidos = ultimosPets,
            ProximosAgendamentos = proximosAgendamentos
        };
    }

    public async Task<IEnumerable<object>> GetAlertasAsync()
    {
        var alertasTemp = await _alertaRepository.FindAsync(a => a.StResolvido == 'N');

        var hoje = DateTime.UtcNow.Date;
        var limite30Dias = hoje.AddDays(30);
        var vacinas = await _vacinaRepository.FindAsync(
            v => v.DtProximaDose.HasValue &&
                 v.DtProximaDose.Value.Date >= hoje &&
                 v.DtProximaDose.Value.Date <= limite30Dias);

        var resultado = new List<object>();
        resultado.AddRange(alertasTemp.Select(a => (object)new
        {
            Tipo = "TEMPERATURA",
            a.Id,
            a.DsTipoAlerta,
            a.DsMensagem,
            a.DtCriacao
        }));
        resultado.AddRange(vacinas.Select(v => (object)new
        {
            Tipo = "VACINA_VENCENDO",
            v.Id,
            DsTipoAlerta = "PROXIMA_DOSE",
            DsMensagem = $"Vacina '{v.NmVacina}' com próxima dose em {v.DtProximaDose:dd/MM/yyyy}.",
            v.DtCriacao
        }));

        return resultado;
    }

    public async Task<DashboardHojeDto> GetRecentesAsync() => await GetHojeAsync();
}
