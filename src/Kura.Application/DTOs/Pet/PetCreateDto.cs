namespace Kura.Application.DTOs.Pet;

public sealed class PetCreateDto
{
    public long IdEspecie { get; init; }
    public long IdRaca { get; init; }
    public long? IdVeterinarioResp { get; init; }
    public string NmPet { get; init; } = string.Empty;
    public DateTime DtNascimento { get; init; }
    public char SgSexo { get; init; }
    public char SgPorte { get; init; }
    public long TutorId { get; init; }
    public char StPrincipal { get; init; } = 'S';
    public string DsVinculo { get; init; } = "PROPRIETARIO";
}
