namespace Kura.Application.Services;

using Kura.Application.DTOs.Veterinario;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

/// <summary>
/// CRUD de <see cref="Veterinario"/>.
///
/// <para>
/// 🔴 <b>FD-05 (ciclo FIN): a clínica do veterinário criado sai do JWT, e de mais lugar
/// nenhum.</b> Até esta task <c>CreateAsync</c> gravava <c>dto.IdClinica</c> — valor do
/// <b>corpo</b> da requisição — sem comparar com o <c>clinicaId</c> do token, e
/// <c>VeterinariosController</c> exige apenas <c>[Authorize]</c>: qualquer clínica autenticada
/// criava veterinário dentro de outra. Medido sobre HTTP real antes do fix, com token da
/// clínica 1 e <c>idClinica: 2</c> no corpo, o recurso nascia na clínica 2
/// (<c>VeterinariosTenantHttpTests</c>).
/// </para>
///
/// <para>
/// A correção removeu o campo do <see cref="VeterinarioCreateDto"/> em vez de compará-lo com o
/// token — o argumento completo está na documentação daquele DTO.
/// </para>
///
/// <para>
/// ⚠️ <b>O que este service NÃO garante sozinho, declarado.</b> <c>UpdateAsync</c>,
/// <c>SoftDeleteAsync</c>, <c>GetByIdAsync</c> e <c>GetAllAsync</c> não têm comparação de
/// tenant escrita aqui: o isolamento deles é <b>ambiente</b>, vindo do query filter de
/// <c>Veterinario</c> em <c>KuraDbContext.ApplyTenantFilters</c> (<c>GetByIdAsync</c> é
/// <c>DbSet.FindAsync</c> e <b>aplica</b> os filtros — medido na FD-04, MED-2). Esse filtro
/// <b>desliga inteiro</b>, em vez de negar, quando <c>IdClinicaFiltro</c> é <c>null</c>; sobre
/// HTTP isso é hoje inalcançável, porque <c>[Authorize]</c> exige um JWT e
/// <c>AuthService.GenerateToken</c> emite <c>clinicaId</c> incondicionalmente. Um endpoint
/// futuro que chame estes métodos por API key (como fazem os da Luna) herdaria o vazamento sem
/// que nada aqui quebrasse. O comportamento de hoje está travado em
/// <c>VeterinariosTenantHttpTests</c>.
/// </para>
///
/// <para>
/// ⚠️ <c>GetByClinicaAsync</c> ainda recebe a clínica por <b>query string</b>
/// (<c>GET /api/v1/veterinarios?clinicaId=…</c>). É leitura, e o query filter a esvazia para
/// clínica alheia — medido, não deduzido, em
/// <c>Listar_veterinarios_filtrando_por_outra_clinica_devolve_lista_vazia</c>. Fica registrado
/// como assimetria: a escrita não aceita mais clínica do cliente, a leitura ainda aceita.
/// </para>
/// </summary>
public sealed class VeterinarioService : IVeterinarioService
{
    private readonly IVeterinarioRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly IClinicaContext _clinicaContext;

    public VeterinarioService(
        IVeterinarioRepository repository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext)
    {
        _repository = repository;
        _uow = uow;
        _clinicaContext = clinicaContext;
    }

    public async Task<IEnumerable<VeterinarioResponseDto>> GetAllAsync()
    {
        var veterinarios = await _repository.GetAllAsync();
        return veterinarios.Select(ToResponse);
    }

    public async Task<VeterinarioResponseDto> GetByIdAsync(long id)
    {
        var veterinario = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Veterinario", id);
        return ToResponse(veterinario);
    }

    public async Task<IEnumerable<VeterinarioResponseDto>> GetByClinicaAsync(long idClinica)
    {
        var veterinarios = await _repository.GetAllByClinicaIdAsync(idClinica);
        return veterinarios.Select(ToResponse);
    }

    public async Task<VeterinarioResponseDto> CreateAsync(VeterinarioCreateDto dto)
    {
        var veterinario = new Veterinario
        {
            // 🔴 FD-05: do JWT, nunca do corpo. Ver a documentação desta classe e do DTO.
            IdClinica = _clinicaContext.IdClinica,
            NmVeterinario = dto.NmVeterinario,
            NrCrmv = dto.NrCrmv,
            DsEmail = dto.DsEmail,
            NrTelefone = dto.NrTelefone
        };
        await _repository.AddAsync(veterinario);
        await _uow.CommitAsync();
        return ToResponse(veterinario);
    }

    public async Task<VeterinarioResponseDto> UpdateAsync(long id, VeterinarioUpdateDto dto)
    {
        var veterinario = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Veterinario", id);

        veterinario.NmVeterinario = dto.NmVeterinario;
        veterinario.NrCrmv = dto.NrCrmv;
        veterinario.DsEmail = dto.DsEmail;
        veterinario.NrTelefone = dto.NrTelefone;

        _repository.Update(veterinario);
        await _uow.CommitAsync();
        return ToResponse(veterinario);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var veterinario = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Veterinario", id);
        _repository.SoftDelete(veterinario);
        await _uow.CommitAsync();
    }

    private static VeterinarioResponseDto ToResponse(Veterinario v) => new()
    {
        Id = v.Id,
        IdClinica = v.IdClinica,
        NmVeterinario = v.NmVeterinario,
        NrCrmv = v.NrCrmv,
        DsEmail = v.DsEmail,
        NrTelefone = v.NrTelefone,
        StAtiva = v.StAtiva
    };
}
