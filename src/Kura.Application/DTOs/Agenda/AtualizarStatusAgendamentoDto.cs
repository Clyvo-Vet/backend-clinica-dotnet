namespace Kura.Application.DTOs.Agenda;

public sealed class AtualizarStatusAgendamentoDto
{
    public string DsStatus { get; init; } = string.Empty;
    public long NrVersion { get; init; }
    public string? DsObservacao { get; init; }
}
