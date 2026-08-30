namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kura.Application.DTOs.Veterinario;

/// <summary>
/// FD-05, fix wave pós-G2 (R-1) — <b>a assimetria de
/// <c>/api/v1/veterinarios</c> quando o token NÃO tem a claim <c>clinicaId</c></b>, travada por
/// teste nas suas DUAS metades.
///
/// <para>
/// 🔴 <b>O estado medido, e ele NÃO é uniforme:</b>
/// </para>
/// <list type="bullet">
///   <item><description><c>POST</c> (<c>CreateAsync</c>) <b>falha FECHADO</b> — <c>401</c>. A
///   clínica sai de <c>IClinicaContext.IdClinica</c>, que é <c>GetRequiredClaimValue</c> e
///   <b>LANÇA</b> <c>UnauthorizedAccessException</c> quando a claim falta; o
///   <c>ExceptionHandlerMiddleware</c> mapeia para <c>401</c>. Nada é gravado.</description></item>
///   <item><description><c>PUT</c>/<c>DELETE</c> (<c>UpdateAsync</c>/<c>SoftDeleteAsync</c>)
///   <b>falham ABERTO</b> — <c>200</c>/<c>204</c> sobre veterinário de <b>outra</b> clínica. Eles
///   não consultam <c>IdClinica</c>: dependem do query filter, que lê <c>IdClinicaFiltro</c>
///   (<c>TryGetClaimValue</c>, devolve <c>null</c>) e <b>desliga inteiro</b> em vez de
///   negar.</description></item>
/// </list>
///
/// <para>
/// ⚠️ <b>Isto documenta um estado conhecido-aberto; não o corrige.</b> Fechá-lo exigiria
/// comparação explícita de tenant em <c>UpdateAsync</c>/<c>SoftDeleteAsync</c> — escopo novo,
/// fora da FD-05. O ponto de escrever o teste é que <b>mudar esse comportamento passe a ser uma
/// decisão consciente</b>, com um vermelho na cara de quem mudar, em vez de um acidente. Este
/// ciclo já encontrou 3× o padrão "código certo, sem trava", e na FD-03 uma dessas era vazamento
/// cross-tenant explorável.
/// </para>
///
/// <para>
/// ⚠️ <b>O caso é alcançável hoje?</b> Por token emitido pelo login, <b>não</b>:
/// <c>AuthService.GenerateToken</c> põe <c>clinicaId</c> incondicionalmente. Por API key
/// também não — <c>POST</c>/<c>PUT</c>/<c>DELETE</c> com <c>X-Api-Key</c> e sem <c>Bearer</c>
/// devolvem <c>401</c> nos três (medido na revisão G2). O token usado aqui é
/// <b>forjado de propósito</b> (<see cref="AutenticacaoHelper.GerarTokenGestorSemClinicaId"/>,
/// herdado da FD-04): assinatura, emissor, audiência e validade corretos, sem
/// <c>clinicaId</c>. Ele não representa um ataque disponível — representa o <b>contrato</b> que
/// vale se um caminho futuro (outro esquema de autenticação, um job, um token de serviço)
/// alcançar estes métodos sem tenant.
/// </para>
///
/// <para>
/// <b>Host PRÓPRIO (<c>IClassFixture</c>), obrigatório aqui:</b> o cenário aberto <b>escreve e
/// desativa o veterinário do OUTRO tenant</b>, que é a isca compartilhada da
/// <see cref="KuraApiFactory"/>. Rodar isto no mesmo banco de qualquer outra classe tornaria
/// aquelas classes dependentes de ordem de execução — que o xUnit não garante.
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class VeterinariosSemClinicaNoTokenHttpTests : IClassFixture<KuraApiFactory>
{
    private const string Rota = "/api/v1/veterinarios";

    private readonly KuraApiFactory _factory;

    public VeterinariosSemClinicaNoTokenHttpTests(KuraApiFactory factory) => _factory = factory;

    /// <summary>Cliente com token VÁLIDO e SEM a claim <c>clinicaId</c>.</summary>
    private HttpClient ClienteSemClinica()
    {
        var client = _factory.CreateClient();
        client.UsarToken(AutenticacaoHelper.GerarTokenGestorSemClinicaId());
        return client;
    }

    /// <summary>
    /// <b>Metade FECHADA.</b> Sem clínica no token, criar veterinário é <c>401</c> e não grava
    /// nada. Correção de uma afirmação do relatório original desta task, que estendia o "falha
    /// aberto" do query filter também ao <c>CreateAsync</c> — errado: <c>IdClinicaFiltro</c>
    /// devolve <c>null</c> (desliga), mas <c>IdClinica</c> <b>lança</b>.
    /// </summary>
    [Fact]
    public async Task Criar_sem_a_claim_de_clinica_falha_FECHADO_em_401_e_nao_grava()
    {
        // Arrange
        var semClinica = ClienteSemClinica();

        // Act
        var criacao = await semClinica.PostAsJsonAsync(Rota, new
        {
            nmVeterinario = "Dr. Sem Tenant",
            nrCrmv = "SP-55555",
            dsEmail = "sem-tenant@kura.test",
            nrTelefone = "11955554444",
        });

        // Assert — o status.
        criacao.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "IClinicaContext.IdClinica é GetRequiredClaimValue e LANÇA sem a claim; o "
            + "ExceptionHandlerMiddleware mapeia para 401");

        // Assert — e NADA foi gravado. Um 401 emitido DEPOIS de um INSERT seria um 401
        // mentiroso, e o status sozinho não distingue os dois casos. A verificação usa um
        // token normal (clínica semeada), que é o único jeito de listar com tenant.
        var comClinica = _factory.CreateClient();
        comClinica.UsarToken(await AutenticacaoHelper.ObterTokenAsync(comClinica));

        var lista = await comClinica.GetAsync(Rota);
        lista.StatusCode.Should().Be(HttpStatusCode.OK);

        var veterinarios = await lista.Content.ReadFromJsonAsync<List<VeterinarioResponseDto>>();
        veterinarios.Should().NotBeNull();
        veterinarios!.Should().NotContain(
            v => v.DsEmail == "sem-tenant@kura.test",
            "o 401 precisa ser anterior à escrita, não posterior");
    }

    /// <summary>
    /// <b>Metade ABERTA — este teste asserta o que HOJE está errado, de propósito.</b> Com o
    /// filtro desligado, o veterinário do outro tenant fica <b>visível</b> e <b>editável</b>.
    ///
    /// <para>
    /// 🔴 <b>Se alguém fechar essa lacuna, este teste FICA VERMELHO — e isso é o comportamento
    /// desejado.</b> A ação certa então não é apagá-lo: é invertê-lo (200/204 → 404) e registrar
    /// a mudança de contrato. Um teste que documenta lacuna precisa quebrar quando a lacuna
    /// fecha, senão ele é só um comentário caro.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Atualizar_e_desativar_sem_a_claim_de_clinica_falham_ABERTO_no_outro_tenant()
    {
        // Arrange
        var semClinica = ClienteSemClinica();
        var alvo = $"{Rota}/{KuraApiFactory.IdVeterinarioOutroTenant}";

        // Controle positivo do arranjo: com o filtro desligado, o recurso alheio é VISÍVEL.
        // Sem esta linha, um 200 no PUT poderia ser lido como qualquer outra coisa.
        var leitura = await semClinica.GetAsync(alvo);
        leitura.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "sem clinicaId o query filter desliga INTEIRO (não nega), então o veterinário do "
            + "outro tenant aparece");

        // Act
        var atualizacao = await semClinica.PutAsJsonAsync(alvo, new
        {
            nmVeterinario = "Alterado Sem Tenant",
            nrCrmv = "SP-44444",
            dsEmail = "alterado-sem-tenant@kura.test",
            nrTelefone = "11944443333",
        });

        var desativacao = await semClinica.DeleteAsync(alvo);

        // Assert — o estado ABERTO, escrito como está e não como gostaríamos.
        atualizacao.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "ESTADO CONHECIDO-ABERTO: UpdateAsync não consulta IClinicaContext.IdClinica e "
            + "depende só do query filter, que desliga sem a claim. Se isto virar 404, a "
            + "lacuna foi fechada — inverta o teste em vez de apagá-lo");

        desativacao.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "ESTADO CONHECIDO-ABERTO: mesma razão do PUT acima");
    }
}
