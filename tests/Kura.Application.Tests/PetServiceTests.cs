namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Pet;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using System.Linq.Expressions;

public class PetServiceTests
{
    private readonly Mock<IPetRepository> _petRepoMock = new();
    private readonly Mock<ITutorPetRepository> _tutorPetRepoMock = new();
    private readonly Mock<IRepository<Especie>> _especieRepoMock = new();
    private readonly Mock<IRepository<Raca>> _racaRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly Mock<ITutorRepository> _tutorRepoMock = new();
    private readonly PetService _sut;

    public PetServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _sut = new PetService(
            _petRepoMock.Object, _tutorPetRepoMock.Object,
            _especieRepoMock.Object, _racaRepoMock.Object,
            _uowMock.Object, _clinicaMock.Object, _tutorRepoMock.Object);
    }

    private PetCreateDto ValidCreateDto() => new()
    {
        TutorId = 20L, IdEspecie = 1L, IdRaca = 1L,
        IdVeterinarioResp = 5L, NmPet = "Rex",
        DtNascimento = new DateTime(2022, 1, 1),
        SgSexo = 'M', SgPorte = 'M', StPrincipal = 'S'
    };

    private void SetupTutor(long id) =>
        _tutorRepoMock.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(new Tutor { Id = id, NmTutor = "João" });

    private void SetupPet(long id) =>
        _petRepoMock.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(new Pet { Id = id, NmPet = "Rex", IdClinica = 1 });

    [Fact]
    public async Task CreateAsync_TutorValido_CriaPetETutorPet()
    {
        SetupTutor(20L);
        _tutorPetRepoMock.Setup(r => r.AddAsync(It.IsAny<TutorPet>())).Returns(Task.CompletedTask);

        await _sut.CreateAsync(ValidCreateDto());

        _tutorPetRepoMock.Verify(r => r.AddAsync(It.IsAny<TutorPet>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_TutorInexistente_LancaEntidadeNaoEncontrada()
    {
        _tutorRepoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((Tutor?)null);
        var dto = new PetCreateDto
        {
            TutorId = 99L, IdEspecie = 1L, IdRaca = 1L,
            IdVeterinarioResp = 5L, NmPet = "Rex",
            DtNascimento = new DateTime(2022, 1, 1),
            SgSexo = 'M', SgPorte = 'M', StPrincipal = 'S'
        };

        var act = async () => await _sut.CreateAsync(dto);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task AdicionarTutorAsync_SegundoTutor_CriaTutorPetStPrincipalN()
    {
        SetupPet(1L);
        _tutorPetRepoMock.Setup(r => r.ExistsAsync(30L, 1L)).ReturnsAsync(false);
        TutorPet? criado = null;
        _tutorPetRepoMock.Setup(r => r.AddAsync(It.IsAny<TutorPet>()))
            .Callback<TutorPet>(tp => criado = tp)
            .Returns(Task.CompletedTask);

        await _sut.AdicionarTutorAsync(1L, new AdicionarTutorPetDto { IdTutor = 30L });

        criado!.StPrincipal.Should().Be('N');
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task AdicionarTutorAsync_TutorJaVinculado_LancaRegraDeNegocio()
    {
        SetupPet(1L);
        _tutorPetRepoMock.Setup(r => r.ExistsAsync(20L, 1L)).ReturnsAsync(true);

        var act = async () => await _sut.AdicionarTutorAsync(1L, new AdicionarTutorPetDto { IdTutor = 20L });

        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("Tutor já vinculado a este pet.");
    }

    [Fact]
    public async Task SoftDeleteAsync_PetExiste_ChamaSoftDelete()
    {
        SetupPet(1L);

        await _sut.SoftDeleteAsync(1L);

        _petRepoMock.Verify(r => r.SoftDelete(It.IsAny<Pet>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }
}
