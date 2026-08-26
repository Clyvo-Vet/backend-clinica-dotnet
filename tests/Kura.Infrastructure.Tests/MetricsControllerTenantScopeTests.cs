namespace Kura.Infrastructure.Tests;

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Kura.Api.Controllers;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// TASK-45: <see cref="MetricsController"/> tinha um <c>IgnoreQueryFilters()</c> num
/// endpoint <c>[AllowAnonymous]</c> que vazava volume cross-tenant "por acidente".
/// Este teste prova o novo contrato: o GET raiz é global DE PROPÓSITO (rotulado
/// <c>escopo = "ambiente"</c>), e o GET /clinica exige contexto de clínica e nunca cai
/// de volta para a contagem do ambiente inteiro quando esse contexto falta — mesmo que
/// alguém remova o <c>[Authorize]</c> por engano no futuro, a query em si é explícita
/// por <c>IdClinica</c>, não delega ao query filter ambiente do EF (que desliga inteiro
/// quando <c>IdClinicaFiltro == null</c>, ver TenantFilterCoverageTests).
/// </summary>
public class MetricsControllerTenantScopeTests
{
    private static KuraDbContext CreateContext(long? idClinicaFiltro, string dbName)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns(idClinicaFiltro);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private static async Task SeedDuasClinicasAsync(string dbName)
    {
        using var seedCtx = CreateContext(idClinicaFiltro: null, dbName);

        seedCtx.Clinicas.AddRange(
            new Clinica
            {
                Id = 1,
                NmClinica = "Clinica A",
                NrCnpj = "00000000000101",
                DsEndereco = "Rua A, 1",
                NmCidade = "Sao Paulo",
                SgUf = "SP",
                NrCep = "00000001",
                DsEmail = "a@teste.com",
                DsEmailAcesso = "a@teste.com",
                DsSenhaHash = "hash",
                StAtiva = true
            },
            new Clinica
            {
                Id = 2,
                NmClinica = "Clinica B",
                NrCnpj = "00000000000102",
                DsEndereco = "Rua B, 2",
                NmCidade = "Rio de Janeiro",
                SgUf = "RJ",
                NrCep = "00000002",
                DsEmail = "b@teste.com",
                DsEmailAcesso = "b@teste.com",
                DsSenhaHash = "hash",
                StAtiva = true
            });

        // 2 pets na clinica 1, 1 pet na clinica 2 — total do ambiente = 3.
        seedCtx.Pets.AddRange(
            new Pet { Id = 1, IdClinica = 1, NmPet = "Rex", StAtiva = true, DtNascimento = DateTime.UtcNow },
            new Pet { Id = 2, IdClinica = 1, NmPet = "Luna", StAtiva = true, DtNascimento = DateTime.UtcNow },
            new Pet { Id = 3, IdClinica = 2, NmPet = "Bidu", StAtiva = true, DtNascimento = DateTime.UtcNow });

        await seedCtx.SaveChangesAsync();
    }

    private static T ReadProperty<T>(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"a resposta deveria expor a propriedade '{propertyName}'");
        return (T)property!.GetValue(value)!;
    }

    [Fact]
    public async Task GetMetrics_SemContextoDeClinica_RetornaAgregadoGlobalRotulado()
    {
        // Arrange
        // Endpoint raiz continua AllowAnonymous — mas agora o agregado global é
        // intencional e explicitamente rotulado, não um vazamento acidental.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        using var ctx = CreateContext(idClinicaFiltro: null, dbName);
        var controller = new MetricsController(ctx, Mock.Of<IClinicaContext>());

        // Act
        var result = await controller.GetMetrics();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!;

        ReadProperty<string>(body, "escopo").Should().Be("ambiente");
        ReadProperty<int>(body, "ambienteTotalPets").Should().Be(3,
            "o GET raiz é o agregado global de propósito — soma as duas clínicas");
    }

    [Fact]
    public async Task GetMetricsClinica_ComContextoDeClinica_RetornaSoContagemDaquelaClinica()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        using var ctx = CreateContext(idClinicaFiltro: 1, dbName);
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinica).Returns(1);
        var controller = new MetricsController(ctx, clinicaContext.Object);

        // Act
        var result = await controller.GetMetricsClinica();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!;

        ReadProperty<long>(body, "idClinica").Should().Be(1);
        ReadProperty<int>(body, "totalPets").Should().Be(2,
            "só a clínica 1 tem 2 pets — o endpoint autenticado nunca deve devolver o " +
            "total do ambiente (3)");
    }

    [Fact]
    public async Task GetMetricsClinica_SemContextoDeClinica_NaoDevolveContagemGlobalPorAcidente()
    {
        // Arrange
        // Armadilha da TASK-45: o filtro de tenant do EF desliga inteiro quando
        // IdClinicaFiltro é null. Se o endpoint escopado dependesse silenciosamente
        // desse filtro (em vez de exigir e usar IClinicaContext.IdClinica de forma
        // explícita), uma chamada sem contexto de clínica devolveria o total do
        // ambiente em vez de falhar. Aqui simulamos exatamente essa ausência de
        // contexto (equivalente a ClinicaContext.IdClinica sem JWT) e provamos que o
        // controller propaga a falha em vez de silenciosamente agregar tudo.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasAsync(dbName);

        using var ctx = CreateContext(idClinicaFiltro: null, dbName);
        var clinicaContextSemJwt = new Mock<IClinicaContext>();
        clinicaContextSemJwt
            .Setup(x => x.IdClinica)
            .Throws(new UnauthorizedAccessException(
                "Claim 'clinicaId' ausente ou inválida no token JWT."));
        var controller = new MetricsController(ctx, clinicaContextSemJwt.Object);

        // Act
        Func<Task> act = async () => await controller.GetMetricsClinica();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "sem contexto de clínica o endpoint deve falhar, nunca cair de volta para a " +
            "contagem global do ambiente");
    }
}
