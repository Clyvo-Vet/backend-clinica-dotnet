namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

public class AgendamentoRepositoryTests
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
    public async Task GetRecentesAsync_RetornaApenasAgendamentosAnterioresAReferencia_OrdenadosDoMaisRecenteAoMaisAntigo_RespeitandoOLimite()
    {
        // Arrange
        var ctx = CreateContext();
        var referencia = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

        ctx.Agendamentos.AddRange(
            new Agendamento { Id = 1, IdClinica = 1, NmPaciente = "Passado-3dias", DtAgendamento = referencia.AddDays(-3) },
            new Agendamento { Id = 2, IdClinica = 1, NmPaciente = "Passado-1dia", DtAgendamento = referencia.AddDays(-1) },
            new Agendamento { Id = 3, IdClinica = 1, NmPaciente = "Passado-2dias", DtAgendamento = referencia.AddDays(-2) },
            new Agendamento { Id = 4, IdClinica = 1, NmPaciente = "Futuro", DtAgendamento = referencia.AddDays(1) });
        await ctx.SaveChangesAsync();

        var repository = new AgendamentoRepository(ctx);

        // Act
        var resultado = (await repository.GetRecentesAsync(referencia, 2)).ToList();

        // Assert
        resultado.Should().HaveCount(2);
        resultado.Select(a => a.NmPaciente).Should().ContainInOrder("Passado-1dia", "Passado-2dias");
        resultado.Should().NotContain(a => a.NmPaciente == "Futuro");
    }
}
