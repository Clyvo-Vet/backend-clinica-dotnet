namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Medicamento;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
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

    [Fact]
    public async Task UpdateAsync_MedicamentoExiste_AtualizaCamposECommit()
    {
        var medicamento = new Medicamento { Id = 1L, NmMedicamento = "Velho", DsPrincipioAtivo = "Velho", DsApresentacao = "Velho" };
        _repoMock.Setup(r => r.GetByIdAsync(1L)).ReturnsAsync(medicamento);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new MedicamentoUpdateDto { NmMedicamento = "Novo", DsPrincipioAtivo = "NovoPrincipio", DsApresentacao = "Liquido" };
        var result = await _sut.UpdateAsync(1L, dto);

        result.NmMedicamento.Should().Be("Novo");
        result.DsPrincipioAtivo.Should().Be("NovoPrincipio");
        result.DsApresentacao.Should().Be("Liquido");
        _repoMock.Verify(r => r.Update(It.IsAny<Medicamento>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_MedicamentoNaoEncontrado_LancaEntidadeNaoEncontrada()
    {
        _repoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((Medicamento?)null);

        var act = async () => await _sut.UpdateAsync(99L, new MedicamentoUpdateDto());

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_MedicamentoExiste_ChamaSoftDelete()
    {
        _repoMock.Setup(r => r.GetByIdAsync(1L))
            .ReturnsAsync(new Medicamento { Id = 1L, NmMedicamento = "Amoxicilina" });
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        await _sut.SoftDeleteAsync(1L);

        _repoMock.Verify(r => r.SoftDelete(It.IsAny<Medicamento>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task SoftDeleteAsync_MedicamentoNaoEncontrado_LancaEntidadeNaoEncontrada()
    {
        _repoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((Medicamento?)null);

        var act = async () => await _sut.SoftDeleteAsync(99L);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_DsApresentacaoVaziaOuWhitespace_ColescaParaSentinela(string dsApresentacaoBruta)
    {
        // TASK-60: MEDICAMENTO.DS_APRESENTACAO é NOT NULL (V9:78, migration imutável) e o Oracle
        // trata VARCHAR2 vazio como NULL — MedicamentoCreateValidator nunca teve regra para este
        // campo (só NmMedicamento/DsPrincipioAtivo), então um payload sem dsApresentacao passava
        // reto pro INSERT e estourava ORA-01400 (500). Mesmo padrão da TASK-56 (DS_OBSERVACAO):
        // coalesce no service, não NotEmpty() no validator.
        Medicamento? medicamentoAdicionado = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Medicamento>()))
            .Callback<Medicamento>(m => medicamentoAdicionado = m)
            .Returns(Task.CompletedTask);

        var dto = new MedicamentoCreateDto
        {
            NmMedicamento = "Amoxicilina",
            DsPrincipioAtivo = "Amoxicilina Triidratada",
            DsApresentacao = dsApresentacaoBruta
        };

        await _sut.CreateAsync(dto);

        medicamentoAdicionado.Should().NotBeNull();
        medicamentoAdicionado!.DsApresentacao.Should().Be("Apresentação não informada");
    }

    [Fact]
    public async Task CreateAsync_DsApresentacaoPreenchida_NaoSobrescreveComSentinela()
    {
        Medicamento? medicamentoAdicionado = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Medicamento>()))
            .Callback<Medicamento>(m => medicamentoAdicionado = m)
            .Returns(Task.CompletedTask);

        var dto = new MedicamentoCreateDto
        {
            NmMedicamento = "Amoxicilina",
            DsPrincipioAtivo = "Amoxicilina Triidratada",
            DsApresentacao = "Comprimido 500mg"
        };

        await _sut.CreateAsync(dto);

        medicamentoAdicionado.Should().NotBeNull();
        medicamentoAdicionado!.DsApresentacao.Should().Be("Comprimido 500mg");
    }
}
