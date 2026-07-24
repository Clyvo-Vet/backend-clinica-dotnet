namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IConsentimentoRepository
{
    Task<Consentimento?> GetMaisRecenteAsync(long idTutor, string dsTipo);
}
