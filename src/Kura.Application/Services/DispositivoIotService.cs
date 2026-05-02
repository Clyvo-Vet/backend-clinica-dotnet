namespace Kura.Application.Services;

using Kura.Application.DTOs.DispositivoIot;
using Kura.Application.DTOs.Iot;
using Kura.Application.DTOs.LeituraTemperatura;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class DispositivoIotService : IDispositivoIotService
{
    private readonly IRepository<DispositivoIot> _repository;
    private readonly IRepository<LeituraTemperatura> _leituraRepository;
    private readonly IUnitOfWork _uow;

    public DispositivoIotService(
        IRepository<DispositivoIot> repository,
        IRepository<LeituraTemperatura> leituraRepository,
        IUnitOfWork uow)
    {
        _repository = repository;
        _leituraRepository = leituraRepository;
        _uow = uow;
    }

    public async Task<IEnumerable<DispositivoIotResponseDto>> GetAllAsync()
    {
        var dispositivos = await _repository.GetAllAsync();
        return dispositivos.Select(ToResponse);
    }

    public async Task<IEnumerable<DispositivoIotResponseDto>> GetByClinicaAsync(long idClinica)
    {
        var dispositivos = await _repository.FindAsync(d => d.IdClinica == idClinica);
        return dispositivos.Select(ToResponse);
    }

    public async Task<DispositivoIotResponseDto> GetByIdAsync(long id)
    {
        var dispositivo = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("DispositivoIot", id);
        return ToResponse(dispositivo);
    }

    public async Task<DispositivoIotResponseDto> CreateAsync(DispositivoIotCreateDto dto)
    {
        var dispositivo = new DispositivoIot
        {
            IdClinica = dto.IdClinica,
            CdDispositivo = dto.CdDispositivo,
            DsDescricao = dto.DsDescricao,
            DsLocalizacao = dto.DsLocalizacao
        };
        await _repository.AddAsync(dispositivo);
        await _uow.CommitAsync();
        return ToResponse(dispositivo);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var dispositivo = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("DispositivoIot", id);
        _repository.SoftDelete(dispositivo);
        await _uow.CommitAsync();
    }

    public async Task<DispositivoStatusDto> GetStatusAsync(long id)
    {
        var dispositivo = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("DispositivoIot", id);

        var leituras = await _leituraRepository.FindAsync(l => l.IdDispositivoIot == id);
        var ultima = leituras.OrderByDescending(l => l.DtLeitura).FirstOrDefault();

        var status = ultima is null ? "SEM_DADOS"
            : (ultima.VlTemperatura >= 2.0m && ultima.VlTemperatura <= 8.0m) ? "OK" : "FORA_FAIXA";

        return new DispositivoStatusDto
        {
            IdDispositivo = id,
            CdDispositivo = dispositivo.CdDispositivo,
            Status = status,
            UltimaLeitura = ultima is null ? null : new LeituraTemperaturaResponseDto
            {
                Id = ultima.Id,
                IdDispositivoIot = ultima.IdDispositivoIot,
                VlTemperatura = ultima.VlTemperatura,
                VlUmidade = ultima.VlUmidade,
                DtLeitura = ultima.DtLeitura
            }
        };
    }

    private static DispositivoIotResponseDto ToResponse(DispositivoIot d) => new()
    {
        Id = d.Id,
        IdClinica = d.IdClinica,
        CdDispositivo = d.CdDispositivo,
        DsDescricao = d.DsDescricao,
        DsLocalizacao = d.DsLocalizacao,
        StAtiva = d.StAtiva
    };
}
