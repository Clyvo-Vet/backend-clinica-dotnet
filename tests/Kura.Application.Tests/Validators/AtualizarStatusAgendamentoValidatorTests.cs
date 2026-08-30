namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Agenda;
using Kura.Application.Validators;

/// <summary>
/// FD-06 — o validator é a metade «que valores esta API escreve» da regra; a metade «de onde
/// se pode sair» está em <c>AgendaService</c> e é provada em <c>AgendaServiceTests</c> e em
/// <c>AgendamentoStatusHttpTests</c>.
/// </summary>
public class AtualizarStatusAgendamentoValidatorTests
{
    private readonly AtualizarStatusAgendamentoValidator _sut = new();

    private static AtualizarStatusAgendamentoDto Dto(string status, long version = 0) => new()
    {
        DsStatus = status,
        NrVersion = version,
    };

    [Theory]
    [InlineData("REALIZADO")]
    [InlineData("CANCELADO")]
    [InlineData("NAO_COMPARECEU")]
    [InlineData("CONFIRMADO")]
    public void Validate_OsQuatroDestinosDaD5_NaoRetornaErro(string status)
    {
        _sut.Validate(Dto(status)).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// <c>INTENCAO</c> e <c>AGENDADO</c> existem no <c>CHECK</c> do Oracle e no enum Java, e
    /// mesmo assim são recusados aqui — a lista do validator é de <b>destinos que o .NET
    /// escreve</b>, não do domínio da coluna. Sem este caso, «aceitar os quatro» seria
    /// indistinguível de «aceitar os seis».
    /// </summary>
    [Theory]
    [InlineData("INTENCAO")]
    [InlineData("AGENDADO")]
    public void Validate_EstadosDePartidaDoJava_RetornaErro(string status)
    {
        _sut.Validate(Dto(status)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("realizado")]     // o CHECK do Oracle é sensível a caixa
    [InlineData("NAO COMPARECEU")] // espaço no lugar do underscore
    [InlineData("FATURADO")]
    public void Validate_ValorForaDoCheckDoOracle_RetornaErro(string status)
    {
        _sut.Validate(Dto(status)).IsValid.Should().BeFalse();
    }

    /// <summary>
    /// A mensagem é derivada de <c>StatusPermitidos</c>. Este teste existe para que ela não
    /// volte a dizer «REALIZADO ou CANCELADO» enquanto o código aceita quatro — a classe de
    /// defeito «documentação que garante o que o código não faz» apareceu 5 vezes num único
    /// ciclo deste projeto.
    /// </summary>
    [Fact]
    public void Validate_MensagemDeErro_ListaExatamenteOsStatusAceitos()
    {
        var resultado = _sut.Validate(Dto("FATURADO"));

        var mensagem = resultado.Errors.Single(e => e.PropertyName == nameof(AtualizarStatusAgendamentoDto.DsStatus)).ErrorMessage;

        foreach (var permitido in AtualizarStatusAgendamentoValidator.StatusPermitidos)
            mensagem.Should().Contain(permitido);

        mensagem.Should().NotContain("INTENCAO");
        mensagem.Should().NotContain("AGENDADO,", "AGENDADO não é destino aceito");
    }

    [Fact]
    public void Validate_NrVersionNegativa_RetornaErro()
    {
        _sut.Validate(Dto("REALIZADO", version: -1)).IsValid.Should().BeFalse();
    }
}
