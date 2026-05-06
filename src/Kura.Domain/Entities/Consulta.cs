namespace Kura.Domain.Entities;

public class Consulta : EntidadeBase
{
    public long IdEventoClinico { get; set; }
    public string DsMotivo { get; set; } = string.Empty;
    public string? DsAnamnese { get; set; }
    public string? DsExameFisico { get; set; }
    public string? DsDiagnostico { get; set; }
    public DateTime DtConsulta { get; set; }

    public EventoClinico EventoClinico { get; set; } = null!;
}
