namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Kura.Application.DTOs.Veterinario;

/// <summary>
/// S3D-06 — item da rubrica: "RESPOSTAS DE SUCESSO e TRATAMENTO DE ERROS" num endpoint
/// real de negócio, com o token obtido pelo endpoint real de login. A cadeia exercitada
/// é a de produção inteira: JWT -> <c>ClinicaContext</c> (claim <c>clinicaId</c>) ->
/// query filter de tenant do <c>KuraDbContext</c> -> repositório -> service -> controller.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class FluxoDeNegocioHttpTests
{
    private readonly KuraApiFactory _factory;

    public FluxoDeNegocioHttpTests(KuraApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAutenticadoAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));
        return client;
    }

    [Fact]
    public async Task Listar_veterinarios_autenticado_devolve_200_com_o_veterinario_da_clinica()
    {
        // Arrange
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await client.GetAsync("/api/v1/veterinarios");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var lista = await resposta.Content.ReadFromJsonAsync<List<VeterinarioResponseDto>>();
        lista.Should().NotBeNull();
        lista!.Should().Contain(v => v.Id == KuraApiFactory.IdVeterinarioSemeado
                                  && v.NmVeterinario == KuraApiFactory.NomeVeterinarioSemeado);
        // Escopo de tenant. O banco tem DUAS clínicas semeadas, cada uma com veterinário
        // próprio; o token é só da primeira. Se o query filter de tenant for removido, ou
        // se IClinicaContext devolver null (que em produção é vazamento cross-tenant
        // total), o veterinário do outro tenant aparece aqui e as asserções abaixo quebram.
        // Com uma clínica só — como era antes do G2 — elas não tinham como falhar.
        lista.Should().NotContain(v => v.Id == KuraApiFactory.IdVeterinarioOutroTenant,
            "o veterinário da outra clínica não pode vazar para este token");
        lista.Should().OnlyContain(v => v.IdClinica == KuraApiFactory.IdClinicaSemeada);
    }

    [Fact]
    public async Task Buscar_veterinario_por_id_devolve_200_com_o_recurso()
    {
        // Arrange
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await client.GetAsync($"/api/v1/veterinarios/{KuraApiFactory.IdVeterinarioSemeado}");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var veterinario = await resposta.Content.ReadFromJsonAsync<VeterinarioResponseDto>();
        veterinario.Should().NotBeNull();
        veterinario!.Id.Should().Be(KuraApiFactory.IdVeterinarioSemeado);
        veterinario.DsEmail.Should().Be(KuraApiFactory.EmailClinica);
    }

    [Fact]
    public async Task Buscar_recurso_inexistente_devolve_404_com_corpo_de_erro_padrao()
    {
        var client = await ClienteAutenticadoAsync();

        var resposta = await client.GetAsync("/api/v1/veterinarios/999999");

        // EntidadeNaoEncontradaException -> 404, mapeado pelo ExceptionHandlerMiddleware.
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var raiz = corpo.RootElement;

        raiz.GetProperty("type").GetString().Should().Be("EntidadeNaoEncontradaException");
        raiz.GetProperty("status").GetInt32().Should().Be(404);
        raiz.GetProperty("title").GetString().Should().Contain("999999");
        raiz.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Criar_veterinario_devolve_201_com_Location_e_o_recurso_fica_consultavel()
    {
        // Arrange
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await client.PostAsJsonAsync("/api/v1/veterinarios", new
        {
            // FD-05: o corpo NAO carrega mais idClinica - a clinica sai do JWT. Ver
            // VeterinarioCreateDto e VeterinariosTenantHttpTests.
            nmVeterinario = "Dr. Criado por Integração",
            nrCrmv = "SP-12345",
            dsEmail = "criado@kura.test",
            nrTelefone = "11988887777",
        });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Location.Should().NotBeNull();

        var criado = await resposta.Content.ReadFromJsonAsync<VeterinarioResponseDto>();
        criado.Should().NotBeNull();
        criado!.Id.Should().BeGreaterThan(0);

        // Segunda requisição HTTP: prova que o POST persistiu de verdade, não só devolveu
        // o eco do DTO enviado.
        var consulta = await client.GetAsync($"/api/v1/veterinarios/{criado.Id}");
        consulta.StatusCode.Should().Be(HttpStatusCode.OK);
        var lido = await consulta.Content.ReadFromJsonAsync<VeterinarioResponseDto>();
        lido!.NmVeterinario.Should().Be("Dr. Criado por Integração");
    }

    [Fact]
    public async Task Criar_veterinario_com_payload_invalido_devolve_400_de_validacao()
    {
        // Arrange
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await client.PostAsJsonAsync("/api/v1/veterinarios", new
        {
            nmVeterinario = "",   // NotEmpty no VeterinarioCreateValidator
            nrCrmv = "",          // NotEmpty
            dsEmail = "",         // NotEmpty
            nrTelefone = "11988887777",
        });

        // Assert
        // Caminho de erro DIFERENTE do 422/404 acima: aqui quem responde é a validação
        // automática do [ApiController] + FluentValidation, antes de o controller rodar —
        // ou seja, o ValidationProblemDetails do ASP.NET, não o middleware de exceção.
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Rota_inexistente_devolve_404_sem_derrubar_o_pipeline()
    {
        // Arrange
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await client.GetAsync("/api/v1/rota-que-nao-existe");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
