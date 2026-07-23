namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class TipoEventoServiceTests
{
    private readonly Mock<IRepository<TipoEvento>> _repositoryMock = new();
    private readonly TipoEventoService _sut;

    public TipoEventoServiceTests()
    {
        _sut = new TipoEventoService(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetIdByCdTipoAsync_CdTipoExistente_RetornaId()
    {
        var tipos = new List<TipoEvento> { new() { Id = 4, CdTipo = "PRESCRICAO", NmTipo = "PRESCRICAO" } };
        _repositoryMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TipoEvento, bool>>>()))
            .ReturnsAsync(tipos);

        var id = await _sut.GetIdByCdTipoAsync("PRESCRICAO");

        id.Should().Be(4L);
    }

    [Fact]
    public async Task GetIdByCdTipoAsync_CdTipoInexistente_LancaEntidadeNaoEncontrada()
    {
        _repositoryMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TipoEvento, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<TipoEvento>());

        var act = async () => await _sut.GetIdByCdTipoAsync("DESCONHECIDO");

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>()
            .WithMessage("*TipoEvento*DESCONHECIDO*");
    }
}
