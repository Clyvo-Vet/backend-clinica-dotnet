namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IPetRepository : IRepository<Pet>
{
    Task<IEnumerable<Pet>> GetByFiltersAsync(long? tutorId, long? especieId, char? porte);
    Task<Pet?> GetByIdWithTutoresAsync(long id);
}
