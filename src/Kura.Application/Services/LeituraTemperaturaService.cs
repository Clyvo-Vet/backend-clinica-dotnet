namespace Kura.Application.Services;

using Kura.Application.DTOs.LeituraTemperatura;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class LeituraTemperaturaService : ILeituraTemperaturaService
{
    private const decimal TempMin = 2.0m;
    private const decimal TempMax = 8.0m;

    private readonly IRepository<LeituraTemperatura> _leituraRepository;
    private readonly IRepository<AlertaTemperatura> _alertaRepository;
    private readonly IRepository<DispositivoIot> _dispositivoRepository;
    private readonly IUnitOfWork _uow;

    public LeituraTemperaturaService(
        IRepository<LeituraTemperatura> leituraRepository,
        IRepository<AlertaTemperatura> alertaRepository,
        IRepository<DispositivoIot> dispositivoRepository,
        IUnitOfWork uow)
    {
        _leituraRepository = leituraRepository;
        _alertaRepository = alertaRepository;
        _dispositivoRepository = dispositivoRepository;
        _uow = uow;
    }

    public async Task<LeituraTemperaturaResponseDto> RegistrarLeituraAsync(LeituraTemperaturaCreateDto dto)
    {
        var dispositivo = await _dispositivoRepository.GetByIdAsync(dto.IdDispositivoIot)
            ?? throw new EntidadeNaoEncontradaException("DispositivoIot", dto.IdDispositivoIot);

        if (dispositivo.StAtiva != 'S')
            throw new RegraDeNegocioException(
                $"Dispositivo {dto.IdDispositivoIot} está inativo.");

        var leitura = new LeituraTemperatura
        {
            IdDispositivoIot = dto.IdDispositivoIot,
            VlTemperatura = dto.VlTemperatura,
            VlUmidade = dto.VlUmidade,
            DtLeitura = dto.DtLeitura
        };
        await _leituraRepository.AddAsync(leitura);

        if (dto.VlTemperatura > TempMax)
        {
            await _alertaRepository.AddAsync(new AlertaTemperatura
            {
                LeituraTemperatura = leitura,
                DsTipoAlerta = "ACIMA_LIMITE",
                VlLimite = TempMax,
                DsMensagem = $"Temperatura {dto.VlTemperatura:F1}°C acima do limite máximo de {TempMax:F1}°C.",
                StResolvido = 'N'
            });
        }
        else if (dto.VlTemperatura < TempMin)
        {
            await _alertaRepository.AddAsync(new AlertaTemperatura
            {
                LeituraTemperatura = leitura,
                DsTipoAlerta = "ABAIXO_LIMITE",
                VlLimite = TempMin,
                DsMensagem = $"Temperatura {dto.VlTemperatura:F1}°C abaixo do limite mínimo de {TempMin:F1}°C.",
                StResolvido = 'N'
            });
        }

        await _uow.CommitAsync();

        return ToResponse(leitura);
    }

    public async Task<IEnumerable<LeituraTemperaturaResponseDto>> GetByDispositivoAsync(
        long idDispositivo, DateTime? dataInicio, DateTime? dataFim)
    {
        var leituras = await _leituraRepository.FindAsync(l =>
            l.IdDispositivoIot == idDispositivo &&
            (!dataInicio.HasValue || l.DtLeitura >= dataInicio.Value) &&
            (!dataFim.HasValue || l.DtLeitura <= dataFim.Value));
        return leituras.Select(ToResponse);
    }

    private static LeituraTemperaturaResponseDto ToResponse(LeituraTemperatura l) => new()
    {
        Id = l.Id,
        IdDispositivoIot = l.IdDispositivoIot,
        VlTemperatura = l.VlTemperatura,
        VlUmidade = l.VlUmidade,
        DtLeitura = l.DtLeitura
    };
}
