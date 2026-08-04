namespace Kura.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    Task<int> CommitAsync();

    /// <summary>
    /// Inicia uma transação de banco explícita. Usar quando mais de uma escrita
    /// (mais de um <see cref="CommitAsync"/>) precisa ser atômica — ex.:
    /// <c>AuthService.RegisterClinicaAsync</c>, que grava Clinica e depois
    /// Veterinario (TASK-30). Sem isso, falha na segunda escrita deixa a
    /// primeira "vazando" no banco (clínica órfã, sem veterinário).
    /// </summary>
    Task BeginTransactionAsync();

    /// <summary>Confirma a transação aberta por <see cref="BeginTransactionAsync"/>.</summary>
    Task CommitTransactionAsync();

    /// <summary>Desfaz tudo que foi gravado desde <see cref="BeginTransactionAsync"/>.</summary>
    Task RollbackTransactionAsync();
}
