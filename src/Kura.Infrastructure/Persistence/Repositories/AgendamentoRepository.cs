namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class AgendamentoRepository : IAgendamentoRepository
{
    private readonly KuraDbContext _context;

    public AgendamentoRepository(KuraDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Agendamento>> GetProximosDoDiaAsync(long idClinica, DateTime data, int limite)
    {
        return await _context.Agendamentos
            .Where(a => a.IdClinica == idClinica
                && a.DtAgendamento.Date == data.Date
                && a.DtAgendamento >= DateTime.UtcNow)
            .OrderBy(a => a.DtAgendamento)
            .Take(limite)
            .ToListAsync();
    }

    public async Task<IEnumerable<Agendamento>> GetRecentesAsync(long idClinica, DateTime referencia, int limite)
    {
        return await _context.Agendamentos
            .Where(a => a.IdClinica == idClinica && a.DtAgendamento < referencia)
            .OrderByDescending(a => a.DtAgendamento)
            .Take(limite)
            .ToListAsync();
    }

    public Task<Agendamento?> GetByIdAsync(long id, long idClinica)
        => _context.Agendamentos
            .FirstOrDefaultAsync(a => a.Id == id && a.IdClinica == idClinica);

    public Task<int> ContarTeleorientacoesHojeAsync(long idClinica, DateTime data)
        => _context.Agendamentos
            .Where(a => a.IdClinica == idClinica
                && a.StTeleconsulta
                && a.DtInicioSessao != null
                && a.DtInicioSessao!.Value.Date == data.Date)
            .CountAsync();

    public void Update(Agendamento agendamento)
        => _context.Agendamentos.Update(agendamento);
}
