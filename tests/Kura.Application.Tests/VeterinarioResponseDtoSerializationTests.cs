using System.Text.Json;
using FluentAssertions;
using Kura.Application.DTOs.Veterinario;

namespace Kura.Application.Tests;

/// <summary>
/// Garante que a correção de casing do CRMV (nrCrmv → nrCRMV) realmente se
/// reflete na serialização JSON consumida pelo app mobile, e não apenas no
/// nome da propriedade em C#.
/// </summary>
public class VeterinarioResponseDtoSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Serializacao_UsaNrCRMV_ComCapitalizacaoCorreta()
    {
        var dto = new VeterinarioResponseDto
        {
            Id = 1,
            IdClinica = 2,
            NmVeterinario = "Dr. Ana",
            NrCrmv = "SP-123456",
            DsEmail = "ana@clinic.com",
            NrTelefone = "11999999999",
            StAtiva = true
        };

        var json = JsonSerializer.Serialize(dto, Options);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("nrCRMV", out var nrCrmvProp).Should().BeTrue(
            "o app mobile espera a chave \"nrCRMV\" (CRMV maiúsculo)");
        nrCrmvProp.GetString().Should().Be("SP-123456");

        doc.RootElement.TryGetProperty("nrCrmv", out _).Should().BeFalse(
            "a política default de camelCase não deve mais vazar como \"nrCrmv\"");
    }

    [Fact]
    public void Deserializacao_AceitaNrCRMV_ERoundTripPreservaValor()
    {
        const string json = """
        {
            "id": 1,
            "idClinica": 2,
            "nmVeterinario": "Dr. Ana",
            "nrCRMV": "SP-654321",
            "dsEmail": "ana@clinic.com",
            "nrTelefone": "11999999999",
            "stAtiva": true
        }
        """;

        var dto = JsonSerializer.Deserialize<VeterinarioResponseDto>(json, Options);

        dto.Should().NotBeNull();
        dto!.NrCrmv.Should().Be("SP-654321");
    }
}
