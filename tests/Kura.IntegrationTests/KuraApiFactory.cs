namespace Kura.IntegrationTests;

using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// S3D-06: fábrica de host para os testes de integração HTTP. Sobe o
/// <c>Program.cs</c> REAL do <c>Kura.Api</c> (toda a fiação de produção: Serilog,
/// autenticação JWT, FluentValidation, health checks, observabilidade, pipeline de
/// middlewares) e substitui apenas a persistência por EF InMemory.
///
/// <para>
/// 🔴 <b>Duas exigências que NÃO são estilo</b>, ambas medidas na revisão G2 da S3D-05
/// (<c>task-S3D-05-review.md</c> §2 e §3) e reconfirmadas por medição nesta task:
/// </para>
///
/// <para>
/// <b>(a) <c>UseEnvironment("Testing")</c> é obrigatório.</b> O
/// <see cref="WebApplicationFactory{TEntryPoint}"/> usa o ambiente <c>Development</c>
/// por PADRÃO (não <c>Testing</c>, não <c>Production</c>). Sem esta linha o guard que a
/// S3D-05 pôs no <c>Program.cs</c> não dispara e o bloco de validação de migrations roda
/// no startup, carregando <c>appsettings.Development.json</c> — que está versionado
/// apontando para <c>oracle.fiap.com.br</c> com uma conta institucional BLOQUEADA. Vale
/// também para o CI, que não define <c>ASPNETCORE_ENVIRONMENT</c>.
/// </para>
///
/// <para>
/// ⚠️ <b>Onde está a barreira de verdade — medido no G2 desta task, em 4 variantes,
/// incluindo o pior caso.</b> NESTA fábrica o bloco de startup <b>não chega a abrir
/// conexão Oracle</b>: ele morre antes com
/// <c>InvalidOperationException: Relational-specific methods…</c>, porque o
/// <c>DbContext</c> já foi substituído por InMemory. Ou seja, <b>quem impede discar para
/// a FIAP é a substituição do <c>DbContext</c>, não esta linha.</b> Não confie no
/// contrário: uma segunda fábrica que mantenha <c>UseEnvironment</c> e dispense a
/// substituição InMemory (p.ex. para testar contra um Oracle local) <b>não</b> está
/// protegida por esta linha. A linha continua obrigatória por dois motivos medidos: sem
/// ela a suíte fica <b>19/19 vermelha</b>, e numa fábrica sem substituição de
/// <c>DbContext</c> o risco de conexão volta inteiro. Ver
/// <see cref="AmbienteEFiacaoDoHostTests"/>, onde o ambiente é asserido em teste para que
/// apagar a linha quebre a suíte em vez de degradar em silêncio.
/// </para>
///
/// <para>
/// <b>(b) A configuração entra por <see cref="IWebHostBuilder.UseSetting"/>, NUNCA por
/// <c>ConfigureAppConfiguration</c>.</b> No modelo de hosting mínimo
/// (<c>WebApplication.CreateBuilder</c>), os callbacks de <c>ConfigureAppConfiguration</c>
/// registrados via <c>ConfigureWebHost</c> rodam DEPOIS de os top-level statements já
/// terem lido <c>builder.Configuration</c> — medido: o host morre com
/// <c>Connection string 'DefaultConnection' not configured.</c>. As chaves abaixo são
/// exatamente os pontos de fail-fast do startup (<c>ServiceCollectionExtensions</c>,
/// <c>HealthCheckExtensions</c>, <c>Program.cs</c>) mais as duas usadas em runtime pelos
/// filtros de API key.
/// </para>
/// </summary>
public class KuraApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Ambiente declarado ao host. Ver nota (a) na documentação da classe.</summary>
    public const string NomeAmbiente = "Testing";

    public const string EmailClinica = "integracao@kura.test";
    public const string SenhaClinica = "SenhaDeIntegracao#2026";
    public const long IdClinicaSemeada = 1;
    public const long IdVeterinarioSemeado = 1;
    public const string NomeVeterinarioSemeado = "Dra. Integração";

    // G2 Important-1: SEGUNDO tenant, semeado só para dar o que vazar.
    // Com uma clínica só, a asserção "todo item veio da minha clínica" é logicamente
    // incapaz de falhar — não existe linha de outro tenant no banco. Medido na revisão:
    // removendo o query filter de Veterinario, e depois zerando IClinicaContext
    // (vazamento cross-tenant total em produção), a suíte inteira ficava VERDE.
    // Nenhum teste faz login neste tenant: ele existe exclusivamente como isca.
    public const long IdClinicaOutroTenant = 2;
    public const long IdVeterinarioOutroTenant = 2;
    public const string NomeVeterinarioOutroTenant = "Dr. Outro Tenant";

    /// <summary>Chave HMAC do JWT. &gt;= 32 bytes, exigência do <c>SymmetricSecurityKey</c>.</summary>
    public const string ChaveJwt = "chave-de-integracao-s3d06-com-mais-de-32-bytes";
    public const string EmissorJwt = "kura-api";
    public const string AudienciaJwt = "kura-client";

    /// <summary>
    /// Connection string INERTE. Aponta para uma porta local morta, nunca para a FIAP.
    /// O <c>DbContext</c> real é substituído por InMemory em
    /// <see cref="ConfigureWebHost"/>, então este valor nunca é usado para abrir conexão —
    /// ele existe só para satisfazer o fail-fast de <c>AddInfrastructure</c>, que lê a
    /// configuração antes de qualquer substituição de serviço ser possível.
    /// </summary>
    public const string ConexaoInerte =
        "User Id=integracao;Password=integracao;Data Source=127.0.0.1:9999/inexistente";

    /// <summary>Base URL inerte da Luna — porta local morta, mesmo raciocínio da conexão.</summary>
    public const string UrlLunaInerte = "http://127.0.0.1:9999/";

    // Banco InMemory exclusivo desta instância de fábrica: duas classes de teste que
    // compartilhem a mesma instância (IClassFixture) veem o mesmo banco; instâncias
    // diferentes ficam isoladas.
    private readonly string _nomeBancoInMemory = $"kura-integration-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // (a) — ver documentação da classe. Não remover.
        builder.UseEnvironment(NomeAmbiente);

        // (b) — ver documentação da classe. UseSetting, não ConfigureAppConfiguration.
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConexaoInerte);
        builder.UseSetting("Jwt:Key", ChaveJwt);
        builder.UseSetting("Jwt:Issuer", EmissorJwt);
        builder.UseSetting("Jwt:Audience", AudienciaJwt);
        builder.UseSetting("Daily:ApiKey", "daily-api-key-de-integracao");
        builder.UseSetting("Luna:BaseUrl", UrlLunaInerte);
        builder.UseSetting("Luna:InboundApiKey", "luna-inbound-de-integracao");
        // Estas duas são lidas em runtime pelos filtros de API key (LunaApiKeyAuthFilter /
        // ApiKeyAuthFilter), não no startup. Declaradas aqui para que os endpoints da Luna
        // e de IoT não morram com InvalidOperationException se algum teste os exercitar.
        builder.UseSetting("Luna:ApiKey", "luna-api-key-de-integracao");
        builder.UseSetting("IoT:ApiKey", "iot-api-key-de-integracao");

        builder.ConfigureServices(services =>
        {
            // Remove TODO registro ligado ao KuraDbContext feito por AddInfrastructure
            // (o provider Oracle inclusive). O predicado por argumento genérico pega
            // também IDbContextOptionsConfiguration<KuraDbContext>, introduzido no EF 9 —
            // filtrar só por DbContextOptions<KuraDbContext> deixaria a configuração do
            // Oracle viva e ela voltaria a ser aplicada por cima do InMemory.
            var registrosDoContexto = services
                .Where(d => d.ServiceType == typeof(KuraDbContext)
                         || d.ServiceType == typeof(DbContextOptions)
                         || (d.ServiceType.IsGenericType
                             && d.ServiceType.GetGenericArguments().Contains(typeof(KuraDbContext))))
                .ToList();

            foreach (var registro in registrosDoContexto)
                services.Remove(registro);

            services.AddDbContext<KuraDbContext>(options =>
            {
                options.UseInMemoryDatabase(_nomeBancoInMemory);
                // Mantido para ficar fiel à produção: o interceptor é quem barra escrita
                // nas tabelas cuja autoridade é o backend Java (CONTA_TUTOR, CONSENTIMENTO).
                options.AddInterceptors(new ReadOnlyTablesInterceptor());
            });
        });
    }

    /// <summary>
    /// Semeia clínica + veterinário assim que o host é construído. O seed vive aqui, e não
    /// num script externo, de propósito: a suíte precisa ser autocontida (o CI roda
    /// <c>dotnet test</c> sem Oracle, sem <c>seed-demo.sh</c> e sem compose).
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        Semear(host.Services);
        return host;
    }

    private static void Semear(IServiceProvider services)
    {
        using var escopo = services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();

        // Sem HttpContext neste escopo, IClinicaContext.IdClinicaFiltro é null e os query
        // filters de tenant desligam inteiros — é o que permite semear sem JWT.
        if (db.Clinicas.Any())
            return;

        db.Clinicas.Add(new Clinica
        {
            Id = IdClinicaSemeada,
            NmClinica = "Clínica de Integração",
            NrCnpj = "00000000000191",
            NmRazaoSocial = "Clínica de Integração LTDA",
            DsEndereco = "Rua dos Testes, 100",
            NmCidade = "São Paulo",
            SgUf = "SP",
            NrCep = "01001000",
            NrTelefone = "1130000000",
            DsEmail = EmailClinica,
            DsEmailAcesso = EmailClinica,
            // Hash real: o login de produção valida com BCrypt.Verify. Semear a senha em
            // texto puro faria o cenário de login passar por um caminho que não existe.
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaClinica),
            StAtiva = true,
            DtCadastro = DateTime.UtcNow,
        });

        db.Veterinarios.Add(new Veterinario
        {
            Id = IdVeterinarioSemeado,
            IdClinica = IdClinicaSemeada,
            NmVeterinario = NomeVeterinarioSemeado,
            NrCrmv = "SP-99999",
            // Igual ao e-mail de acesso da clínica: é assim que AuthService.LoginAsync
            // escolhe o veterinário responsável pelo token.
            DsEmail = EmailClinica,
            NrTelefone = "11999990000",
            StAtiva = true,
        });

        // Segundo tenant — ver o comentário nas constantes. Sem esta clínica, a asserção
        // de escopo em FluxoDeNegocioHttpTests não tem como falhar.
        db.Clinicas.Add(new Clinica
        {
            Id = IdClinicaOutroTenant,
            NmClinica = "Clínica do Outro Tenant",
            NrCnpj = "00000000000272",
            NmRazaoSocial = "Clínica do Outro Tenant LTDA",
            DsEndereco = "Rua do Vazamento, 200",
            NmCidade = "São Paulo",
            SgUf = "SP",
            NrCep = "01002000",
            NrTelefone = "1130000001",
            DsEmail = "outro-tenant@kura.test",
            DsEmailAcesso = "outro-tenant@kura.test",
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaClinica),
            StAtiva = true,
            DtCadastro = DateTime.UtcNow,
        });

        db.Veterinarios.Add(new Veterinario
        {
            Id = IdVeterinarioOutroTenant,
            IdClinica = IdClinicaOutroTenant,
            NmVeterinario = NomeVeterinarioOutroTenant,
            NrCrmv = "SP-88888",
            DsEmail = "outro-tenant@kura.test",
            NrTelefone = "11988880000",
            StAtiva = true,
        });

        db.SaveChanges();
    }
}
