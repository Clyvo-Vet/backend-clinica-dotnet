namespace Kura.Infrastructure.Persistence.Interceptors;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public class ReadOnlyTablesInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<Type> ReadOnlyTypes = new()
    {
        typeof(ContaTutor),
        typeof(Consentimento)
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ValidarEscrita(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        ValidarEscrita(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private static void ValidarEscrita(DbContext? context)
    {
        if (context is null) return;

        var entriesInvalidas = context.ChangeTracker.Entries()
            .Where(e => ReadOnlyTypes.Contains(e.Entity.GetType())
                     && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entriesInvalidas.Count > 0)
        {
            var tipo = entriesInvalidas[0].Entity.GetType().Name;
            throw new InvalidOperationException(
                $"Tentativa de escrita em tabela read-only: {tipo}. " +
                "Esta tabela é gerenciada pelo backend Java.");
        }
    }
}
