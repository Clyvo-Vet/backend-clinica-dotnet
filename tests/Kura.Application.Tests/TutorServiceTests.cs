namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Tutor;
using Kura.Domain.Exceptions;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;

public class TutorServiceTests
{
    private readonly Mock<ITutorRepository> _tutorRepoMock = new();
    private readonly Mock<ITutorPetRepository> _tutorPetRepoMock = new();
    private readonly Mock<IRepository<Especie>> _especieRepoMock = new();
    private readonly Mock<IRepository<Raca>> _racaRepoMock = new();
    private readonly Mock<IInviteTutorRepository> _inviteRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _clinicaContextMock = new();
    private readonly TutorService _sut;

    public TutorServiceTests()
    {
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>())).Returns(Task.CompletedTask);
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);
        _clinicaContextMock.Setup(c => c.IdClinica).Returns(1L);

        _sut = new TutorService(
            _tutorRepoMock.Object,
            _tutorPetRepoMock.Object,
            _especieRepoMock.Object,
            _racaRepoMock.Object,
            _inviteRepoMock.Object,
            _uowMock.Object,
            _clinicaContextMock.Object);
    }

    private static TutorCreateDto ValidDto(string canal = "WHATSAPP", string nrTelefone = "11999999999") => new()
    {
        NmTutor = "Maria Silva",
        NrCpf = "12345678901",
        DsEmail = "maria@email.com",
        NrTelefone = nrTelefone,
        DsCanalConvite = canal
    };

    [Fact]
    public async Task CreateAsync_TutorEInviteCriadosNaMesmaTransacao_CommitUmaVez()
    {
        var result = await _sut.CreateAsync(ValidDto(), 1L);

        _tutorRepoMock.Verify(r => r.AddAsync(It.IsAny<Tutor>()), Times.Once);
        _inviteRepoMock.Verify(r => r.AddAsync(It.IsAny<InviteTutor>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
        result.Should().NotBeNull();
        result.Invite.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_TokenGerado_EhGuidValidoENaoVazio()
    {
        InviteTutor? capturado = null;
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>()))
            .Callback<InviteTutor>(i => capturado = i)
            .Returns(Task.CompletedTask);

        await _sut.CreateAsync(ValidDto(), 1L);

        capturado.Should().NotBeNull();
        capturado!.NrToken.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_DtExpiracao_Sete_DiasApos_DtCriacao()
    {
        InviteTutor? capturado = null;
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>()))
            .Callback<InviteTutor>(i => capturado = i)
            .Returns(Task.CompletedTask);

        Tutor? tutorCapturado = null;
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>()))
            .Callback<Tutor>(t => tutorCapturado = t)
            .Returns(Task.CompletedTask);

        await _sut.CreateAsync(ValidDto(), 1L);

        capturado!.DtExpiracao.Should().BeCloseTo(tutorCapturado!.DtCriacao.AddDays(7), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAsync_SemCanal_UsaDefaultWhatsapp()
    {
        InviteTutor? capturado = null;
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>()))
            .Callback<InviteTutor>(i => capturado = i)
            .Returns(Task.CompletedTask);

        await _sut.CreateAsync(ValidDto("WHATSAPP"), 1L);

        capturado!.DsCanal.Should().Be("WHATSAPP");
    }

    [Fact]
    public async Task CreateAsync_InviteRepositoryFalha_TutorNaoPersiste()
    {
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>()))
            .ThrowsAsync(new InvalidOperationException("Falha simulada no invite"));

        var act = async () => await _sut.CreateAsync(ValidDto(), 1L);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_TutorExiste_ChamaSoftDeleteECommit()
    {
        _tutorRepoMock.Setup(r => r.GetByIdAsync(42L))
            .ReturnsAsync(new Tutor { Id = 42L, NmTutor = "Maria" });

        await _sut.SoftDeleteAsync(42L);

        _tutorRepoMock.Verify(r => r.SoftDelete(It.IsAny<Tutor>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task SoftDeleteAsync_TutorNaoEncontrado_LancaEntidadeNaoEncontrada()
    {
        _tutorRepoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((Tutor?)null);

        var act = async () => await _sut.SoftDeleteAsync(99L);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_NrTelefoneVazioOuWhitespace_ColescaParaSentinela(string nrTelefoneBruto)
    {
        // TASK-60: TUTOR.DS_TELEFONE é NOT NULL (V1:91, migration imutável) e o Oracle trata
        // VARCHAR2 vazio como NULL — TutorCreateValidator só valida NmTutor/NrCpf/DsEmail, nunca
        // teve regra para NrTelefone, então um payload sem esse campo passava reto pro INSERT e
        // estourava ORA-01400 (500). Mesmo padrão da TASK-56: coalesce no service, não NotEmpty()
        // no validator.
        Tutor? tutorAdicionado = null;
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>()))
            .Callback<Tutor>(t => tutorAdicionado = t)
            .Returns(Task.CompletedTask);

        var dto = ValidDto(nrTelefone: nrTelefoneBruto);

        await _sut.CreateAsync(dto, 1L);

        tutorAdicionado.Should().NotBeNull();
        tutorAdicionado!.NrTelefone.Should().Be("Não informado");
    }

    [Fact]
    public async Task CreateAsync_NrTelefonePreenchido_NaoSobrescreveComSentinela()
    {
        Tutor? tutorAdicionado = null;
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>()))
            .Callback<Tutor>(t => tutorAdicionado = t)
            .Returns(Task.CompletedTask);

        var dto = ValidDto(nrTelefone: "11988887777");

        await _sut.CreateAsync(dto, 1L);

        tutorAdicionado.Should().NotBeNull();
        tutorAdicionado!.NrTelefone.Should().Be("11988887777");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_NrTelefoneVazioOuWhitespace_ColescaParaSentinela(string nrTelefoneBruto)
    {
        // Mesmo gap de TutorCreateDto — TutorUpdateValidator também nunca teve regra para
        // NrTelefone (só NmTutor/NrCpf/DsEmail).
        var tutorExistente = new Tutor { Id = 42L, NmTutor = "Maria", NrTelefone = "11900000000" };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(42L)).ReturnsAsync(tutorExistente);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria Silva",
            NrCpf = "12345678901",
            DsEmail = "maria@email.com",
            NrTelefone = nrTelefoneBruto
        };

        await _sut.UpdateAsync(42L, dto);

        tutorExistente.NrTelefone.Should().Be("Não informado");
        _tutorRepoMock.Verify(r => r.Update(It.IsAny<Tutor>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NrTelefonePreenchido_NaoSobrescreveComSentinela()
    {
        var tutorExistente = new Tutor { Id = 42L, NmTutor = "Maria", NrTelefone = "11900000000" };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(42L)).ReturnsAsync(tutorExistente);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria Silva",
            NrCpf = "12345678901",
            DsEmail = "maria@email.com",
            NrTelefone = "11977776666"
        };

        await _sut.UpdateAsync(42L, dto);

        tutorExistente.NrTelefone.Should().Be("11977776666");
    }
}
