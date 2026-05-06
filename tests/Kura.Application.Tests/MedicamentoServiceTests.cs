namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;

public class MedicamentoServiceTests
{
    private readonly Mock<IRepository<Medicamento>> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly MedicamentoService _sut;

    public MedicamentoServiceTests()
    {
        _sut = new MedicamentoService(_repoMock.Object, _uowMock.Object);
    }

    private static List<Medicamento> BuildList(int count) =>
        Enumerable.Range(1, count).Select(i => new Medicamento
        {
            Id = i,
            NmMedicamento = $"Remedio{i}",
            DsPrincipioAtivo = $"Principio{i}",
            DsApresentacao = "Comprimido"
        }).ToList();

    [Fact]
    public async Task ListarAsync_SemFiltro_RetornaPrimeiraPagina()
    {
        _repoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(BuildList(50));

        var result = await _sut.ListarAsync(null, 1, 20);

        result.Total.Should().Be(50);
        result.Items.Should().HaveCount(20);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task ListarAsync_ComBusca_FiltraCorretamente()
    {
        var items = new List<Medicamento>
        {
            new() { Id = 1, NmMedicamento = "Amoxicilina", DsPrincipioAtivo = "Amoxicilina", DsApresentacao = "Comp" },
            new() { Id = 2, NmMedicamento = "Dipirona", DsPrincipioAtivo = "Metamizol", DsApresentacao = "Comp" },
        };

        _repoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Medicamento, bool>>>()))
            .ReturnsAsync(items.Where(m =>
                m.NmMedicamento.ToLower().Contains("amoxi") ||
                m.DsPrincipioAtivo.ToLower().Contains("amoxi")).ToList());

        var result = await _sut.ListarAsync("amoxi", 1, 20);

        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items.First().NmMedicamento.Should().Be("Amoxicilina");
    }

    [Fact]
    public async Task ListarAsync_PageSizeMaiorQue100_CapadoEm100()
    {
        _repoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(BuildList(200));

        var result = await _sut.ListarAsync(null, 1, 150);

        result.PageSize.Should().Be(100);
        result.Items.Should().HaveCount(100);
    }

    [Fact]
    public async Task ListarAsync_PageMenorQue1_NormalizadoPara1()
    {
        _repoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(BuildList(10));

        var result = await _sut.ListarAsync(null, -5, 20);

        result.Page.Should().Be(1);
        result.Items.Should().HaveCount(10);
    }
}
