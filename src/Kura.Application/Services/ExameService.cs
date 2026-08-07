namespace Kura.Application.Services;

using Kura.Application.DTOs.Exame;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class ExameService : IExameService
{
    private const string CdTipoExame = "EXAME";

    private readonly IEventoClinicoRepository _eventoRepository;
    private readonly IRepository<Exame> _exameRepository;
    private readonly IUnitOfWork _uow;
    private readonly IClinicaContext _clinicaContext;
    private readonly ITipoEventoService _tipoEventoService;

    public ExameService(
        IEventoClinicoRepository eventoRepository,
        IRepository<Exame> exameRepository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext,
        ITipoEventoService tipoEventoService)
    {
        _eventoRepository = eventoRepository;
        _exameRepository = exameRepository;
        _uow = uow;
        _clinicaContext = clinicaContext;
        _tipoEventoService = tipoEventoService;
    }

    public async Task<ExameResponseDto> CreateAsync(ExameCreateDto dto)
    {
        var idTipoEvento = await _tipoEventoService.GetIdByCdTipoAsync(CdTipoExame);

        var evento = new EventoClinico
        {
            IdClinica = _clinicaContext.IdClinica,
            IdPet = dto.IdPet,
            IdVeterinario = dto.IdVeterinario,
            IdTipoEvento = idTipoEvento,
            DtEvento = dto.DtEvento,
            // TASK-56: EVENTO_CLINICO.DS_OBSERVACAO é NOT NULL (V9:58, migration imutável) e o
            // Oracle trata VARCHAR2 vazio como NULL — sem este coalesce, um payload sem observação
            // estoura ORA-01400 (500). Observação é opcional do ponto de vista clínico, então a
            // restrição de armazenamento se resolve aqui, não no contrato com o cliente.
            DsObservacao = string.IsNullOrWhiteSpace(dto.DsObservacao)
                ? "Sem observações"
                : dto.DsObservacao
        };

        // Navigation property — EF Core insere EventoClinico primeiro (FK ordering)
        var exame = new Exame
        {
            EventoClinico = evento,
            NmExame = dto.NmExame,
            DsResultado = dto.DsResultado,
            DtRealizacao = dto.DtRealizacao
        };

        await _exameRepository.AddAsync(exame);
        await _uow.CommitAsync();

        return BuildResponse(evento, exame);
    }

    public async Task<ExameResponseDto> GetByEventoClinicoAsync(long idEvento)
    {
        var exames = await _exameRepository.FindAsync(e => e.IdEventoClinico == idEvento);
        var exame = exames.FirstOrDefault()
            ?? throw new EntidadeNaoEncontradaException("Exame", idEvento);

        var evento = await _eventoRepository.GetByIdAsync(exame.IdEventoClinico)
            ?? throw new EntidadeNaoEncontradaException("EventoClinico", exame.IdEventoClinico);

        return BuildResponse(evento, exame);
    }

    public async Task<IEnumerable<ExameResponseDto>> GetByPetAsync(long idPet)
    {
        var idTipoEvento = await _tipoEventoService.GetIdByCdTipoAsync(CdTipoExame);
        var eventos = await _eventoRepository.GetByFiltersAsync(idPet, idTipoEvento, null, null, null);
        var result = new List<ExameResponseDto>();
        foreach (var evento in eventos)
        {
            var exames = await _exameRepository.FindAsync(e => e.IdEventoClinico == evento.Id);
            var exame = exames.FirstOrDefault();
            if (exame is not null)
                result.Add(BuildResponse(evento, exame));
        }
        return result;
    }

    private static ExameResponseDto BuildResponse(EventoClinico evento, Exame exame) => new()
    {
        IdEventoClinico = evento.Id,
        Id = exame.Id,
        IdPet = evento.IdPet,
        IdVeterinario = evento.IdVeterinario,
        DtEvento = evento.DtEvento,
        DsObservacao = evento.DsObservacao,
        NmExame = exame.NmExame,
        DsResultado = exame.DsResultado,
        DtRealizacao = exame.DtRealizacao,
        StAtiva = exame.StAtiva
    };
}
