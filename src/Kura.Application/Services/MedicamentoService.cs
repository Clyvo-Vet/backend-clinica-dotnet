namespace Kura.Application.Services;

using Kura.Application.DTOs.Common;
using Kura.Application.DTOs.Medicamento;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class MedicamentoService : IMedicamentoService
{
    private readonly IRepository<Medicamento> _repository;
    private readonly IUnitOfWork _uow;

    public MedicamentoService(IRepository<Medicamento> repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow = uow;
    }

    public async Task<PagedResultDto<MedicamentoResponseDto>> ListarAsync(string? busca, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IEnumerable<Medicamento> medicamentos;
        if (string.IsNullOrWhiteSpace(busca))
            medicamentos = await _repository.GetAllAsync();
        else
            medicamentos = await _repository.FindAsync(
                m => m.NmMedicamento.ToLower().Contains(busca.ToLower()) ||
                     m.DsPrincipioAtivo.ToLower().Contains(busca.ToLower()));

        var list = medicamentos.ToList();
        var total = list.Count;
        var items = list
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToResponse);

        return new PagedResultDto<MedicamentoResponseDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<MedicamentoResponseDto>> SearchAsync(string? busca)
    {
        IEnumerable<Medicamento> medicamentos;
        if (string.IsNullOrWhiteSpace(busca))
            medicamentos = await _repository.GetAllAsync();
        else
            medicamentos = await _repository.FindAsync(
                m => m.NmMedicamento.ToLower().Contains(busca.ToLower()) ||
                     m.DsPrincipioAtivo.ToLower().Contains(busca.ToLower()));
        return medicamentos.Select(ToResponse);
    }

    public async Task<MedicamentoResponseDto> GetByIdAsync(long id)
    {
        var medicamento = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Medicamento", id);
        return ToResponse(medicamento);
    }

    public async Task<MedicamentoResponseDto> CreateAsync(MedicamentoCreateDto dto)
    {
        var medicamento = new Medicamento
        {
            NmMedicamento = dto.NmMedicamento,
            DsPrincipioAtivo = dto.DsPrincipioAtivo,
            DsApresentacao = dto.DsApresentacao
        };
        await _repository.AddAsync(medicamento);
        await _uow.CommitAsync();
        return ToResponse(medicamento);
    }

    public async Task<MedicamentoResponseDto> UpdateAsync(long id, MedicamentoUpdateDto dto)
    {
        var medicamento = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Medicamento", id);

        medicamento.NmMedicamento = dto.NmMedicamento;
        medicamento.DsPrincipioAtivo = dto.DsPrincipioAtivo;
        medicamento.DsApresentacao = dto.DsApresentacao;

        _repository.Update(medicamento);
        await _uow.CommitAsync();
        return ToResponse(medicamento);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var medicamento = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Medicamento", id);
        _repository.SoftDelete(medicamento);
        await _uow.CommitAsync();
    }

    private static MedicamentoResponseDto ToResponse(Medicamento m) => new()
    {
        Id = m.Id,
        NmMedicamento = m.NmMedicamento,
        DsPrincipioAtivo = m.DsPrincipioAtivo,
        DsApresentacao = m.DsApresentacao,
        StAtiva = m.StAtiva
    };
}
