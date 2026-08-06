namespace Kura.Application.Tests;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;

/// <summary>
/// TASK-51 — prova de ponta a ponta (KuraDbContext real, InMemory, igual ao padrão
/// consolidado em CrossTenantRegressionTests.cs — TASK-21) de que o download do
/// receituário respeita o isolamento de tenant: <c>Documento</c> não tem
/// <c>IdClinica</c>/query filter próprio (é filho de <c>EventoClinico</c> via FK), então
/// o isolamento depende inteiramente de carregar o <c>EventoClinico</c> primeiro — que
/// já está em <c>KuraDbContext.ApplyTenantFilters</c>. Diferente de
/// <see cref="ReceituarioPdfServiceTests"/> (que moca <c>IEventoClinicoRepository</c> e
/// portanto não exercita o filtro de verdade), este teste usa
/// <see cref="EventoClinicoRepository"/> e <see cref="Repository{T}"/> reais sobre um
/// <see cref="KuraDbContext"/> real.
/// </summary>
public class ReceituarioDownloadCrossTenantTests : IDisposable
{
    private const long ClinicaA = 1L;
    private const long ClinicaB = 2L;

    static ReceituarioDownloadCrossTenantTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private readonly string _storageDir =
        Path.Combine(Path.GetTempPath(), "kura-tests-receituario-cross-tenant-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_storageDir))
            Directory.Delete(_storageDir, recursive: true);
    }

    private static KuraDbContext CreateContext(string dbName, long? idClinicaFiltro)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinicaFiltro).Returns(idClinicaFiltro);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private ReceituarioPdfService BuildService(KuraDbContext ctx)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:BasePath"] = _storageDir })
            .Build();

        return new ReceituarioPdfService(
            new EventoClinicoRepository(ctx),
            new Mock<IRepository<Prescricao>>().Object,
            new Mock<IPetRepository>().Object,
            new Mock<IVeterinarioRepository>().Object,
            new Mock<IRepository<Medicamento>>().Object,
            new Repository<Documento>(ctx),
            new Mock<IUnitOfWork>().Object,
            configuration);
    }

    private async Task<string> EscreverArquivoDeTesteAsync(string nomeArquivo, byte[] conteudo)
    {
        Directory.CreateDirectory(_storageDir);
        var caminho = Path.Combine(_storageDir, nomeArquivo);
        await File.WriteAllBytesAsync(caminho, conteudo);
        return caminho;
    }

    [Fact]
    public async Task ObterArquivoReceituarioAsync_ReceituarioDaMesmaClinica_RetornaBytes()
    {
        var dbName = Guid.NewGuid().ToString();
        var conteudo = new byte[] { 1, 2, 3 };
        var caminho = await EscreverArquivoDeTesteAsync("receituario-a.pdf", conteudo);

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.EventosClinicos.Add(new EventoClinico
            {
                Id = 10,
                IdClinica = ClinicaA,
                IdPet = 1,
                IdVeterinario = 1,
                IdTipoEvento = 1,
                DtEvento = DateTime.UtcNow,
            });
            seedCtx.Documentos.Add(new Documento
            {
                Id = 55,
                IdEventoClinico = 10,
                NmArquivo = "receituario-a.pdf",
                DsTipoMime = "application/pdf",
                DsCaminho = caminho,
                NrTamanhoBytes = conteudo.Length,
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildService(ctxClinicaA);

        var resultado = await sut.ObterArquivoReceituarioAsync(10L, 55L);

        resultado.Conteudo.Should().Equal(conteudo);
    }

    [Fact]
    public async Task ObterArquivoReceituarioAsync_ReceituarioDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        // O evento e o documento pertencem à Clínica B. Um veterinário autenticado na
        // Clínica A não pode baixar os bytes — mesmo sabendo (ou adivinhando) os ids
        // corretos de evento/documento.
        var dbName = Guid.NewGuid().ToString();
        var conteudo = new byte[] { 9, 9, 9 };
        var caminho = await EscreverArquivoDeTesteAsync("receituario-b.pdf", conteudo);

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.EventosClinicos.Add(new EventoClinico
            {
                Id = 20,
                IdClinica = ClinicaB,
                IdPet = 2,
                IdVeterinario = 2,
                IdTipoEvento = 1,
                DtEvento = DateTime.UtcNow,
            });
            seedCtx.Documentos.Add(new Documento
            {
                Id = 66,
                IdEventoClinico = 20,
                NmArquivo = "receituario-b.pdf",
                DsTipoMime = "application/pdf",
                DsCaminho = caminho,
                NrTamanhoBytes = conteudo.Length,
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildService(ctxClinicaA);

        var act = async () => await sut.ObterArquivoReceituarioAsync(20L, 66L);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>(
            "um veterinário da Clínica A nunca deve conseguir baixar um receituário da Clínica B, " +
            "nem mesmo acertando os ids de evento/documento por adivinhação");
    }
}
