namespace Kura.Application.Tests;

using System.Text.Json;
using FluentAssertions;
using Kura.Application.DTOs.Luna;

/// <summary>
/// TASK-67: prova que o JSON que sai/entra destes 4 DTOs bate com o snake_case do
/// Pydantic (kura-luna-ai/luna/src/integration/dtos.py), não com a política default
/// de camelCase que este projeto usa em todo o resto da API (ver
/// VeterinarioResponseDtoSerializationTests — mesma técnica). Usa
/// JsonNamingPolicy.CamelCase de propósito nas opções, igual ao runtime real do
/// ASP.NET Core: se algum [JsonPropertyName] fosse removido por engano, a política
/// default assumiria e o teste pegaria a regressão (a mesma classe de bug que
/// originou o KURA_BACKLOG_FIX_4 inteiro — camelCase aqui, snake_case lá).
/// </summary>
public class LunaDtoSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void TutorContextoLunaDto_Serializa_ComChavesSnakeCase()
    {
        // Arrange
        var dto = new TutorContextoLunaDto
        {
            IdTutor = 7,
            NmTutor = "Fulano",
            DsWhatsapp = "5511999990000",
            IdClinica = 42,
            Pets = [new PetResumoLunaDto { IdPet = 3, NmPet = "Rex", NmEspecie = "Cachorro", NmRaca = "Vira-lata" }]
        };

        // Act
        var json = JsonSerializer.Serialize(dto, Options);
        using var doc = JsonDocument.Parse(json);

        // Assert
        doc.RootElement.TryGetProperty("id_tutor", out var idTutor).Should().BeTrue();
        idTutor.GetInt64().Should().Be(7);
        doc.RootElement.TryGetProperty("nm_tutor", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("ds_whatsapp", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("id_clinica", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("pets", out var pets).Should().BeTrue();
        pets[0].TryGetProperty("id_pet", out _).Should().BeTrue();
        pets[0].TryGetProperty("nm_pet", out _).Should().BeTrue();
        pets[0].TryGetProperty("nm_especie", out _).Should().BeTrue();
        pets[0].TryGetProperty("nm_raca", out _).Should().BeTrue();

        // Nenhuma chave camelCase deve escapar — provaria que algum [JsonPropertyName] sumiu.
        doc.RootElement.TryGetProperty("idTutor", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("dsWhatsapp", out _).Should().BeFalse();
    }

    [Fact]
    public void InteractionRequestDto_Deserializa_PayloadRealDoPydantic()
    {
        // Arrange
        // Espelha literalmente dto.model_dump(mode="json") do InteractionRequestDTO real
        // (kura_client.py:83-89).
        const string json = """
        {
            "id_tutor": 7,
            "ds_canal": "WHATSAPP",
            "ds_direcao": "INBOUND",
            "ds_conteudo": "Meu pet está com febre",
            "dt_recebimento": "2026-08-08T10:00:00",
            "ds_metadados": null
        }
        """;

        // Act
        var dto = JsonSerializer.Deserialize<InteractionRequestDto>(json, Options);

        // Assert
        dto.Should().NotBeNull();
        dto!.IdTutor.Should().Be(7);
        dto.DsCanal.Should().Be("WHATSAPP");
        dto.DsDirecao.Should().Be("INBOUND");
        dto.DsConteudo.Should().Be("Meu pet está com febre");
        dto.DtRecebimento.Should().Be(new DateTime(2026, 8, 8, 10, 0, 0));
        dto.DsMetadados.Should().BeNull();
    }

    [Fact]
    public void InteractionRequestDto_Deserializa_IdTutorNull_SemLancar()
    {
        // Arrange
        // InteractionRequestDTO.id_tutor é `int | None` no Pydantic — precisa desserializar
        // sem lançar quando null (cenário que LunaService.RegistrarInteracaoAsync trata
        // como 422, não um erro de binding/500).
        const string json = """
        {
            "id_tutor": null,
            "ds_canal": "WHATSAPP",
            "ds_direcao": "INBOUND",
            "ds_conteudo": "oi",
            "dt_recebimento": "2026-08-08T10:00:00",
            "ds_metadados": null
        }
        """;

        // Act
        var dto = JsonSerializer.Deserialize<InteractionRequestDto>(json, Options);

        // Assert
        dto.Should().NotBeNull();
        dto!.IdTutor.Should().BeNull();
    }

    [Fact]
    public void InteractionResponseDto_Serializa_ComoIdInteracaoSnakeCase()
    {
        // Arrange
        var dto = new InteractionResponseDto { IdInteracao = 123 };

        // Act
        var json = JsonSerializer.Serialize(dto, Options);
        using var doc = JsonDocument.Parse(json);

        // Assert
        // kura_client.py:97 lê resp.json()["id_interacao"] direto — sem essa chave
        // exata, KeyError explode do lado Luna.
        doc.RootElement.TryGetProperty("id_interacao", out var prop).Should().BeTrue();
        prop.GetInt64().Should().Be(123);
    }

    [Fact]
    public void TriageRequestDto_Deserializa_PayloadRealDoPydantic()
    {
        // Arrange
        const string json = """
        {
            "id_interacao": 100,
            "id_tutor": 7,
            "sintomas": ["vomito", "letargia"],
            "ds_urgencia": "ALTA",
            "nr_score": 87,
            "ds_recomendacao": "Levar ao veterinário em até 2 horas"
        }
        """;

        // Act
        var dto = JsonSerializer.Deserialize<TriageRequestDto>(json, Options);

        // Assert
        dto.Should().NotBeNull();
        dto!.IdInteracao.Should().Be(100);
        dto.IdTutor.Should().Be(7);
        dto.Sintomas.Should().BeEquivalentTo(["vomito", "letargia"]);
        dto.DsUrgencia.Should().Be("ALTA");
        dto.NrScore.Should().Be(87);
        dto.DsRecomendacao.Should().Be("Levar ao veterinário em até 2 horas");
    }

    [Fact]
    public void TriageResponseDto_Serializa_ComoIdTriagemSnakeCase()
    {
        // Arrange
        var dto = new TriageResponseDto { IdTriagem = 456 };

        // Act
        var json = JsonSerializer.Serialize(dto, Options);
        using var doc = JsonDocument.Parse(json);

        // Assert
        // kura_client.py:113 lê resp.json()["id_triagem"] direto.
        doc.RootElement.TryGetProperty("id_triagem", out var prop).Should().BeTrue();
        prop.GetInt64().Should().Be(456);
    }
}
