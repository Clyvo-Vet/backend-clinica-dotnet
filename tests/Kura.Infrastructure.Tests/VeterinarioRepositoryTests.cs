namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// TASK-36 (E-4): regressão para NrTelefone nulo em Veterinario.
/// Contra Oracle real, gravar sem telefone (NULL, já que "" também vira NULL
/// no Oracle) e depois ler de volta lançava InvalidCastException
/// ("ORA-50032: Column contains NULL data"), porque VeterinarioConfiguration
/// mapeava a coluna como .IsRequired() mesmo o schema físico (Flyway,
/// V1__initial_schema.sql) sendo NULLABLE. O InMemory provider não reproduz o
/// ORA-50032 em si (ver nota em InviteTutorRepositoryTests sobre a mesma
/// limitação) — mas prova que o domínio/mapeamento aceitam null de ponta a
/// ponta sem exigir o fallback para string.Empty que mascarava o problema.
/// A prova definitiva do fix contra Oracle real está em task-36-report.md.
/// </summary>
public class VeterinarioRepositoryTests
{
    private static KuraDbContext CreateContext(string dbName)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    [Fact]
    public async Task SalvarELer_VeterinarioSemTelefone_NaoLancaERetornaNull()
    {
        var dbName = Guid.NewGuid().ToString();
        var ctx = CreateContext(dbName);

        var clinica = new Clinica
        {
            Id = 1,
            NmClinica = "Clinica Teste",
            NrCnpj = "00000000000100",
            DsEndereco = "Rua Teste, 1",
            NmCidade = "Sao Paulo",
            SgUf = "SP",
            NrCep = "00000000",
            DsEmail = "clinica@teste.com",
            DsEmailAcesso = "clinica@teste.com",
            DsSenhaHash = "hash",
            StAtiva = true
        };
        ctx.Clinicas.Add(clinica);

        var repository = new VeterinarioRepository(ctx);
        var veterinario = new Veterinario
        {
            IdClinica = 1,
            NmVeterinario = "Dr. Teste",
            NrCrmv = "SP-000000",
            DsEmail = "vet@teste.com",
            NrTelefone = null,
            Clinica = clinica
        };

        await repository.AddAsync(veterinario);
        await ctx.SaveChangesAsync();

        // Novo contexto (mesmo banco InMemory nomeado) para forçar releitura
        // do "banco", não do change tracker do contexto que gravou.
        var ctx2 = CreateContext(dbName);

        Func<Task<Veterinario?>> act = () => ctx2.Veterinarios
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == veterinario.Id);

        var lido = await act.Should().NotThrowAsync();
        lido.Subject.Should().NotBeNull();
        lido.Subject!.NrTelefone.Should().BeNull("o fallback para string.Empty foi removido — NULL é o valor honesto");
    }
}
