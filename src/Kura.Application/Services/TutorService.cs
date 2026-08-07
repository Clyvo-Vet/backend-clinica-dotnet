namespace Kura.Application.Services;

using Kura.Application.DTOs.Pet;
using Kura.Application.DTOs.Tutor;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class TutorService : ITutorService
{
    private readonly ITutorRepository _repository;
    private readonly ITutorPetRepository _tutorPetRepository;
    private readonly IRepository<Especie> _especieRepository;
    private readonly IRepository<Raca> _racaRepository;
    private readonly IInviteTutorRepository _inviteRepository;
    private readonly IUnitOfWork _uow;
    private readonly IClinicaContext _clinicaContext;

    public TutorService(
        ITutorRepository repository,
        ITutorPetRepository tutorPetRepository,
        IRepository<Especie> especieRepository,
        IRepository<Raca> racaRepository,
        IInviteTutorRepository inviteRepository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext)
    {
        _repository = repository;
        _tutorPetRepository = tutorPetRepository;
        _especieRepository = especieRepository;
        _racaRepository = racaRepository;
        _inviteRepository = inviteRepository;
        _uow = uow;
        _clinicaContext = clinicaContext;
    }

    public async Task<IEnumerable<TutorResponseDto>> SearchAsync(string? busca)
    {
        // TASK-21: idClinica passado explicitamente — defesa em profundidade além do
        // HasQueryFilter global do KuraDbContext (mesmo padrão de AgendaService/PetService).
        var tutores = await _repository.SearchAsync(busca, _clinicaContext.IdClinica);
        return tutores.Select(ToResponse);
    }

    public async Task<TutorResponseDto> GetByIdAsync(long id)
    {
        var tutor = await _repository.GetByIdAsync(id, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);
        return ToResponse(tutor);
    }

    public async Task<IEnumerable<PetResponseDto>> GetPetsAsync(long id)
    {
        _ = await _repository.GetByIdAsync(id, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);

        var vinculos = await _tutorPetRepository.GetByTutorIdAsync(id);
        var result = new List<PetResponseDto>();
        foreach (var vinculo in vinculos)
        {
            var pet = vinculo.Pet;
            var especie = await _especieRepository.GetByIdAsync(pet.IdEspecie);
            var raca = await _racaRepository.GetByIdAsync(pet.IdRaca);
            result.Add(new PetResponseDto
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
                StAtiva = pet.StAtiva
            });
        }
        return result;
    }

    public async Task<TutorComInviteResponseDto> CreateAsync(TutorCreateDto dto, long clinicaId)
    {
        var tutor = new Tutor
        {
            IdClinica = clinicaId,
            NmTutor = dto.NmTutor,
            NrCpf = dto.NrCpf,
            DsEmail = dto.DsEmail,
            // TASK-60: TUTOR.DS_TELEFONE é NOT NULL (V1:91, migration imutável) e o Oracle trata
            // VARCHAR2 vazio como NULL. TutorCreateValidator só valida NmTutor/NrCpf/DsEmail,
            // nunca teve regra NotEmpty() para NrTelefone — sem este coalesce, um payload sem
            // esse campo estoura ORA-01400 (500) no INSERT. Mesmo padrão da TASK-56: a restrição
            // de armazenamento se resolve aqui, não como regra de negócio no validator.
            NrTelefone = string.IsNullOrWhiteSpace(dto.NrTelefone)
                ? "Não informado"
                : dto.NrTelefone,
            StAvisoPrivacidade = "S",
            DtAvisoPrivacidade = DateTime.UtcNow,
            DsVersaoAviso = "v1.0"
        };
        await _repository.AddAsync(tutor);

        var invite = new InviteTutor
        {
            Tutor = tutor,
            NrToken = Guid.NewGuid(),
            DtExpiracao = tutor.DtCriacao.AddDays(7),
            DsCanal = dto.DsCanalConvite,
        };
        await _inviteRepository.AddAsync(invite);

        await _uow.CommitAsync();
        return ToComInviteResponse(tutor, invite);
    }

    public async Task<TutorResponseDto> UpdateAsync(long id, TutorUpdateDto dto)
    {
        var tutor = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);

        tutor.NmTutor = dto.NmTutor;
        tutor.NrCpf = dto.NrCpf;
        tutor.DsEmail = dto.DsEmail;
        // TASK-60: mesmo coalesce de CreateAsync — TutorUpdateValidator também nunca teve regra
        // NotEmpty() para NrTelefone.
        tutor.NrTelefone = string.IsNullOrWhiteSpace(dto.NrTelefone)
            ? "Não informado"
            : dto.NrTelefone;

        _repository.Update(tutor);
        await _uow.CommitAsync();
        return ToResponse(tutor);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var tutor = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);
        _repository.SoftDelete(tutor);
        await _uow.CommitAsync();
    }

    private static TutorResponseDto ToResponse(Tutor t) => new()
    {
        Id = t.Id,
        NmTutor = t.NmTutor,
        NrCpf = t.NrCpf,
        DsEmail = t.DsEmail,
        NrTelefone = t.NrTelefone,
        StAtiva = t.StAtiva
    };

    private static TutorComInviteResponseDto ToComInviteResponse(Tutor t, InviteTutor i) => new()
    {
        Id = t.Id,
        NmTutor = t.NmTutor,
        NrCpf = t.NrCpf,
        DsEmail = t.DsEmail,
        NrTelefone = t.NrTelefone,
        StAtiva = t.StAtiva,
        Invite = new InviteTutorResponseDto
        {
            Id = i.Id,
            NrToken = i.NrToken,
            DtExpiracao = i.DtExpiracao,
            DsCanal = i.DsCanal,
            StUtilizado = i.StUtilizado
        }
    };
}
