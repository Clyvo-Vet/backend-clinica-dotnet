namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kura.Application.DTOs.Veterinario;

/// <summary>
/// FD-05 (ciclo FIN) — <b>escrita cross-tenant em <c>POST /api/v1/veterinarios</c></b>, e o
/// que os outros dois métodos de escrita do mesmo controller fazem com um id alheio.
///
/// <para>
/// 🔴 <b>O defeito que originou a task:</b> <c>VeterinarioService.CreateAsync</c> gravava
/// <c>dto.IdClinica</c> — valor vindo do <b>corpo</b> — sem nunca compará-lo com o
/// <c>clinicaId</c> do JWT. <c>VeterinariosController</c> exige apenas <c>[Authorize]</c>.
/// Resultado medido: qualquer clínica autenticada criava veterinário <b>dentro de outra
/// clínica</b>.
/// </para>
///
/// <para>
/// 🔴 <b>Por que estes cenários vivem em HTTP e não só no service.</b> Os testes de service
/// deste repositório rodam com <c>IdClinicaFiltro = null</c>, isto é, com os query filters de
/// tenant <b>desligados</b> — arranjo hostil de propósito, mas que não consegue observar o
/// caminho de produção, onde o filtro está ligado. A pergunta de <c>UpdateAsync</c> /
/// <c>SoftDeleteAsync</c> ("o filtro de fato morde num PUT/DELETE real?") só tem resposta
/// aqui, com JWT de verdade emitido pelo endpoint de login de verdade. É a mesma lição
/// medida na fix wave pós-G2 da FD-04.
/// </para>
///
/// <para>
/// ⚠️ <b>Host PRÓPRIO (<c>IClassFixture</c>), e não a <see cref="ColecaoDeIntegracao"/>.</b>
/// Esta classe <b>desativa</b> veterinário (<c>DELETE</c>), e o banco InMemory é compartilhado
/// por todas as classes de uma mesma collection: um soft delete daqui apagaria da lista o
/// veterinário semeado que <see cref="FluxoDeNegocioHttpTests"/> asserta, criando dependência
/// de ordem de execução — que o xUnit não garante. O custo é um bootstrap a mais; o benefício
/// é que a suíte não fica ordem-dependente.
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class VeterinariosTenantHttpTests : IClassFixture<KuraApiFactory>
{
    private readonly KuraApiFactory _factory;

    public VeterinariosTenantHttpTests(KuraApiFactory factory) => _factory = factory;

    /// <summary>Cliente logado como a clínica SEMEADA (id 1). O outro tenant é o id 2.</summary>
    private async Task<HttpClient> ClienteDaClinicaSemeadaAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));
        return client;
    }

    /// <summary>
    /// 🔴 <b>A MORDIDA DESTA TASK.</b> O corpo pede explicitamente a clínica do OUTRO tenant;
    /// o JWT é da clínica semeada. O veterinário tem de nascer na clínica do <b>token</b>.
    ///
    /// <para>
    /// <b>Este teste mantém a mesma forma antes e depois do fix</b>, o que é raro e vale
    /// registrar: a correção <b>removeu</b> <c>IdClinica</c> de
    /// <see cref="VeterinarioCreateDto"/>, e o <c>System.Text.Json</c> deste projeto
    /// <b>ignora</b> propriedade desconhecida no corpo (não há
    /// <c>UnmappedMemberHandling.Disallow</c> configurado em lugar nenhum — verificado por
    /// varredura em <c>src/</c>). Ou seja, o payload hostil continua sendo aceito na rede e
    /// continua sendo <b>desprezado</b> na desserialização. Contra o código antigo esta
    /// asserção falhava com <c>Expected criado.IdClinica to be 1L, but found 2L</c>.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Criar_veterinario_pedindo_outra_clinica_no_corpo_grava_na_clinica_do_JWT()
    {
        // Arrange
        var client = await ClienteDaClinicaSemeadaAsync();

        // Act — corpo hostil: idClinica do OUTRO tenant.
        var resposta = await client.PostAsJsonAsync("/api/v1/veterinarios", new
        {
            idClinica = KuraApiFactory.IdClinicaOutroTenant,
            nmVeterinario = "Dr. Injetado no Outro Tenant",
            nrCrmv = "SP-77777",
            dsEmail = "injetado@kura.test",
            nrTelefone = "11977776666",
        });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);

        var criado = await resposta.Content.ReadFromJsonAsync<VeterinarioResponseDto>();
        criado.Should().NotBeNull();
        criado!.IdClinica.Should().Be(
            KuraApiFactory.IdClinicaSemeada,
            "a clínica do veterinário criado sai do clinicaId do JWT, nunca do corpo");

        // Controle positivo do próprio instrumento: se a linha tivesse nascido na clínica 2,
        // ela seria invisível para este token e este GET devolveria 404. O 200 prova que a
        // asserção acima não passou por a resposta ser um eco do DTO — o recurso está mesmo
        // no tenant de quem chamou.
        var consulta = await client.GetAsync($"/api/v1/veterinarios/{criado.Id}");
        consulta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <b>UpdateAsync — o que foi medido.</b> O método não tem o buraco do
    /// <c>CreateAsync</c> (o <c>VeterinarioUpdateDto</c> nunca teve campo <c>IdClinica</c>, e
    /// o registro é buscado por <c>GetByIdAsync</c>, que é <c>DbSet.FindAsync</c> e
    /// <b>aplica</b> os query filters — medido na FD-04, MED-2). O isolamento aqui é, porém,
    /// <b>ambiente</b>: vem do query filter, não de comparação escrita no service. Este teste
    /// existe para que essa proteção pare de ser invisível — apagar o filtro de
    /// <c>Veterinario</c> em <c>KuraDbContext.ApplyTenantFilters</c> troca este 404 por 200.
    /// </summary>
    [Fact]
    public async Task Atualizar_veterinario_de_outra_clinica_devolve_404()
    {
        // Arrange
        var client = await ClienteDaClinicaSemeadaAsync();

        var corpo = new
        {
            nmVeterinario = "Sequestrado",
            nrCrmv = "SP-00000",
            dsEmail = "sequestrado@kura.test",
            nrTelefone = "11900000000",
        };

        // Act
        var alheio = await client.PutAsJsonAsync(
            $"/api/v1/veterinarios/{KuraApiFactory.IdVeterinarioOutroTenant}", corpo);

        // Controle positivo: MESMO verbo, MESMO corpo, MESMO token, id da PRÓPRIA clínica.
        // Sem ele, um 404 por rota errada, verbo não mapeado ou payload recusado passaria por
        // "isolamento funcionando" — foi exatamente esse o falso verde da FD-03.
        var proprio = await client.PutAsJsonAsync(
            $"/api/v1/veterinarios/{KuraApiFactory.IdVeterinarioSemeado}", corpo);

        // Assert
        alheio.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "o veterinário do outro tenant não existe para este token");
        proprio.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "controle positivo: o mesmo PUT no próprio tenant precisa funcionar, senão o 404 "
            + "acima não prova isolamento nenhum");
    }

    /// <summary>
    /// 🟢 <b>R-2 da fix wave pós-G2: o parâmetro <c>?clinicaId=</c> foi REMOVIDO da listagem</b>
    /// — o argumento está em <c>VeterinariosController.GetAll</c>. Este teste era, antes,
    /// <c>Listar_veterinarios_filtrando_por_outra_clinica_devolve_lista_vazia</c>, e provava a
    /// assimetria que a remoção acabou de eliminar: a escrita não aceitava mais clínica do
    /// cliente, a leitura ainda aceitava.
    ///
    /// <para>Na forma nova ele prova <b>duas</b> coisas de uma vez: que mandar o parâmetro não
    /// causa erro (compatibilidade para trás — o ASP.NET ignora query não vinculada) e que ele
    /// não tem <b>efeito</b>, porque a resposta continua sendo a lista do tenant do token, sem o
    /// veterinário da outra clínica. A asserção de <c>NotContain</c> é a que morde: ela quebra
    /// se o query filter de tenant for removido.</para>
    /// </summary>
    [Fact]
    public async Task Listar_com_clinicaId_de_outro_tenant_na_query_ignora_o_parametro()
    {
        // Arrange
        var client = await ClienteDaClinicaSemeadaAsync();

        // Act — o parâmetro que deixou de existir, mandado assim mesmo.
        var comParametro = await client.GetAsync(
            $"/api/v1/veterinarios?clinicaId={KuraApiFactory.IdClinicaOutroTenant}");

        // Controle: a MESMA rota sem parâmetro nenhum. As duas respostas têm de descrever o
        // mesmo conjunto — é isso que "o parâmetro não tem efeito" significa.
        var semParametro = await client.GetAsync("/api/v1/veterinarios");

        // Assert
        comParametro.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "query string não vinculada é ignorada pelo ASP.NET, não recusada");
        semParametro.StatusCode.Should().Be(HttpStatusCode.OK);

        var comLista = await comParametro.Content.ReadFromJsonAsync<List<VeterinarioResponseDto>>();
        var semLista = await semParametro.Content.ReadFromJsonAsync<List<VeterinarioResponseDto>>();

        comLista.Should().NotBeNull();
        comLista!.Should().NotBeEmpty(
            "antes do R-2 este mesmo pedido devolvia lista VAZIA — o parâmetro parecia escopar");
        comLista.Should().OnlyContain(v => v.IdClinica == KuraApiFactory.IdClinicaSemeada);
        comLista.Should().NotContain(
            v => v.Id == KuraApiFactory.IdVeterinarioOutroTenant,
            "pedir a outra clínica na query não pode trazer o veterinário dela");

        comLista.Select(v => v.Id).Should().BeEquivalentTo(
            semLista!.Select(v => v.Id),
            "o parâmetro removido não pode ter efeito nenhum sobre o resultado");
    }

    /// <summary>
    /// <b>SoftDeleteAsync — mesma medição do <c>UpdateAsync</c>.</b> O controle positivo
    /// apaga um veterinário <b>criado por este próprio teste</b>, e não o semeado: desativar o
    /// semeado deixaria o host desta classe num estado que nenhum outro teste dela espera.
    /// </summary>
    [Fact]
    public async Task Desativar_veterinario_de_outra_clinica_devolve_404()
    {
        // Arrange
        var client = await ClienteDaClinicaSemeadaAsync();

        var criacao = await client.PostAsJsonAsync("/api/v1/veterinarios", new
        {
            nmVeterinario = "Dr. Descartável",
            nrCrmv = "SP-66666",
            dsEmail = "descartavel@kura.test",
            nrTelefone = "11966665555",
        });
        criacao.StatusCode.Should().Be(HttpStatusCode.Created);
        var descartavel = await criacao.Content.ReadFromJsonAsync<VeterinarioResponseDto>();

        // Act
        var alheio = await client.DeleteAsync(
            $"/api/v1/veterinarios/{KuraApiFactory.IdVeterinarioOutroTenant}");
        var proprio = await client.DeleteAsync($"/api/v1/veterinarios/{descartavel!.Id}");

        // Assert
        alheio.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "o veterinário do outro tenant não existe para este token");
        proprio.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "controle positivo: o mesmo DELETE no próprio tenant precisa funcionar");
    }
}
