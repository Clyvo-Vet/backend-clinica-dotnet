namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Transcricao;

public interface ISoapDraftService
{
    /// <summary>
    /// Envia o áudio à Luna (transcrição + draft SOAP heurístico) e persiste o
    /// resultado como rascunho não confirmado (ST_SOAP_CONFIRMADO='N').
    /// </summary>
    Task<EventoClinicoSoapResponseDto> EnviarTranscricaoAsync(
        long idEventoClinico, Stream audio, string nomeArquivo, string contentType);

    /// <summary>
    /// Confirmação explícita do vet: grava o texto SOAP revisado e marca
    /// ST_SOAP_CONFIRMADO='S'. Nunca acontece automaticamente.
    /// </summary>
    Task<EventoClinicoSoapResponseDto> ConfirmarSoapAsync(long idEventoClinico, SoapConfirmarDto dto);
}
