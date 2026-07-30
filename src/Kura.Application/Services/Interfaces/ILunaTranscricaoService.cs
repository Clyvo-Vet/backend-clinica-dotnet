namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Transcricao;

public interface ILunaTranscricaoService
{
    /// <summary>
    /// Envia o áudio da consulta à Luna para transcrição (Whisper) + draft SOAP.
    /// Nunca lança em caso de falha da Luna — retorna transcrição/soap nulos
    /// para edição manual pelo vet (ver TASK-12/kura-luna-ai).
    /// </summary>
    Task<TranscricaoResultDto> TranscreverAsync(Stream audio, string nomeArquivo, string contentType);
}
