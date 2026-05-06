namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class LunaServiceTests
{
    private readonly Mock<ITriagemLunaRepository> _repoMock = new();
    private readonly LunaService _sut;

    public LunaServiceTests()
    {
        _sut = new LunaService(_repoMock.Object);
    }

    private static DateTime Inicio => new(2026, 5, 1);
    private static DateTime Fim => new(2026, 5, 31);

    [Fact]
    public async Task GerarRelatorioAsync_IntervaloValido_RetornaAgregacaoCorreta()
    {
        var triagens = new List<TriagemLuna>
        {
            new() { Id = 1, IdClinica = 1, DsNivelUrgencia = "URGENTE", StEncaminhadoVet = true, DtTriagem = Inicio.AddDays(1), StAtiva = 'S', DsDescricao = "desc" },
            new() { Id = 2, IdClinica = 1, DsNivelUrgencia = "URGENTE", StEncaminhadoVet = true, DtTriagem = Inicio.AddDays(2), StAtiva = 'S', DsDescricao = "desc" },
            new() { Id = 3, IdClinica = 1, DsNivelUrgencia = "LEVE", StEncaminhadoVet = false, DtTriagem = Inicio.AddDays(3), StAtiva = 'S', DsDescricao = "desc" },
        };

        _repoMock.Setup(r => r.GetByIntervaloAsync(Inicio, Fim))
            .ReturnsAsync(triagens);

        var result = await _sut.GerarRelatorioAsync(Inicio, Fim);

        result.Should().NotBeNull();
        result.TotalTriagens.Should().Be(3);
        result.EncaminhadasParaVet.Should().Be(2);
        result.PorUrgencia.Should().ContainKey("URGENTE").WhoseValue.Should().Be(2);
        result.PorUrgencia.Should().ContainKey("LEVE").WhoseValue.Should().Be(1);
        result.DataInicio.Should().Be(Inicio);
        result.DataFim.Should().Be(Fim);
    }

    [Fact]
    public async Task GerarRelatorioAsync_SemTriagensNoPeriodo_RetornaZeros()
    {
        _repoMock.Setup(r => r.GetByIntervaloAsync(Inicio, Fim))
            .ReturnsAsync(new List<TriagemLuna>());

        var result = await _sut.GerarRelatorioAsync(Inicio, Fim);

        result.TotalTriagens.Should().Be(0);
        result.EncaminhadasParaVet.Should().Be(0);
        result.PorUrgencia.Should().BeEmpty();
    }

    [Fact]
    public async Task GerarRelatorioAsync_DataFimAnteriorDataInicio_LancaRegraDeNegocio()
    {
        var act = async () => await _sut.GerarRelatorioAsync(Fim, Inicio);

        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("DataFim não pode ser anterior à DataInicio.");
    }

    [Fact]
    public async Task GerarRelatorioAsync_IntervaloMaiorQue90Dias_LancaRegraDeNegocio()
    {
        var inicio = new DateTime(2026, 1, 1);
        var fimFora = inicio.AddDays(91);

        var act = async () => await _sut.GerarRelatorioAsync(inicio, fimFora);

        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("Intervalo máximo de 90 dias.");
    }
}
