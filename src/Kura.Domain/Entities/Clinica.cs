namespace Kura.Domain.Entities;

public class Clinica : EntidadeBase
{
    public string NmClinica { get; set; } = string.Empty;
    public string NrCnpj { get; set; } = string.Empty;
    public string? NmRazaoSocial { get; set; }
    public string DsEndereco { get; set; } = string.Empty;
    public string NmCidade { get; set; } = string.Empty;
    public string SgUf { get; set; } = string.Empty;
    public string NrCep { get; set; } = string.Empty;
    public string? NrTelefone { get; set; }
    public string DsEmail { get; set; } = string.Empty;
    public DateTime DtCadastro { get; set; } = DateTime.UtcNow;
    public string DsEmailAcesso { get; set; } = string.Empty;
    public string DsSenhaHash { get; set; } = string.Empty;
    public ICollection<Veterinario> Veterinarios { get; set; } = [];
}
