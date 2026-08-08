namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Luna;

public interface ILunaService
{
    Task<RelatorioTriagensDto> GerarRelatorioAsync(DateTime dataInicio, DateTime dataFim);

    /// <summary>
    /// TASK-67: POST /api/v1/luna/interactions. Deriva ID_CLINICA do tutor (ID_CLINICA
    /// é NOT NULL e a Luna nunca envia esse campo).
    /// </summary>
    Task<InteractionResponseDto> RegistrarInteracaoAsync(InteractionRequestDto dto);

    /// <summary>
    /// TASK-67: POST /api/v1/luna/triage.
    /// </summary>
    Task<TriageResponseDto> RegistrarTriagemAsync(TriageRequestDto dto);
}
