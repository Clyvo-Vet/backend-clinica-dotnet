namespace Kura.Application.Services;

using Kura.Application.DTOs.Luna;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class LunaService : ILunaService
{
    private const int MaxIntervaloDias = 90;

    private readonly ITriagemLunaRepository _repository;

    public LunaService(ITriagemLunaRepository repository)
    {
        _repository = repository;
    }

    public async Task<RelatorioTriagensDto> GerarRelatorioAsync(DateTime dataInicio, DateTime dataFim)
    {
        if (dataFim < dataInicio)
            throw new RegraDeNegocioException("DataFim não pode ser anterior à DataInicio.");

        if ((dataFim - dataInicio).TotalDays > MaxIntervaloDias)
            throw new RegraDeNegocioException($"Intervalo máximo de {MaxIntervaloDias} dias.");

        var triagens = await _repository.GetByIntervaloAsync(dataInicio, dataFim);

        var porUrgencia = triagens
            .GroupBy(t => t.DsNivelUrgencia)
            .ToDictionary(g => g.Key, g => g.Count());

        return new RelatorioTriagensDto
        {
            DataInicio = dataInicio,
            DataFim = dataFim,
            TotalTriagens = triagens.Count,
            PorUrgencia = porUrgencia,
            EncaminhadasParaVet = triagens.Count(t => t.StEncaminhadoVet)
        };
    }
}
