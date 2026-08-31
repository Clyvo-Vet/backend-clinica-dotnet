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

    /// <summary>
    /// FD-13 — <b>igual a <see cref="BeginTransactionAsync"/>, mas degrada em vez de explodir
    /// quando o provider não é relacional</b>, devolvendo <c>true</c> só quando uma transação
    /// de banco REAL foi aberta.
    ///
    /// <para>🔴 <b>Existe porque o invariante do último gestor precisa de uma transação para
    /// segurar um lock pessimista, e a suíte inteira deste repo roda sobre o provider
    /// InMemory</b>, que não modela transação nem isolamento. Sem este método, o caminho de
    /// produção teria de ser desviado por <c>if (ehTeste)</c> — o anti-padrão que faz o teste
    /// exercitar um código diferente do que roda em produção.</para>
    ///
    /// <para>⚠️ <b>O que ele NÃO faz:</b> devolver <c>false</c> significa <b>sem atomicidade e
    /// sem lock</b>. Quem chama tem de tratar isso como "esta garantia não existe aqui", nunca
    /// como sucesso. Por isso o retorno é <c>bool</c> e não <c>void</c>: a ausência da
    /// transação fica visível no call site em vez de virar um silêncio.</para>
    /// </summary>
    /// <returns><c>true</c> se uma transação de banco foi de fato aberta.</returns>
    Task<bool> TryBeginTransactionAsync();

    /// <summary>Confirma a transação aberta por <see cref="BeginTransactionAsync"/>.</summary>
    Task CommitTransactionAsync();

    /// <summary>Desfaz tudo que foi gravado desde <see cref="BeginTransactionAsync"/>.</summary>
    Task RollbackTransactionAsync();
}
