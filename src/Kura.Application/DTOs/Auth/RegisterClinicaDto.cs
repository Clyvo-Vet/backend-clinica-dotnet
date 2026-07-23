namespace Kura.Application.DTOs.Auth;

public sealed class RegisterClinicaDto
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
    public string DsEmailAcesso { get; set; } = string.Empty;
    public string DsSenha { get; set; } = string.Empty;
    public string NmVeterinarioAdmin { get; set; } = string.Empty;
    public string NrCRMV { get; set; } = string.Empty;
}
