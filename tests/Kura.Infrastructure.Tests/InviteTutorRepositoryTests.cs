namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// TASK-33 (E-1): regressão para o HasConversion de InviteTutor.NrToken.
/// Garante que o mapeamento Guid&lt;-&gt;string continua íntegro do lado da leitura (.NET),
/// já que GetByTokenAsync compara o Guid recebido contra a coluna após a conversão.
/// Não substitui a validação ponta a ponta contra Oracle real (ver task-33-report.md) —
/// o InMemory aplica o ValueConverter na materialização, mas não reproduz a serialização
/// binária que só aparece contra um provider relacional de verdade.
/// </summary>
public class InviteTutorRepositoryTests
{
    private KuraDbContext CreateContext()
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    [Fact]
    public async Task GetByTokenAsync_TokenExistente_EncontraOInviteAposConversaoGuidString()
    {
        // Arrange
        var ctx = CreateContext();
        var token = Guid.NewGuid();

        var tutor = new Tutor { Id = 1, IdClinica = 1, NmTutor = "Maria Silva", NrCpf = "12345678901" };
        ctx.Tutores.Add(tutor);
        ctx.Set<InviteTutor>().Add(new InviteTutor
        {
            Id = 1,
            IdTutor = 1,
            NrToken = token,
            DtExpiracao = DateTime.UtcNow.AddDays(7),
            DsCanal = "WHATSAPP",
            Tutor = tutor
        });
        await ctx.SaveChangesAsync();

        var repository = new InviteTutorRepository(ctx);

        // Act
        var resultado = await repository.GetByTokenAsync(token);

        // Assert
        resultado.Should().NotBeNull();
        resultado!.NrToken.Should().Be(token);
    }

    [Fact]
    public async Task GetByTokenAsync_TokenInexistente_RetornaNull()
    {
        // Arrange
        var ctx = CreateContext();
        var repository = new InviteTutorRepository(ctx);

        // Act
        var resultado = await repository.GetByTokenAsync(Guid.NewGuid());

        // Assert
        resultado.Should().BeNull();
    }
}
