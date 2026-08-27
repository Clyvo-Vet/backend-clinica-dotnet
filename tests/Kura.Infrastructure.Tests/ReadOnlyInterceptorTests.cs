namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Moq;

public class ReadOnlyInterceptorTests
{
    private KuraDbContext CreateContext()
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new ReadOnlyTablesInterceptor())
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    [Fact]
    public async Task Add_ContaTutor_LancaInvalidOperationException()
    {
        // Arrange
        var ctx = CreateContext();
        ctx.ContasTutor.Add(new ContaTutor
        {
            Id = 1,
            IdTutor = 1,
            DsEmail = "test@test.com",
            StEmailVerificado = 'S',
            DtCadastro = DateTime.UtcNow
        });

        // Act
        var act = async () => await ctx.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ContaTutor*");
    }

    [Fact]
    public async Task Modify_Consentimento_LancaInvalidOperationException()
    {
        // Arrange
        var ctx = CreateContext();
        var consentimento = new Consentimento
        {
            Id = 1,
            IdTutor = 1,
            DsTipo = "LGPD",
            StAceito = 'S',
            NrVersaoTermo = "1.0",
            DtConsentimento = DateTime.UtcNow
        };
        ctx.Consentimentos.Attach(consentimento);
        ctx.Entry(consentimento).State = EntityState.Modified;

        // Act
        var act = async () => await ctx.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Consentimento*");
    }

    [Fact]
    public async Task Query_AsNoTracking_ContaTutor_FuncionaNormalmente()
    {
        // Arrange
        var ctx = CreateContext();

        // Act
        var act = async () => await ctx.ContasTutor.AsNoTracking().ToListAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveChanges_SemMudancas_FuncionaNormalmente()
    {
        // Arrange
        var ctx = CreateContext();

        // Act
        var act = async () => await ctx.SaveChangesAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
