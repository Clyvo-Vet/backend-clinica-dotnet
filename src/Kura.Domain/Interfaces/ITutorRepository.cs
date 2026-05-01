namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface ITutorRepository : IRepository<Tutor>
{
    Task<IEnumerable<Tutor>> SearchAsync(string busca);
}
