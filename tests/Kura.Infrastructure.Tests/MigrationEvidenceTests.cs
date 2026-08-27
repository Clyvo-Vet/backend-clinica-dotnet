using Kura.Api.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kura.Infrastructure.Tests;

/// <summary>
/// S3D-10 — prova de mordida do <c>Critical</c> achado na revisão do G4.
///
/// <para><b>O defeito:</b> a verificação de migrations pendentes no <c>Program.cs</c> capturava
/// apenas uma allowlist escrita à mão de 4 códigos Oracle (<c>[12514, 1109, 12541, 17002]</c>).
/// Medida contra o ambiente real, ela acerta <b>zero</b> dos 3 modos de falha que ocorrem de fato
/// (<c>12154</c>/<c>12545</c> com o container parado, <c>50000</c> com ele congelado). Resultado
/// medido: Oracle inalcançável na partida → exceção não tratada → processo morto → crash loop,
/// <c>HTTP 000</c> por 139s e <c>RestartCount=7</c>.</para>
///
/// <para><b>Por que estes testes usam exceções genéricas em vez de <c>OracleException</c>:</b> não
/// é conveniência. A invariante que precisa ser protegida não é <i>"trate estes códigos Oracle"</i>
/// — foi justamente essa formulação que produziu o defeito. A invariante é <b>"nenhuma falha desta
/// verificação pode escapar"</b>, e ela é mais forte quando exercitada com um tipo que a allowlist
/// antiga jamais capturaria: se o teste passa com <c>InvalidOperationException</c>, passa também
/// com qualquer <c>ORA-*</c>, presente ou futuro.</para>
///
/// <para><b>Mordida verificada:</b> revertendo <c>MigrationEvidenceExtensions</c> para a forma
/// antiga (<c>catch (OracleException ex) when (retriableErrors.Contains(ex.Number) &amp;&amp;
/// attempt &lt; maxAttempts)</c>), os 3 primeiros testes ficam <b>vermelhos</b> — a exceção
/// atravessa o método.</para>
/// </summary>
public class MigrationEvidenceTests
{
    /// <summary>Não espera de verdade — o backoff real custaria ~15s de relógio por teste.</summary>
    private static readonly Func<TimeSpan, Task> SemEspera = _ => Task.CompletedTask;

    [Fact]
    public async Task RegistrarMigrationsPendentesAsync_FalhaSempreComTipoForaDaAllowlistAntiga_NaoLanca()
    {
        // Arrange — InvalidOperationException NUNCA seria capturada pelo catch antigo, que exigia
        // OracleException com Number dentro de uma lista de 4 itens.
        Func<Task<IEnumerable<string>>> sempreFalha =
            () => throw new InvalidOperationException("Oracle inalcançável na partida");

        // Act
        var excecao = await Record.ExceptionAsync(() =>
            MigrationEvidenceExtensions.RegistrarMigrationsPendentesAsync(
                sempreFalha,
                NullLogger.Instance,
                tentativas: 3,
                aguardar: SemEspera));

        // Assert — é exatamente aqui que o processo morria em produção.
        Assert.Null(excecao);
    }

    [Fact]
    public async Task RegistrarMigrationsPendentesAsync_FalhaNaUltimaTentativa_NaoLanca()
    {
        // Arrange — o segundo defeito do bloco antigo: a guarda `attempt < maxAttempts` deixava
        // escapar até o erro considerado retriável, na última volta. O retry era terminal.
        var chamadas = 0;
        Func<Task<IEnumerable<string>>> falhaSempre = () =>
        {
            chamadas++;
            throw new TimeoutException("ORA-50000: Connection request timed out");
        };

        // Act
        var excecao = await Record.ExceptionAsync(() =>
            MigrationEvidenceExtensions.RegistrarMigrationsPendentesAsync(
                falhaSempre,
                NullLogger.Instance,
                tentativas: 4,
                aguardar: SemEspera));

        // Assert
        Assert.Null(excecao);
        Assert.Equal(4, chamadas);
    }

    [Fact]
    public async Task RegistrarMigrationsPendentesAsync_FalhaTransitoriaDepoisSucesso_NaoLancaERegistraPendentes()
    {
        // Arrange — Oracle demorando a registrar o serviço no listener: falha 2×, depois responde.
        var chamadas = 0;
        Func<Task<IEnumerable<string>>> falhaDuasVezes = () =>
        {
            chamadas++;
            if (chamadas <= 2)
            {
                throw new InvalidOperationException("listener ainda não registrou XEPDB1");
            }

            return Task.FromResult<IEnumerable<string>>(["20260811123528_Task77"]);
        };

        var logger = new ListaDeLogsLogger();

        // Act
        var excecao = await Record.ExceptionAsync(() =>
            MigrationEvidenceExtensions.RegistrarMigrationsPendentesAsync(
                falhaDuasVezes,
                logger,
                tentativas: 5,
                aguardar: SemEspera));

        // Assert — parou na 3ª (não gastou as 5) e registrou a evidência.
        Assert.Null(excecao);
        Assert.Equal(3, chamadas);
        Assert.Contains(logger.Mensagens, m => m.Contains("migrations pendentes no EF Core"));
    }

    [Fact]
    public async Task RegistrarMigrationsPendentesAsync_SemMigrationsPendentes_NaoRegistraAviso()
    {
        // Arrange
        Func<Task<IEnumerable<string>>> nenhumaPendente =
            () => Task.FromResult<IEnumerable<string>>([]);

        var logger = new ListaDeLogsLogger();

        // Act
        await MigrationEvidenceExtensions.RegistrarMigrationsPendentesAsync(
            nenhumaPendente,
            logger,
            aguardar: SemEspera);

        // Assert — silêncio quando não há nada a reportar.
        Assert.Empty(logger.Mensagens);
    }

    [Fact]
    public async Task RegistrarMigrationsPendentesAsync_FalhaPersistente_RegistraMotivoNoLog()
    {
        // Arrange — falhar em silêncio seria trocar um defeito barulhento por um mudo.
        Func<Task<IEnumerable<string>>> sempreFalha =
            () => throw new InvalidOperationException("ORA-12154");

        var logger = new ListaDeLogsLogger();

        // Act
        await MigrationEvidenceExtensions.RegistrarMigrationsPendentesAsync(
            sempreFalha,
            logger,
            tentativas: 2,
            aguardar: SemEspera);

        // Assert
        Assert.Contains(logger.Mensagens, m => m.Contains("Não foi possível verificar migrations"));
    }

    /// <summary>Logger mínimo que só acumula as mensagens formatadas, para asserção.</summary>
    private sealed class ListaDeLogsLogger : ILogger
    {
        public List<string> Mensagens { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Mensagens.Add(formatter(state, exception));
    }
}
