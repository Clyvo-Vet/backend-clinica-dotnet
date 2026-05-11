namespace Kura.Infrastructure.Persistence;

using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class UnitOfWork : IUnitOfWork
{
    private readonly KuraDbContext _context;

    public UnitOfWork(KuraDbContext context)
    {
        _context = context;
    }

    public async Task<int> CommitAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflitoConcorrenciaException();
        }
    }

    public void Dispose()
    {
        // No-op: the DbContext lifetime is managed by DI (scoped).
    }
}
