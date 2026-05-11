namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IInviteTutorRepository : IRepository<InviteTutor>
{
    Task<InviteTutor?> GetByTokenAsync(Guid token);
    Task<bool> ExisteInviteAtivoAsync(long idTutor);
}
