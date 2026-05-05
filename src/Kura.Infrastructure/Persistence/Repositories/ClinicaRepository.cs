namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class ClinicaRepository : Repository<Clinica>, IClinicaRepository
{
    public ClinicaRepository(KuraDbContext context) : base(context)
    {
    }

    public async Task<bool> ExisteComCnpjAsync(string cnpj) =>
        await _dbSet.IgnoreQueryFilters().AnyAsync(c => c.NrCnpj == cnpj);

    public async Task<bool> ExisteComEmailAcessoAsync(string email) =>
        await _dbSet.IgnoreQueryFilters().AnyAsync(c => c.DsEmailAcesso == email);
}
