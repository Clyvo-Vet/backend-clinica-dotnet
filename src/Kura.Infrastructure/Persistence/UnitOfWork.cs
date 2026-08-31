namespace Kura.Infrastructure.Persistence;

using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public class UnitOfWork : IUnitOfWork
{
    private readonly KuraDbContext _context;
    private IDbContextTransaction? _transaction;

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

    public async Task BeginTransactionAsync()
    {
        // Nota (TASK-30): o provider EF Core InMemory (usado nos testes deste
        // projeto) não implementa transações relacionais — Database.BeginTransactionAsync
        // lança InvalidOperationException nele. Este método só é exercitado de fato
        // contra um provider relacional (Oracle em produção). Os testes de
        // orquestração do AuthService usam um IUnitOfWork fake, não este.
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>Database.IsRelational()</c> é a pergunta certa (e não <c>is InMemoryDatabase</c>):
    /// ela é sobre a CAPACIDADE que o chamador precisa — transação + SQL cru —, não sobre o
    /// nome de um provider específico. Um provider relacional novo (SQLite num teste futuro)
    /// passa a valer automaticamente; um não relacional novo degrada sozinho.
    /// </remarks>
    public async Task<bool> TryBeginTransactionAsync()
    {
        if (!_context.Database.IsRelational())
            return false;

        _transaction = await _context.Database.BeginTransactionAsync();
        return true;
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction is null)
            return;

        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        // DbContext lifetime is otherwise managed by DI (scoped) — no-op beyond the transaction.
    }
}
