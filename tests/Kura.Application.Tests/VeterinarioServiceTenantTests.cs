namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.DTOs.Veterinario;
using Kura.Application.Services;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// FD-05 (ciclo FIN) — a clínica gravada por <c>VeterinarioService.CreateAsync</c> sai do JWT.
///
/// <para>
/// 🔴 <b>Por que a forma da prova MUDOU no meio da task, e a mudança é a própria correção.</b>
/// A mordida original era «token da clínica A, corpo pedindo a clínica B, tem de gravar em A»,
/// e ela foi medida sobre HTTP real contra o código antigo, falhando com
/// <c>Expected criado!.IdClinica to be 1L …, but found 2L</c>
/// (<c>VeterinariosTenantHttpTests</c>). Depois do fix esse cenário <b>deixou de ser
/// expressável em C#</b>: <see cref="VeterinarioCreateDto"/> não tem mais o campo, então não há
/// como pedir B. O que estes testes provam no lugar é a propriedade que sobrou e que importa:
/// a clínica gravada <b>acompanha o <c>IClinicaContext</c></b>.
/// </para>
///
/// <para>
/// <b>Dois contextos diferentes, de propósito.</b> Um teste só, com uma clínica só, passaria
/// igual se alguém escrevesse <c>IdClinica = 1</c> literal no service. Com A e B exercitados
/// lado a lado, a única implementação que satisfaz os dois é a que lê o contexto.
/// </para>
///
/// <para>
/// 🔴 <b>O <c>IClinicaContext</c> do <c>DbContext</c> tem <c>IdClinicaFiltro = null</c>, isto é,
/// os query filters de tenant estão DESLIGADOS</b> — mesmo arranjo hostil de
/// <c>UsuarioClinicaServiceTests</c>. Com o filtro ligado, a leitura de verificação já traria
/// só a linha da clínica certa e a asserção não teria como falhar. Aqui a leitura é
/// <c>IgnoreQueryFilters</c> explícita sobre o contexto sem tenant: o que estiver gravado
/// aparece como está.
/// </para>
/// </summary>
public class VeterinarioServiceTenantTests
{
    private const long ClinicaA = 1L;
    private const long ClinicaB = 2L;

    /// <summary>Contexto com os query filters DESLIGADOS — ver a documentação da classe.</summary>
    private static KuraDbContext CriarContexto(string dbName)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private static VeterinarioService CriarService(KuraDbContext ctx, long idClinicaDoJwt)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinica).Returns(idClinicaDoJwt);

        return new VeterinarioService(
            new VeterinarioRepository(ctx),
            new UnitOfWork(ctx),
            clinicaContext.Object);
    }

    private static VeterinarioCreateDto Dto(string nome) => new()
    {
        NmVeterinario = nome,
        NrCrmv = "SP-11111",
        DsEmail = $"{nome.Replace(" ", string.Empty)}@kura.test",
        NrTelefone = "11911112222",
    };

    [Theory]
    [InlineData(ClinicaA)]
    [InlineData(ClinicaB)]
    public async Task Criar_grava_a_clinica_do_contexto_e_nao_uma_constante(long idClinicaDoJwt)
    {
        // Arrange
        await using var ctx = CriarContexto($"vet-tenant-{idClinicaDoJwt}-{Guid.NewGuid():N}");
        var service = CriarService(ctx, idClinicaDoJwt);

        // Act
        var criado = await service.CreateAsync(Dto("Dr. Do Contexto"));

        // Assert — a resposta.
        criado.IdClinica.Should().Be(idClinicaDoJwt);

        // Assert — a LINHA GRAVADA, lida de novo do banco ignorando qualquer filtro. Sem esta
        // segunda asserção o teste passaria mesmo que o service devolvesse um DTO montado a
        // partir do contexto e persistisse outra coisa.
        var persistido = await ctx.Veterinarios
            .IgnoreQueryFilters()
            .SingleAsync(v => v.Id == criado.Id);

        persistido.IdClinica.Should().Be(
            idClinicaDoJwt,
            "a clínica persistida sai de IClinicaContext.IdClinica, e de mais lugar nenhum");
    }

    /// <summary>
    /// <b>Controle do instrumento.</b> As duas clínicas gravadas pelo <see cref="Theory"/> acima
    /// são exercitadas em bancos separados, então nenhuma delas prova sozinha que o service
    /// <b>distingue</b> os contextos. Aqui os dois <c>CreateAsync</c> rodam sobre o MESMO banco,
    /// com contextos diferentes, e as duas linhas coexistem com clínicas diferentes — resultado
    /// impossível para qualquer implementação que ignore o contexto.
    /// </summary>
    [Fact]
    public async Task Dois_contextos_no_mesmo_banco_produzem_clinicas_diferentes()
    {
        // Arrange
        await using var ctx = CriarContexto($"vet-tenant-duplo-{Guid.NewGuid():N}");

        // Act
        var deA = await CriarService(ctx, ClinicaA).CreateAsync(Dto("Dr. Da A"));
        var deB = await CriarService(ctx, ClinicaB).CreateAsync(Dto("Dr. Da B"));

        // Assert
        deA.IdClinica.Should().Be(ClinicaA);
        deB.IdClinica.Should().Be(ClinicaB);
        deA.Id.Should().NotBe(deB.Id);
    }
}
