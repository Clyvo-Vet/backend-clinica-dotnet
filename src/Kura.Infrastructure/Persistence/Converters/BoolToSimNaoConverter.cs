using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kura.Infrastructure.Persistence.Converters;

/// <summary>
/// Converte bool ↔ "S"/"N" para compatibilidade com Oracle 19c, onde
/// colunas booleanas são CHAR(1) CHECK IN ('S','N') por convenção do schema Flyway.
/// </summary>
public class BoolToSimNaoConverter : ValueConverter<bool, string>
{
    public BoolToSimNaoConverter()
        : base(
            v => v ? "S" : "N",
            v => v == "S")
    {
    }
}
