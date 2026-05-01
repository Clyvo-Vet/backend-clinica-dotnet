namespace Kura.Application.DTOs.Pet;

using Kura.Application.DTOs.Tutor;

public sealed class PetResponseDto
{
    public long Id { get; init; }
    public string NmPet { get; init; } = string.Empty;
    public long IdEspecie { get; init; }
    public string NmEspecie { get; init; } = string.Empty;
    public long IdRaca { get; init; }
    public string NmRaca { get; init; } = string.Empty;
    public long? IdVeterinarioResp { get; init; }
    public DateTime DtNascimento { get; init; }
    public char SgSexo { get; init; }
    public char SgPorte { get; init; }
    public char StAtiva { get; init; }
    public IReadOnlyList<TutorVinculoDto> Tutores { get; init; } = [];
}

public sealed class TutorVinculoDto
{
    public long IdTutor { get; init; }
    public string NmTutor { get; init; } = string.Empty;
    public string DsVinculo { get; init; } = string.Empty;
    public char StPrincipal { get; init; }
}
