namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Teleconsulta;

public interface IDailyService
{
    Task<DailyRoomResult> CriarSalaAsync(string nomeSala);
}
