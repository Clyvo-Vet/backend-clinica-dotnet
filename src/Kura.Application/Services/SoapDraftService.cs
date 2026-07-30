namespace Kura.Application.Services;

using Kura.Application.DTOs.Transcricao;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class SoapDraftService : ISoapDraftService
{
    private readonly IEventoClinicoRepository _repository;
    private readonly ILunaTranscricaoService _lunaTranscricaoService;
    private readonly IUnitOfWork _uow;

    public SoapDraftService(
        IEventoClinicoRepository repository,
        ILunaTranscricaoService lunaTranscricaoService,
        IUnitOfWork uow)
    {
        _repository = repository;
        _lunaTranscricaoService = lunaTranscricaoService;
        _uow = uow;
    }

    public async Task<EventoClinicoSoapResponseDto> EnviarTranscricaoAsync(
        long idEventoClinico, Stream audio, string nomeArquivo, string contentType)
    {
        var evento = await GetEventoAsync(idEventoClinico);

        var resultado = await _lunaTranscricaoService.TranscreverAsync(audio, nomeArquivo, contentType);

        evento.DsTranscricao = resultado.Transcricao;
        evento.DsSoapS = resultado.Soap?.S;
        evento.DsSoapO = resultado.Soap?.O;
        evento.DsSoapA = resultado.Soap?.A;
        evento.DsSoapP = resultado.Soap?.P;
        evento.StSoapConfirmado = false;

        _repository.Update(evento);
        await _uow.CommitAsync();

        return ToDto(evento);
    }

    public async Task<EventoClinicoSoapResponseDto> ConfirmarSoapAsync(long idEventoClinico, SoapConfirmarDto dto)
    {
        var evento = await GetEventoAsync(idEventoClinico);

        evento.DsSoapS = dto.S;
        evento.DsSoapO = dto.O;
        evento.DsSoapA = dto.A;
        evento.DsSoapP = dto.P;
        evento.StSoapConfirmado = true;

        _repository.Update(evento);
        await _uow.CommitAsync();

        return ToDto(evento);
    }

    private async Task<EventoClinico> GetEventoAsync(long id) =>
        await _repository.GetByIdAsync(id) ?? throw new EntidadeNaoEncontradaException("EventoClinico", id);

    private static EventoClinicoSoapResponseDto ToDto(EventoClinico evento) => new()
    {
        IdEventoClinico = evento.Id,
        DsTranscricao = evento.DsTranscricao,
        Soap = new SoapDraftDto
        {
            S = evento.DsSoapS,
            O = evento.DsSoapO,
            A = evento.DsSoapA,
            P = evento.DsSoapP
        },
        StSoapConfirmado = evento.StSoapConfirmado
    };
}
