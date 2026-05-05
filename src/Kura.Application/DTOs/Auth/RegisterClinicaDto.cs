namespace Kura.Application.DTOs.Auth;

public sealed class RegisterClinicaDto
{
    public string NmClinica { get; set; } = string.Empty;
    public string NrCnpj { get; set; } = string.Empty;
    public string DsEndereco { get; set; } = string.Empty;
    public string NrTelefone { get; set; } = string.Empty;
    public string DsEmail { get; set; } = string.Empty;
    public string DsEmailAcesso { get; set; } = string.Empty;
    public string DsSenha { get; set; } = string.Empty;
}
