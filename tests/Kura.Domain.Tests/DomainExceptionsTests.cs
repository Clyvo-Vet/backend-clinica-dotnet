namespace Kura.Domain.Tests;

using FluentAssertions;
using Kura.Domain.Exceptions;

/// <summary>
/// Cobre o invariante de mensagem das exceções de domínio usadas para sinalizar
/// violações de regra de negócio, conflito de concorrência otimista (NrVersion) e
/// entidade não encontrada. O texto da mensagem é contrato observável (propagado
/// para respostas HTTP), então mudanças de formato aqui devem ser deliberadas.
/// </summary>
public class DomainExceptionsTests
{
    [Fact]
    public void ConflitoConcorrenciaException_ComEntidadeEId_MontaMensagemComOsDoisValores()
    {
        // Act
        var ex = new ConflitoConcorrenciaException("Agendamento", 42);

        // Assert
        ex.Message.Should().Be(
            "Agendamento id 42 foi modificado por outro processo. Atualize e tente novamente.");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void ConflitoConcorrenciaException_SemArgumentos_UsaMensagemGenerica()
    {
        // Act
        var ex = new ConflitoConcorrenciaException();

        // Assert
        ex.Message.Should().Be(
            "O registro foi modificado por outro processo. Atualize e tente novamente.");
    }

    [Fact]
    public void EntidadeNaoEncontradaException_ComIdNumerico_MontaMensagemComId()
    {
        // Act
        var ex = new EntidadeNaoEncontradaException("Pet", 7L);

        // Assert
        ex.Message.Should().Be("Pet com id 7 não encontrado.");
    }

    [Fact]
    public void EntidadeNaoEncontradaException_ComCodigoDeNegocio_MontaMensagemComCodigo()
    {
        // Act
        var ex = new EntidadeNaoEncontradaException("TipoEvento", "CONSULTA");

        // Assert
        ex.Message.Should().Be("TipoEvento com código 'CONSULTA' não encontrado.");
    }

    [Fact]
    public void RegraDeNegocioException_ComMensagemPersonalizada_PropagaMensagemRecebida()
    {
        // Act
        var ex = new RegraDeNegocioException("Agendamento fora do horário de funcionamento da clínica.");

        // Assert
        ex.Message.Should().Be("Agendamento fora do horário de funcionamento da clínica.");
        ex.Should().BeAssignableTo<DomainException>();
    }
}
