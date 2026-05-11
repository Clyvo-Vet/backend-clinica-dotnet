namespace Kura.Domain.Exceptions;

public class ConflitoConcorrenciaException : DomainException
{
    public ConflitoConcorrenciaException(string entidade, long id)
        : base($"{entidade} id {id} foi modificado por outro processo. Atualize e tente novamente.") { }

    public ConflitoConcorrenciaException()
        : base("O registro foi modificado por outro processo. Atualize e tente novamente.") { }
}
