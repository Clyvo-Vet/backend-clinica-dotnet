namespace Kura.Domain.Tests;

using FluentAssertions;
using Kura.Domain.Entities;

/// <summary>
/// Entidade concreta mínima só para exercitar os invariantes de <see cref="EntidadeBase"/>
/// sem depender de nenhuma entidade de negócio real.
/// </summary>
file sealed class EntidadeDeTeste : EntidadeBase;

/// <summary>
/// Cobre os invariantes de <see cref="EntidadeBase"/>: toda entidade nasce ativa
/// (soft delete via ST_ATIVA, nunca DELETE físico — ver CLAUDE.md) e com data de
/// criação preenchida automaticamente.
/// </summary>
public class EntidadeBaseTests
{
    [Fact]
    public void NovaEntidade_NasceAtiva_RefletindoOPadraoDeSoftDelete()
    {
        // Act
        var entidade = new EntidadeDeTeste();

        // Assert
        entidade.StAtiva.Should().BeTrue(
            "toda entidade deve nascer ativa; a inativação ocorre via soft delete (ST_ATIVA = 'N'), nunca DELETE físico");
    }

    [Fact]
    public void NovaEntidade_AoSerCriada_PreencheDtCriacaoAutomaticamenteProximoDeAgora()
    {
        // Arrange
        var antes = DateTime.UtcNow;

        // Act
        var entidade = new EntidadeDeTeste();

        // Assert
        var depois = DateTime.UtcNow;
        entidade.DtCriacao.Should().BeOnOrAfter(antes).And.BeOnOrBefore(depois);
    }

    [Fact]
    public void NovaEntidade_AoSerCriada_NaoTemDtAtualizacaoAteSerModificada()
    {
        // Act
        var entidade = new EntidadeDeTeste();

        // Assert
        entidade.DtAtualizacao.Should().BeNull();
    }
}
