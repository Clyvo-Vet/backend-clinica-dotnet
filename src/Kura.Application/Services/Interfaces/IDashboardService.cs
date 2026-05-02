namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Dashboard;

public interface IDashboardService
{
    Task<DashboardHojeDto> GetHojeAsync();
    Task<IEnumerable<object>> GetAlertasAsync();
    Task<DashboardHojeDto> GetRecentesAsync();
}
