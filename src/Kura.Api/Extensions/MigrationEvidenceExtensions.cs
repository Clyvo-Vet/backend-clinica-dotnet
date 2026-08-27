namespace Kura.Api.Extensions;

/// <summary>
/// S3D-10: verificação de migrations pendentes do EF Core — extraída do <c>Program.cs</c> para
/// ficar testável, e tornada <b>não-fatal</b>.
///
/// <para><b>O defeito que originou esta classe (Critical, achado na revisão do G4).</b> O bloco
/// vivia inline no <c>Program.cs</c> e capturava apenas uma <b>allowlist escrita à mão</b> de 4
/// códigos Oracle: <c>[12514, 1109, 12541, 17002]</c>. Medido contra o ambiente real, essa lista
/// acerta <b>zero</b> dos 3 modos de falha que de fato ocorrem: <c>ORA-12154</c> e <c>ORA-12545</c>
/// (container do Oracle parado — o caso comum) e <c>ORA-50000</c> (container congelado). Com o
/// Oracle inalcançável <b>na partida</b>, a exceção escapava, o processo <b>morria</b>, e com
/// <c>restart: unless-stopped</c> isso virava crash loop: medidos <c>HTTP 000</c> por 139s
/// ininterruptos e <c>RestartCount=7</c>.</para>
///
/// <para>É a regra de ouro v7 do projeto dentro do código desta Sprint: <i>inventário escrito à mão
/// apodrece em silêncio</i>. A correção não é acrescentar os 3 códigos que faltavam — seria a mesma
/// classe de erro, só que com 7 itens em vez de 4. A correção é <b>parar de discriminar por
/// código</b>.</para>
///
/// <para><b>A invariante que esta classe garante:</b> nenhuma falha aqui pode impedir a API de
/// subir. O schema é responsabilidade do <b>Flyway</b> (ver <c>MIGRATIONS_POLICY.md</c>); as
/// migrations do EF Core existem apenas como <b>evidência</b> para a rubrica. Uma verificação de
/// evidência que derruba o processo inverte a prioridade: a API precisa subir e reportar o banco
/// via <c>GET /health</c> (<c>503</c> + <c>oracle: Unhealthy</c>), que é exatamente o
/// comportamento que a S3D-03 desenhou. Um processo morto não reporta nada.</para>
/// </summary>
public static class MigrationEvidenceExtensions
{
    /// <summary>Tentativas antes de desistir da evidência.</summary>
    /// <remarks>
    /// <b>5, não 10, e o número tem aritmética por trás.</b> O backoff é exponencial (1s, 2s, 4s,
    /// 8s), então 5 tentativas custam <b>~15s</b> de espera no pior caso. Com as 10 tentativas
    /// originais o teto seria <c>1+2+4+8+16+32+60+60+60 ≈ 243s</c> — e aí a correção seria falsa:
    /// o processo não morreria, mas também não passaria a escutar na porta a tempo. O healthcheck
    /// do container dá <c>start_period: 60s</c> + <c>5 × interval 30s</c> = <b>210s</b> antes de
    /// marcar <c>unhealthy</c>; 243s de espera estouraria esse orçamento e a Luna (que declara
    /// <c>depends_on: condition: service_healthy</c>) continuaria sem subir — o mesmo sintoma
    /// operacional do defeito original, só que mais lento e mais difícil de diagnosticar.
    /// Os ~15s cabem folgadamente dentro do <c>start_period</c>.
    /// </remarks>
    public const int TentativasPadrao = 5;

    /// <summary>
    /// Consulta as migrations pendentes e as registra em log. <b>Nunca lança.</b>
    /// </summary>
    /// <param name="obterMigrationsPendentes">
    /// Como obter a lista. Recebido como delegate para que o chamador de produção passe
    /// <c>context.Database.GetPendingMigrationsAsync</c> e os testes possam exercitar os caminhos
    /// de falha sem precisar de um Oracle — inclusive tipos de exceção que a allowlist antiga
    /// jamais capturaria.
    /// </param>
    /// <param name="logger">Destino das mensagens.</param>
    /// <param name="tentativas">Quantas vezes tentar antes de desistir.</param>
    /// <param name="aguardar">
    /// Injetável para que o teste não gaste 15s de relógio de parede. Em produção, <c>Task.Delay</c>.
    /// </param>
    public static async Task RegistrarMigrationsPendentesAsync(
        Func<Task<IEnumerable<string>>> obterMigrationsPendentes,
        ILogger logger,
        int tentativas = TentativasPadrao,
        Func<TimeSpan, Task>? aguardar = null)
    {
        ArgumentNullException.ThrowIfNull(obterMigrationsPendentes);
        ArgumentNullException.ThrowIfNull(logger);

        aguardar ??= atraso => Task.Delay(atraso);

        for (int tentativa = 1; tentativa <= tentativas; tentativa++)
        {
            try
            {
                var pendentes = (await obterMigrationsPendentes()).ToList();

                if (pendentes.Count > 0)
                {
                    logger.LogWarning(
                        "Existem {Count} migrations pendentes no EF Core. " +
                        "ATENÇÃO: schema é aplicado pelo Flyway. Migrations EF servem apenas como evidência. " +
                        "Migrations pendentes: {Migrations}",
                        pendentes.Count,
                        string.Join(", ", pendentes));
                }

                return;
            }
            // Sem filtro por código de erro: qualquer falha é tratada como transitória enquanto
            // houver tentativa sobrando. Discriminar por Number foi exatamente o defeito.
            catch (Exception ex) when (tentativa < tentativas)
            {
                var atrasoSegundos = Math.Min(Math.Pow(2, tentativa - 1), 60);

                logger.LogWarning(
                    ex,
                    "Não foi possível verificar migrations pendentes — tentativa {Tentativa}/{Tentativas}. " +
                    "Aguardando {Atraso}s antes de nova tentativa...",
                    tentativa,
                    tentativas,
                    atrasoSegundos);

                await aguardar(TimeSpan.FromSeconds(atrasoSegundos));
            }
            // Última tentativa. Este catch é a diferença entre "a API sobe degradada" e "o
            // processo morre em crash loop" — no código antigo, mesmo um erro considerado
            // retriável escapava aqui, porque a guarda era `attempt < maxAttempts`.
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Não foi possível verificar migrations pendentes após {Tentativas} tentativas. " +
                    "Seguindo com a subida: o schema é do Flyway e esta verificação é apenas evidência. " +
                    "O estado real do banco é reportado por GET /health.",
                    tentativas);

                return;
            }
        }
    }
}
