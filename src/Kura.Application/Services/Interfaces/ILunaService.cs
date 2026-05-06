namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Luna;

public interface ILunaService
{
    Task<RelatorioTriagensDto> GerarRelatorioAsync(DateTime dataInicio, DateTime dataFim);
}
