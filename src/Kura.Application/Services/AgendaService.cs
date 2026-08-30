namespace Kura.Application.Services;

using Kura.Application.DTOs.Agenda;
using Kura.Application.Services.Interfaces;
using Kura.CrossCutting.Observability;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class AgendaService : IAgendaService
{
    private readonly IAgendamentoReadRepository _readRepository;
    private readonly IAgendamentoRepository _agendamentoRepository;
    private readonly IClinicaContext _clinicaContext;
    private readonly IUnitOfWork _uow;

    /// <summary>
    /// FD-06 — <b>máquina de estados de <c>AGENDAMENTO.ST_STATUS</c> do lado <c>.NET</c>.</b>
    /// Chave = status atual da linha; valor = destinos alcançáveis a partir dele.
    ///
    /// <para>
    /// 🔴 <b>Por que a lista de destinos do validator NÃO basta.</b> O validator só enxerga o
    /// corpo da requisição; ele não sabe de onde o agendamento está saindo. Sem esta tabela,
    /// <c>StatusFinais</c> seria a única regra e <c>INTENCAO → REALIZADO</c> passaria com 200 —
    /// um atendimento faturável nascido de um lead que nunca virou agendamento. A trilha
    /// financeira deste mesmo ciclo (FD-10/FD-11) fatura exatamente sobre esse dado.
    /// </para>
    ///
    /// <para><b>As decisões, uma a uma:</b></para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>CONFIRMADO</c> só a partir de <c>AGENDADO</c> — cópia literal da guarda do outro
    ///     dono da tabela compartilhada (<c>Agendamento.java</c>, <c>confirmar()</c> exige
    ///     status <b>exatamente</b> <c>AGENDADO</c>). Divergir aqui faria o mesmo gesto ser
    ///     aceito por um backend e recusado pelo outro.
    ///   </description></item>
    ///   <item><description>
    ///     <c>NAO_COMPARECEU</c> a partir de <c>AGENDADO</c> ou <c>CONFIRMADO</c> — «faltou» só
    ///     tem sentido para quem tinha hora marcada. A partir de <c>INTENCAO</c> não: um lead
    ///     que nunca virou compromisso não pode faltar a ele, e aceitar isso encheria de faltas
    ///     falsas a base sobre a qual uma política de no-show seria construída.
    ///   </description></item>
    ///   <item><description>
    ///     <c>REALIZADO</c> a partir de <c>AGENDADO</c> ou <c>CONFIRMADO</c> — mesmo argumento,
    ///     e é o par que fecha <c>INTENCAO → REALIZADO</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <c>CANCELADO</c> a partir de <c>INTENCAO</c>, <c>AGENDADO</c> ou <c>CONFIRMADO</c> —
    ///     exatamente as três origens que o <c>cancelar()</c> do Java aceita depois da FD-06.
    ///     Cancelar um lead é legítimo: é como ele morre.
    ///   </description></item>
    /// </list>
    ///
    /// <para>
    /// ⚠️ <b>Estado de origem desconhecido (ou nulo) é recusado, não ignorado.</b> A coluna é
    /// <c>NOT NULL DEFAULT 'AGENDADO'</c> com <c>CHECK</c> nos seis valores, então uma origem
    /// fora deste mapa é sinal de que o mapa envelheceu — e nesse caso a resposta certa é parar,
    /// não escolher um caminho.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> TransicoesPermitidas =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["INTENCAO"] = ["CANCELADO"],
            ["AGENDADO"] = ["CONFIRMADO", "REALIZADO", "CANCELADO", "NAO_COMPARECEU"],
            ["CONFIRMADO"] = ["REALIZADO", "CANCELADO", "NAO_COMPARECEU"],
            ["REALIZADO"] = [],
            ["CANCELADO"] = [],
            ["NAO_COMPARECEU"] = [],
        };

    /// <summary>
    /// Estados terminais — <b>derivados</b> do mapa acima (origem sem nenhum destino), nunca
    /// mantidos à mão.
    ///
    /// <para>
    /// 🔴 <b>É a regra de ouro v7 do projeto aplicada ao caso que a FD-06 criou.</b> Até esta
    /// task esta lista era <c>["REALIZADO", "CANCELADO"]</c> escrita literalmente, e estava
    /// certa por acidente: o validator tornava <c>NAO_COMPARECEU</c> <b>inalcançável</b>, então
    /// ninguém precisou lembrar dele aqui. Afrouxar o validator sem tocar nesta linha deixaria
    /// um agendamento marcado como falta virar <c>REALIZADO</c> depois — dado falso com cara de
    /// dado certo. Derivando, acrescentar um estado terminal ao mapa é suficiente: não há
    /// segunda lista para esquecer.
    /// </para>
    /// </summary>
    private static readonly IReadOnlySet<string> StatusFinais =
        TransicoesPermitidas
            .Where(par => par.Value.Length == 0)
            .Select(par => par.Key)
            .ToHashSet(StringComparer.Ordinal);

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
        // S3D-04b: span-filho de camada Application, aninhado sob o span HTTP
        // (AddAspNetCoreInstrumentation, S3D-04) por Activity.Current — sem passagem
        // manual de contexto, é o comportamento padrão do System.Diagnostics.Activity.
        using var activity = KuraActivitySource.Instancia.StartActivity("Application.AgendaService.GetAgendaAsync");
        activity?.SetTag("kura.layer", "Application");
        activity?.SetTag("kura.intervalo_dias", (dataFim - dataInicio).TotalDays);

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

        if (agendamento.StStatus is not null && StatusFinais.Contains(agendamento.StStatus))
            throw new RegraDeNegocioException(
                $"Agendamento {id} já está em estado final ({agendamento.StStatus}) e não pode ser alterado.");

        // FD-06 — a origem manda tanto quanto o destino. Ver TransicoesPermitidas.
        if (agendamento.StStatus is null
            || !TransicoesPermitidas.TryGetValue(agendamento.StStatus, out var destinosPermitidos))
            throw new RegraDeNegocioException(
                $"Agendamento {id} está com status atual não reconhecido "
                + $"('{agendamento.StStatus ?? "null"}') e não pode ter o status alterado.");

        if (!destinosPermitidos.Contains(dto.DsStatus, StringComparer.Ordinal))
            throw new RegraDeNegocioException(
                $"Transição de status inválida para o agendamento {id}: "
                + $"{agendamento.StStatus} -> {dto.DsStatus}. A partir de {agendamento.StStatus} "
                + $"só é possível ir para: {string.Join(", ", destinosPermitidos)}.");

        if (dto.NrVersion != agendamento.NrVersion)
            throw new ConflitoConcorrenciaException("Agendamento", id);

        agendamento.StStatus = dto.DsStatus;
        agendamento.NrVersion = dto.NrVersion + 1;

        _agendamentoRepository.Update(agendamento);
        await _uow.CommitAsync();
        return ToItemDto(agendamento);
    }

    private static AgendamentoItemDto ToItemDto(Domain.Entities.Agendamento a) => new()
    {
        IdAgendamento = a.Id,
        DtAgendamento = a.DtAgendamento,
        DuracaoMinutos = a.NrDuracaoMinutos ?? 0,
        NmTutor = a.Tutor?.NmTutor ?? string.Empty,
        NmPet = a.Pet?.NmPet ?? string.Empty,
        IdVeterinario = a.IdVeterinario ?? 0,
        NmVeterinario = a.Veterinario?.NmVeterinario ?? string.Empty,
        DsTipoConsulta = a.DsTipoConsulta ?? string.Empty,
        DsStatus = a.StStatus ?? string.Empty,
        NrVersion = a.NrVersion
    };
}
