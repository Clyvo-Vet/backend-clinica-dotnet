namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Financeiro;

/// <summary>FD-11 — KPI financeiros agregados da clínica do JWT.</summary>
public interface IFinanceiroService
{
    /// <summary>
    /// Os 4 KPI do período, numa resposta só. As duas datas são <b>inclusivas</b>; a clínica
    /// sai do JWT e <b>não</b> é parâmetro. Ver <see cref="ResumoFinanceiroResponseDto"/>.
    /// </summary>
    Task<ResumoFinanceiroResponseDto> ObterResumoAsync(DateOnly de, DateOnly ate);
}
