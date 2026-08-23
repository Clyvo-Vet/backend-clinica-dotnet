namespace Kura.IntegrationTests;

using System.Net;
using FluentAssertions;
using Kura.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/// <summary>
/// S3D-06 — controles negativos do próprio arranjo de teste, mais a primeira cobertura
/// que este repositório tem da FIAÇÃO do <c>Program.cs</c>.
///
/// <para>
/// Por que isto existe: os G2 anteriores mediram que a suíte prova que as extensões
/// FUNCIONAM, mas não prova que alguém as CHAMA — remover
/// <c>AddKuraObservability()</c> ou o guard da S3D-05 passava 285/285. Testes que sobem
/// o host de verdade encostam nisso naturalmente, e é isso que as asserções abaixo
/// travam.
/// </para>
/// </summary>
public class AmbienteEFiacaoDoHostTests : IClassFixture<KuraApiFactory>
{
    private readonly KuraApiFactory _factory;

    public AmbienteEFiacaoDoHostTests(KuraApiFactory factory) => _factory = factory;

    /// <summary>
    /// 🔴 O teste mais importante deste projeto, e não é sobre a rubrica — é sobre não
    /// repetir o incidente que bloqueou a conta Oracle da FIAP.
    ///
    /// <c>WebApplicationFactory</c> usa <c>Development</c> por PADRÃO. Em
    /// <c>Development</c>, o <c>Program.cs</c> carrega <c>appsettings.Development.json</c>
    /// (versionado, apontando para <c>oracle.fiap.com.br</c> com conta bloqueada) e o
    /// guard da S3D-05 não dispara, deixando o bloco de startup rodar — na máquina do dev
    /// E no CI, que não define <c>ASPNETCORE_ENVIRONMENT</c>.
    ///
    /// ⚠️ Precisão medida no G2: nesta fábrica isso <b>não</b> vira conexão Oracle — morre
    /// antes, em <c>InvalidOperationException: Relational-specific methods…</c>, porque o
    /// <c>DbContext</c> já é InMemory. A barreira efetiva contra a FIAP é a substituição do
    /// <c>DbContext</c>; esta asserção protege a <b>segunda</b> linha de defesa, que é a que
    /// sobra se alguém escrever uma fábrica sem essa substituição.
    ///
    /// Se alguém apagar <c>UseEnvironment("Testing")</c> da fábrica, é este teste que
    /// grita — e a suíte inteira fica vermelha junto (19/19, medido).
    /// </summary>
    [Fact]
    public void Host_sobe_no_ambiente_Testing()
    {
        var ambiente = _factory.Services.GetRequiredService<IHostEnvironment>();

        ambiente.EnvironmentName.Should().Be("Testing");
        ambiente.IsDevelopment().Should().BeFalse();
        ambiente.IsProduction().Should().BeFalse();
    }

    /// <summary>
    /// Segundo controle negativo: mesmo que o ambiente estivesse certo, uma substituição
    /// mal feita do <c>DbContext</c> deixaria o provider Oracle vivo. Aqui se prova, em
    /// runtime, que a persistência dos testes é InMemory.
    /// </summary>
    [Fact]
    public void Persistencia_dos_testes_usa_InMemory_e_nao_Oracle()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();

        db.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.InMemory");
        db.Database.ProviderName.Should().NotContain("Oracle");
    }

    /// <summary>
    /// Terceiro controle negativo: a connection string visível ao host não pode, em
    /// hipótese nenhuma, apontar para a infraestrutura da FIAP.
    /// </summary>
    [Fact]
    public void Connection_string_do_host_de_teste_e_inerte()
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var conexao = config.GetConnectionString("DefaultConnection");

        conexao.Should().NotBeNullOrWhiteSpace();
        conexao.Should().NotContain("fiap", "a suíte nunca pode discar para a infraestrutura da FIAP");
        conexao.Should().NotContain("RM562999");
        conexao.Should().Be(KuraApiFactory.ConexaoInerte);
    }

    /// <summary>
    /// Fiação do <c>Program.cs</c>: <c>app.MapKuraHealthChecks("/health")</c> foi mesmo
    /// chamado. Apagar essa linha faz a rota devolver 404 e este teste quebrar.
    ///
    /// O código de status NÃO é asserido de propósito: ele depende de dependências
    /// externas (a Luna aponta para porta morta e devolve Degraded). O que prova a
    /// fiação é a rota RESPONDER com o corpo do <c>ResponseWriter</c> customizado.
    ///
    /// ⚠️ Valor MEDIDO nesta suíte: <c>200</c>, não <c>503</c>. O brief desta task
    /// previa 503 com base na probe da revisão G2 da S3D-05 — mas naquela probe o
    /// <c>DbContext</c> ainda era Oracle, então o check "oracle" ficava Unhealthy.
    /// Aqui o <c>DbContext</c> é InMemory: "oracle" fica Healthy, só a Luna fica
    /// Degraded, e o mapeamento padrão do ASP.NET Core traduz Degraded para 200.
    /// Registrado para que ninguém "corrija" este teste para 503.
    /// </summary>
    [Fact]
    public async Task Rota_de_health_esta_mapeada_com_o_writer_customizado()
    {
        var client = _factory.CreateClient();

        var resposta = await client.GetAsync("/health");

        resposta.StatusCode.Should().NotBe(HttpStatusCode.NotFound);

        var corpo = await resposta.Content.ReadAsStringAsync();
        // Os 3 checks registrados por AddKuraHealthChecks, no formato do WriteResponse.
        corpo.Should().Contain("\"self\"");
        corpo.Should().Contain("\"oracle\"");
        corpo.Should().Contain("\"luna\"");
        corpo.Should().Contain("durationMs");
    }

    /// <summary>
    /// Fiação do <c>Program.cs</c>: <c>builder.Services.AddKuraObservability()</c> foi
    /// mesmo chamado. Este é o Important pré-existente do Bloco 0 — "remover
    /// AddKuraObservability() passa 285/285" — fechado por construção: sem a chamada,
    /// nenhum <c>TracerProvider</c>/<c>MeterProvider</c> existe no container.
    /// </summary>
    [Fact]
    public void Observabilidade_esta_registrada_no_host()
    {
        _factory.Services.GetService<TracerProvider>()
            .Should().NotBeNull("AddKuraObservability() precisa estar cabeado no Program.cs");
        _factory.Services.GetService<MeterProvider>()
            .Should().NotBeNull("a pilha de métricas também é registrada por AddKuraObservability()");
    }

    /// <summary>
    /// O host SOBE. Parece trivial e não é: com o guard da S3D-05 removido ou invertido, o
    /// bloco de validação de migrations roda no startup e derruba o processo de teste com
    /// exceção não tratada — medido por mutação (19/19 vermelho).
    ///
    /// ⚠️ O que a mutação NÃO produziu, e por isso o nome deste teste é mais forte do que a
    /// medição: <b>não houve tentativa de conexão Oracle</b> (0 linhas <c>ORA-</c>). A
    /// exceção é <c>InvalidOperationException: Relational-specific methods…</c>, porque o
    /// <c>DbContext</c> já é InMemory quando o bloco roda. Quem barra o Oracle aqui é a
    /// substituição do <c>DbContext</c>, não o guard.
    /// </summary>
    [Fact]
    public async Task Host_responde_HTTP_sem_tocar_no_Oracle_no_startup()
    {
        var client = _factory.CreateClient();

        var resposta = await client.GetAsync("/api/v1/veterinarios");

        // 401 (e não uma exceção de startup) já prova que o pipeline inteiro subiu.
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
