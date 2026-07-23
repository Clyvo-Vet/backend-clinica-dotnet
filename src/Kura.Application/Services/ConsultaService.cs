namespace Kura.Application.Services;

using Kura.Application.DTOs.EventoClinico;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class ConsultaService : IConsultaService
{
    private const string CdTipoConsulta = "CONSULTA";

    private readonly IRepository<Consulta> _consultaRepository;
    private readonly IPetRepository _petRepository;
    private readonly IVeterinarioRepository _veterinarioRepository;
    private readonly IUnitOfWork _uow;
    private readonly IClinicaContext _clinicaContext;
    private readonly ITipoEventoService _tipoEventoService;

    public ConsultaService(
        IRepository<Consulta> consultaRepository,
        IPetRepository petRepository,
        IVeterinarioRepository veterinarioRepository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext,
        ITipoEventoService tipoEventoService)
    {
        _consultaRepository = consultaRepository;
        _petRepository = petRepository;
        _veterinarioRepository = veterinarioRepository;
        _uow = uow;
        _clinicaContext = clinicaContext;
        _tipoEventoService = tipoEventoService;
    }

    public async Task<ConsultaResponseDto> CriarConsultaAsync(ConsultaCreateDto dto)
    {
        _ = await _petRepository.GetByIdAsync(dto.IdPet)
            ?? throw new EntidadeNaoEncontradaException("Pet", dto.IdPet);

        _ = await _veterinarioRepository.GetByIdAsync(dto.IdVeterinario)
            ?? throw new EntidadeNaoEncontradaException("Veterinario", dto.IdVeterinario);

        var idTipoEvento = await _tipoEventoService.GetIdByCdTipoAsync(CdTipoConsulta);

        var evento = new EventoClinico
        {
            IdClinica = _clinicaContext.IdClinica,
            IdPet = dto.IdPet,
            IdVeterinario = dto.IdVeterinario,
            IdTipoEvento = idTipoEvento,
            DtEvento = dto.DtConsulta,
            DsObservacao = dto.DsObservacao ?? string.Empty
        };

        // Navigation property — EF Core insere EventoClinico primeiro (FK ordering)
        var consulta = new Consulta
        {
            EventoClinico = evento,
            DsMotivo = dto.DsMotivo,
            DsAnamnese = dto.DsAnamnese,
            DsExameFisico = dto.DsExameFisico,
            DsDiagnostico = dto.DsDiagnostico,
            DtConsulta = dto.DtConsulta
        };

        await _consultaRepository.AddAsync(consulta);
        await _uow.CommitAsync();

        return BuildResponse(evento, consulta);
    }

    private static ConsultaResponseDto BuildResponse(EventoClinico evento, Consulta consulta) => new()
    {
        IdEventoClinico = evento.Id,
        IdConsulta = consulta.Id,
        IdPet = evento.IdPet,
        IdVeterinario = evento.IdVeterinario,
        DtConsulta = consulta.DtConsulta,
        DsMotivo = consulta.DsMotivo,
        DsAnamnese = consulta.DsAnamnese,
        DsExameFisico = consulta.DsExameFisico,
        DsDiagnostico = consulta.DsDiagnostico,
        DsObservacao = evento.DsObservacao,
        StAtiva = consulta.StAtiva
    };
}
