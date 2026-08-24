namespace Kura.Infrastructure.Tests;

using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;

/// <summary>
/// S3D-07 — <b>Class Fixture</b> da suíte <b>unitária</b> (a rubrica pede o padrão dos dois
/// lados; a collection fixture do lado de integração é <c>ColecaoDeIntegracao</c>).
///
/// <para>
/// Monta <b>uma vez por classe de teste</b> o <c>DbContextOptions&lt;KuraDbContext&gt;</c>
/// InMemory e o <see cref="KuraDbContext"/> correspondente, e expõe o
/// <see cref="IModel"/> já construído. O padrão que existia antes era um
/// <c>CreateContext()</c> estático chamado por teste, cada chamada criando um banco
/// InMemory novo (<c>Guid.NewGuid()</c>) só para ler metadata.
/// </para>
///
/// <para>
/// 🔴 <b>Onde compartilhar é HONESTO e onde seria errado — a distinção é o ponto desta
/// fixture, não o ganho de tempo.</b> Compartilhar estado entre testes só é legítimo quando
/// o estado é <b>imutável</b>. É o caso aqui: o consumidor
/// (<see cref="InteracaoCanalColumnTypesTests"/>) lê exclusivamente
/// <c>ctx.Model</c> — metadata do EF, congelada no primeiro uso do contexto — e <b>nunca</b>
/// escreve linha nenhuma. Nenhum teste pode sujar o que o outro lê.
/// </para>
///
/// <para>
/// ⚠️ <b>Por isso esta fixture NÃO foi aplicada às outras classes InMemory do projeto.</b>
/// <c>InteracaoCanalTenantIsolationTests</c>, <c>MetricsControllerTenantScopeTests</c>,
/// <c>AgendamentoRepositoryTests</c> e <c>InviteTutorRepositoryTests</c> <b>gravam</b> —
/// elas dependem de um banco limpo por teste, e é por isso que sorteiam um nome de banco
/// por cenário. Reaproveitar options ali trocaria isolamento por economia de milissegundos
/// e criaria exatamente a dependência de ordem de execução que o critério de aceite desta
/// task proíbe.
/// </para>
///
/// <para>
/// <b>Escopo de vida:</b> o xUnit constrói a fixture uma vez antes do primeiro teste da
/// classe e a descarta depois do último; testes de uma mesma classe nunca rodam em paralelo
/// entre si, então o <see cref="KuraDbContext"/> exposto (que não é thread-safe) é usado
/// sempre por uma thread de cada vez.
/// </para>
/// </summary>
public sealed class ModeloEfInMemoryFixture : IDisposable
{
    /// <summary>
    /// Options InMemory compartilhadas. O <c>IClinicaContext</c> é mockado com
    /// <c>IdClinicaFiltro == null</c> — sem contexto de clínica os query filters de tenant
    /// desligam inteiros (documentado em <c>TenantFilterCoverageTests</c>), o que é
    /// irrelevante para leitura de metadata e evita amarrar a fixture a um tenant.
    /// </summary>
    public DbContextOptions<KuraDbContext> Options { get; }

    /// <summary>
    /// Contexto <b>somente leitura de metadata</b>. Não gravar por aqui: o contrato desta
    /// fixture é imutabilidade, e é ele que torna o compartilhamento seguro.
    /// </summary>
    public KuraDbContext Contexto { get; }

    /// <summary>Atalho para o modelo do EF já construído.</summary>
    public IModel Modelo => Contexto.Model;

    public ModeloEfInMemoryFixture()
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns((long?)null);

        Options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase($"modelo-ef-{Guid.NewGuid()}")
            .Options;

        Contexto = new KuraDbContext(Options, clinicaContext.Object);
    }

    public void Dispose() => Contexto.Dispose();
}
