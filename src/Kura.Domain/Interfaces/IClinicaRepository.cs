namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IClinicaRepository : IRepository<Clinica>
{
    Task<bool> ExisteComCnpjAsync(string cnpj);
    Task<bool> ExisteComEmailAcessoAsync(string email);
}
