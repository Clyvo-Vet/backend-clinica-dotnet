namespace Kura.Domain.Exceptions;

public class EntidadeNaoEncontradaException : DomainException
{
    public EntidadeNaoEncontradaException(string entidade, long id)
        : base($"{entidade} com id {id} não encontrado.") { }

    // Sobrecarga para lookups por chave de negócio (ex.: CD_TIPO em vez de ID numérico)
    public EntidadeNaoEncontradaException(string entidade, string codigo)
        : base($"{entidade} com código '{codigo}' não encontrado.") { }
}
