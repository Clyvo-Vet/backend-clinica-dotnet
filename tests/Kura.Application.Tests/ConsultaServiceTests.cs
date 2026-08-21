namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.EventoClinico;
using Kura.Application.Services;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class ConsultaServiceTests
{
    private const long IdTipoEventoConsultaSeed = 4L;

    private readonly Mock<IRepository<Consulta>> _consultaRepoMock = new();
    private readonly Mock<IPetRepository> _petRepoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly Mock<ITipoEventoService> _tipoEventoServiceMock = new();
    private readonly ConsultaService _sut;

    public ConsultaServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _tipoEventoServiceMock.Setup(t => t.GetIdByCdTipoAsync("CONSULTA"))
            .ReturnsAsync(IdTipoEventoConsultaSeed);
        _sut = new ConsultaService(
            _consultaRepoMock.Object,
            _petRepoMock.Object,
            _vetRepoMock.Object,
            _uowMock.Object,
            _clinicaMock.Object,
            _tipoEventoServiceMock.Object);
    }

    private static ConsultaCreateDto ValidDto(string dsObservacao = "") => new()
    {
        IdPet = 5,
        IdVeterinario = 10,
        DtConsulta = new DateTime(2026, 5, 6, 9, 0, 0),
        DsMotivo = "Check-up anual",
        DsAnamnese = "Paciente apresenta letargia",
        DsDiagnostico = "Saudável",
        DsObservacao = dsObservacao
    };

    [Fact]
    public async Task CriarConsultaAsync_DadosValidos_CriaDoisRegistrosAtomicamente()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        _vetRepoMock.Setup(r => r.GetByIdAsync(10L))
            .ReturnsAsync(new Veterinario { Id = 10, NmVeterinario = "Dr. Ana", IdClinica = 1, NrCrmv = "1234" });

        var result = await _sut.CriarConsultaAsync(ValidDto());

        result.Should().NotBeNull();
        result.IdPet.Should().Be(5);
        result.IdVeterinario.Should().Be(10);
        result.DsMotivo.Should().Be("Check-up anual");
        result.DsAnamnese.Should().Be("Paciente apresenta letargia");
        result.DsDiagnostico.Should().Be("Saudável");

        _consultaRepoMock.Verify(r => r.AddAsync(It.IsAny<Consulta>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CriarConsultaAsync_ResolveIdTipoEventoPorCdTipo_PersisteFkCorreta()
    {
        // Regressão: IdTipoEvento não pode mais vir de um const hardcoded (id 4 inexistente na seed
        // antiga), e sim ser resolvido via CD_TIPO='CONSULTA' no TIPO_EVENTO — evitando o 500 por
        // violação de FK que ocorria antes.
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        _vetRepoMock.Setup(r => r.GetByIdAsync(10L))
            .ReturnsAsync(new Veterinario { Id = 10, NmVeterinario = "Dr. Ana", IdClinica = 1, NrCrmv = "1234" });

        Consulta? consultaAdicionada = null;
        _consultaRepoMock.Setup(r => r.AddAsync(It.IsAny<Consulta>()))
            .Callback<Consulta>(c => consultaAdicionada = c)
            .Returns(Task.CompletedTask);

        await _sut.CriarConsultaAsync(ValidDto());

        _tipoEventoServiceMock.Verify(t => t.GetIdByCdTipoAsync("CONSULTA"), Times.Once);
        consultaAdicionada.Should().NotBeNull();
        consultaAdicionada!.EventoClinico.IdTipoEvento.Should().Be(IdTipoEventoConsultaSeed);
    }

    [Fact]
    public async Task CriarConsultaAsync_CdTipoNaoEncontrado_PropagaEntidadeNaoEncontrada()
    {
        // Se o TIPO_EVENTO 'CONSULTA' não estiver seedado, o erro deve ser um 404 de domínio
        // (EntidadeNaoEncontradaException), nunca mais um 500 de violação de FK no banco.
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        _vetRepoMock.Setup(r => r.GetByIdAsync(10L))
            .ReturnsAsync(new Veterinario { Id = 10, NmVeterinario = "Dr. Ana", IdClinica = 1, NrCrmv = "1234" });

        _tipoEventoServiceMock.Setup(t => t.GetIdByCdTipoAsync("CONSULTA"))
            .ThrowsAsync(new EntidadeNaoEncontradaException("TipoEvento", "CONSULTA"));

        var act = async () => await _sut.CriarConsultaAsync(ValidDto());

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>()
            .WithMessage("*TipoEvento*CONSULTA*");

        _consultaRepoMock.Verify(r => r.AddAsync(It.IsAny<Consulta>()), Times.Never);
    }

    [Fact]
    public async Task CriarConsultaAsync_PetInexistente_LancaEntidadeNaoEncontrada()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync((Pet?)null);

        var act = async () => await _sut.CriarConsultaAsync(ValidDto());

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>()
            .WithMessage("*Pet*5*");
    }

    [Fact]
    public async Task CriarConsultaAsync_VeterinarioInexistente_LancaEntidadeNaoEncontrada()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        _vetRepoMock.Setup(r => r.GetByIdAsync(10L))
            .ReturnsAsync((Veterinario?)null);

        var act = async () => await _sut.CriarConsultaAsync(ValidDto());

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>()
            .WithMessage("*Veterinario*10*");
    }

    [Fact]
    public async Task CriarConsultaAsync_PetEVeterinarioValidos_CommitChamadoUmaVez()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        _vetRepoMock.Setup(r => r.GetByIdAsync(10L))
            .ReturnsAsync(new Veterinario { Id = 10, NmVeterinario = "Dr. Ana", IdClinica = 1, NrCrmv = "1234" });

        await _sut.CriarConsultaAsync(ValidDto());

        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CriarConsultaAsync_DsObservacaoVaziaOuWhitespace_ColescaParaSentinela(string dsObservacaoBruta)
    {
        // TASK-56: EVENTO_CLINICO.DS_OBSERVACAO é NOT NULL (V9:58, migration imutável) e o Oracle
        // trata VARCHAR2 vazio como NULL — o form SOAP do app (S/O/A/P) permite "Plano" vazio
        // legitimamente, então quem satisfaz a restrição de armazenamento é o service, não um
        // NotEmpty() no validator (reverte a TASK-47 nesse ponto, ver ConsultaCreateValidator).
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        _vetRepoMock.Setup(r => r.GetByIdAsync(10L))
            .ReturnsAsync(new Veterinario { Id = 10, NmVeterinario = "Dr. Ana", IdClinica = 1, NrCrmv = "1234" });

        Consulta? consultaAdicionada = null;
        _consultaRepoMock.Setup(r => r.AddAsync(It.IsAny<Consulta>()))
            .Callback<Consulta>(c => consultaAdicionada = c)
            .Returns(Task.CompletedTask);

        var dto = ValidDto(dsObservacao: dsObservacaoBruta);

        await _sut.CriarConsultaAsync(dto);

        consultaAdicionada.Should().NotBeNull();
        consultaAdicionada!.EventoClinico.DsObservacao.Should().Be("Sem observações");
    }

    [Fact]
    public async Task CriarConsultaAsync_DsObservacaoPreenchida_NaoSobrescreveComSentinela()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(5L))
            .ReturnsAsync(new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' });

        _vetRepoMock.Setup(r => r.GetByIdAsync(10L))
            .ReturnsAsync(new Veterinario { Id = 10, NmVeterinario = "Dr. Ana", IdClinica = 1, NrCrmv = "1234" });

        Consulta? consultaAdicionada = null;
        _consultaRepoMock.Setup(r => r.AddAsync(It.IsAny<Consulta>()))
            .Callback<Consulta>(c => consultaAdicionada = c)
            .Returns(Task.CompletedTask);

        var dto = ValidDto(dsObservacao: "Plano: retorno em 15 dias");

        await _sut.CriarConsultaAsync(dto);

        consultaAdicionada.Should().NotBeNull();
        consultaAdicionada!.EventoClinico.DsObservacao.Should().Be("Plano: retorno em 15 dias");
    }
}
