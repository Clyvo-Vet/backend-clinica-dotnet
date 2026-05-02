namespace Kura.Application.Services;

using Kura.Application.DTOs.Pet;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class PetService : IPetService
{
    private readonly IPetRepository _repository;
    private readonly ITutorPetRepository _tutorPetRepository;
    private readonly IRepository<Especie> _especieRepository;
    private readonly IRepository<Raca> _racaRepository;
    private readonly IUnitOfWork _uow;
    private readonly IClinicaContext _clinicaContext;

    public PetService(
        IPetRepository repository,
        ITutorPetRepository tutorPetRepository,
        IRepository<Especie> especieRepository,
        IRepository<Raca> racaRepository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext)
    {
        _repository = repository;
        _tutorPetRepository = tutorPetRepository;
        _especieRepository = especieRepository;
        _racaRepository = racaRepository;
        _uow = uow;
        _clinicaContext = clinicaContext;
    }

    public async Task<IEnumerable<PetResponseDto>> GetByFiltersAsync(long? tutorId, long? especieId, char? porte)
    {
        var pets = await _repository.GetByFiltersAsync(tutorId, especieId, porte);
        var result = new List<PetResponseDto>();
        foreach (var pet in pets)
            result.Add(await BuildResponseAsync(pet));
        return result;
    }

    public async Task<PetResponseDto> GetByIdAsync(long id)
    {
        var pet = await _repository.GetByIdWithTutoresAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Pet", id);
        return await BuildResponseAsync(pet);
    }

    public async Task<PetResponseDto> CreateAsync(PetCreateDto dto)
    {
        var pet = new Pet
        {
            IdClinica = _clinicaContext.IdClinica,
            IdEspecie = dto.IdEspecie,
            IdRaca = dto.IdRaca,
            IdVeterinarioResp = dto.IdVeterinarioResp,
            NmPet = dto.NmPet,
            DtNascimento = dto.DtNascimento,
            SgSexo = dto.SgSexo,
            SgPorte = dto.SgPorte
        };

        // TutorPet via navigation property — EF Core insere Pet primeiro (FK ordering)
        var tutorPet = new TutorPet
        {
            IdTutor = dto.TutorId,
            Pet = pet,
            DsVinculo = dto.DsVinculo,
            StPrincipal = dto.StPrincipal
        };

        await _tutorPetRepository.AddAsync(tutorPet);
        await _uow.CommitAsync();
        return await BuildResponseAsync(pet);
    }

    public async Task<PetResponseDto> UpdateAsync(long id, PetUpdateDto dto)
    {
        var pet = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Pet", id);

        pet.IdVeterinarioResp = dto.IdVeterinarioResp;
        pet.NmPet = dto.NmPet;
        pet.SgSexo = dto.SgSexo;
        pet.SgPorte = dto.SgPorte;

        _repository.Update(pet);
        await _uow.CommitAsync();
        return await BuildResponseAsync(pet);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var pet = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Pet", id);
        _repository.SoftDelete(pet);
        await _uow.CommitAsync();
    }

    public async Task AdicionarTutorAsync(long idPet, AdicionarTutorPetDto dto)
    {
        _ = await _repository.GetByIdAsync(idPet)
            ?? throw new EntidadeNaoEncontradaException("Pet", idPet);

        var jaExiste = await _tutorPetRepository.ExistsAsync(dto.IdTutor, idPet);
        if (jaExiste)
            throw new RegraDeNegocioException("Já existe vínculo ativo entre este tutor e este pet.");

        var tutorPet = new TutorPet
        {
            IdTutor = dto.IdTutor,
            IdPet = idPet,
            DsVinculo = dto.DsVinculo,
            StPrincipal = 'N'
        };

        await _tutorPetRepository.AddAsync(tutorPet);
        await _uow.CommitAsync();
    }

    private async Task<PetResponseDto> BuildResponseAsync(Pet pet)
    {
        var especie = await _especieRepository.GetByIdAsync(pet.IdEspecie);
        var raca = await _racaRepository.GetByIdAsync(pet.IdRaca);
        var vinculos = await _tutorPetRepository.GetByPetIdAsync(pet.Id);

        return new PetResponseDto
        {
            Id = pet.Id,
            NmPet = pet.NmPet,
            IdEspecie = pet.IdEspecie,
            NmEspecie = especie?.NmEspecie ?? string.Empty,
            IdRaca = pet.IdRaca,
            NmRaca = raca?.NmRaca ?? string.Empty,
            IdVeterinarioResp = pet.IdVeterinarioResp,
            DtNascimento = pet.DtNascimento,
            SgSexo = pet.SgSexo,
            SgPorte = pet.SgPorte,
            StAtiva = pet.StAtiva,
            Tutores = vinculos.Select(tp => new TutorVinculoDto
            {
                IdTutor = tp.IdTutor,
                NmTutor = tp.Tutor.NmTutor,
                DsVinculo = tp.DsVinculo,
                StPrincipal = tp.StPrincipal
            }).ToList()
        };
    }
}
