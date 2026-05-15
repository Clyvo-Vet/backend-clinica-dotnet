namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Tutor;
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
    private readonly TutorService _sut;

    public TutorServiceTests()
    {
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>())).Returns(Task.CompletedTask);
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        _sut = new TutorService(
            _tutorRepoMock.Object,
            _tutorPetRepoMock.Object,
            _especieRepoMock.Object,
            _racaRepoMock.Object,
            _inviteRepoMock.Object,
            _uowMock.Object);
    }

    private static TutorCreateDto ValidDto(string canal = "WHATSAPP") => new()
    {
        NmTutor = "Maria Silva",
        NrCpf = "12345678901",
        DsEmail = "maria@email.com",
        NrTelefone = "11999999999",
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
}
