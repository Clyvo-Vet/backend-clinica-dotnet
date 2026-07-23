namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.TipoEvento;

public interface ITipoEventoService
{
    Task<IEnumerable<TipoEventoResponseDto>> GetAllAsync();
    Task<TipoEventoResponseDto> GetByIdAsync(long id);

    /// <summary>
    /// Resolve o ID_TIPO_EVENTO a partir do código de negócio (CD_TIPO).
    /// Usado pelos serviços de EventoClinico para não hardcodar IDs de FK.
    /// </summary>
    Task<long> GetIdByCdTipoAsync(string cdTipo);
}
