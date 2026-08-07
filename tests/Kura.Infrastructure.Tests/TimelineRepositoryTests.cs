namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// TASK-63 — GET /api/v1/pets/{id}/timeline devolvia 500 (ORA-00904) contra o Oracle
/// real: <see cref="TimelineRepository.GetByPetIdAsync"/> consultava VW_TIMELINE_PET via
/// FromSqlRaw, mas essa view (Flyway V1/V6, backend-tutor-java) é derivada de
/// AGENDAMENTO e documentada desde a V1 como "lida pelo Java" — não tem DS_OBSERVACAO
/// nem NM_VETERINARIO, que são exatamente os campos que o read model .NET
/// (TimelineItem/TimelineEntry) espera. Mismatch estrutural, não só o ORDER BY que
/// estourava primeiro.
///
/// Fix: consultar EventoClinico diretamente via LINQ do EF Core (com Include para
/// Pet/Veterinario/TipoEvento), sem FromSqlRaw e sem tocar VW_TIMELINE_PET/Flyway.
/// EventoClinico já está em KuraDbContext.ApplyTenantFilters, então o isolamento de
/// tenant passa a ser automático (prova em CrossTenantRegressionTests.cs).
///
/// Nota sobre o "vermelho" deste teste: contra Oracle real o sintoma é ORA-00904 (EF
/// gera "ORDER BY ... DtEvento" com o nome da propriedade C#, que não existe na view).
/// Contra o provider InMemory usado aqui, FromSqlRaw simplesmente não é suportado — o
/// código pré-fix lança InvalidOperationException ao tentar rodar SQL bruto num provider
/// que não tem SGBD relacional por trás. É uma manifestação diferente da mesma causa
/// raiz (consultar uma view via SQL bruto, incompatível com o modelo real do read
/// model), não uma reprodução byte-a-byte do ORA-00904 — que só ocorre contra Oracle de
/// verdade (validado à parte contra o compose, ver relatório da task).
/// </summary>
public class TimelineRepositoryTests
{
    private static KuraDbContext CreateContext(long? idClinicaFiltro = null)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns(idClinicaFiltro);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    [Fact]
    public async Task GetByPetIdAsync_RetornaEventosOrdenadosPorDataDecrescente_ComDsObservacaoENmVeterinarioCorretos()
    {
        var ctx = CreateContext();

        ctx.Veterinarios.Add(new Veterinario
        {
            Id = 1,
            IdClinica = 1,
            NmVeterinario = "Dra. Ana Souza",
            NrCrmv = "CRMV-1",
            DsEmail = "ana@clinica.com",
        });
        ctx.Pets.Add(new Pet
        {
            Id = 1,
            IdClinica = 1,
            IdEspecie = 1,
            IdRaca = 1,
            NmPet = "Rex",
            DtNascimento = new DateTime(2022, 1, 1),
            SgSexo = 'M',
            SgPorte = 'G',
        });
        ctx.TiposEvento.AddRange(
            new TipoEvento { Id = 1, CdTipo = "CONSULTA", NmTipo = "Consulta" },
            new TipoEvento { Id = 2, CdTipo = "VACINA", NmTipo = "Vacina" });
        ctx.EventosClinicos.AddRange(
            new EventoClinico
            {
                Id = 1,
                IdClinica = 1,
                IdPet = 1,
                IdVeterinario = 1,
                IdTipoEvento = 1,
                DtEvento = new DateTime(2026, 8, 1, 10, 0, 0),
                DsObservacao = "Consulta de rotina",
            },
            new EventoClinico
            {
                Id = 2,
                IdClinica = 1,
                IdPet = 1,
                IdVeterinario = 1,
                IdTipoEvento = 2,
                DtEvento = new DateTime(2026, 8, 5, 9, 0, 0),
                DsObservacao = "Vacina antirrábica aplicada",
            });
        await ctx.SaveChangesAsync();

        var repository = new TimelineRepository(ctx);

        var resultado = (await repository.GetByPetIdAsync(1L)).ToList();

        resultado.Should().HaveCount(2);
        // Ordenado por DtEvento decrescente: o evento de 08/05 (vacina) vem antes do de 08/01 (consulta).
        resultado.Select(e => e.DsObservacao).Should().ContainInOrder(
            "Vacina antirrábica aplicada", "Consulta de rotina");
        resultado[0].NmTipo.Should().Be("Vacina");
        resultado[0].NmVeterinario.Should().Be("Dra. Ana Souza");
        resultado[0].NmPet.Should().Be("Rex");
        resultado[0].IdPet.Should().Be(1L);
    }

    [Fact]
    public async Task GetByPetIdAsync_PetSemEventosClinicos_RetornaListaVazia()
    {
        var ctx = CreateContext();
        var repository = new TimelineRepository(ctx);

        var resultado = await repository.GetByPetIdAsync(999L);

        resultado.Should().BeEmpty();
    }
}
