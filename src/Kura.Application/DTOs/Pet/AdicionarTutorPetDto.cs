namespace Kura.Application.DTOs.Pet;

public sealed class AdicionarTutorPetDto
{
    public long IdTutor { get; init; }
    public string DsVinculo { get; init; } = "CUIDADOR";
}
