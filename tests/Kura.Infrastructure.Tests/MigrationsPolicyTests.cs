namespace Kura.Infrastructure.Tests;

using FluentAssertions;

public class MigrationsPolicyTests
{
    [Fact]
    public void PastaMigrations_DeveTerArquivosCSharp_ParaEvidenciaFIAP()
    {
        var migrationsPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "../../../../../src/Kura.Infrastructure/Migrations");

        Directory.Exists(migrationsPath).Should().BeTrue(
            "pasta Migrations deve existir como evidência para a rubrica FIAP");

        var arquivosCs = Directory.GetFiles(migrationsPath, "*.cs");
        arquivosCs.Should().NotBeEmpty(
            "deve ter ao menos uma migration gerada como evidência");
    }
}
