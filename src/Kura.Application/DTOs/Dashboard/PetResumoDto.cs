namespace Kura.Application.DTOs.Dashboard;

public sealed class PetResumoDto
{
    public long Id { get; init; }
    public string NmPet { get; init; } = string.Empty;
    public DateTime UltimoAtendimento { get; init; }
}
