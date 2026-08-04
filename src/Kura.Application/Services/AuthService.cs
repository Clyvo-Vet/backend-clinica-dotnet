namespace Kura.Application.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kura.Application.DTOs.Auth;
using Kura.Application.DTOs.Veterinario;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

public sealed class AuthService : IAuthService
{
    private readonly IClinicaRepository _clinicaRepository;
    private readonly IVeterinarioRepository _veterinarioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public AuthService(
        IClinicaRepository clinicaRepository,
        IVeterinarioRepository veterinarioRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _clinicaRepository = clinicaRepository;
        _veterinarioRepository = veterinarioRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task<TokenResponseDto> LoginAsync(LoginDto dto)
    {
        var clinicas = await _clinicaRepository.FindAsync(c => c.DsEmailAcesso == dto.DsEmail);
        var clinica = clinicas.FirstOrDefault()
            ?? throw new RegraDeNegocioException("Email ou senha inválidos.");

        if (!BCrypt.Net.BCrypt.Verify(dto.DsSenha, clinica.DsSenhaHash))
            throw new RegraDeNegocioException("Email ou senha inválidos.");

        var veterinarios = await _veterinarioRepository.GetAllByClinicaIdAsync(clinica.Id);
        var veterinario = veterinarios.FirstOrDefault(v => v.DsEmail == clinica.DsEmailAcesso)
            ?? veterinarios.OrderBy(v => v.Id).FirstOrDefault()
            ?? throw new RegraDeNegocioException("Clínica sem veterinário responsável cadastrado.");

        var expiresAt = DateTime.UtcNow.AddHours(
            _configuration.GetValue<int>("Jwt:ExpiryHours", 8));

        var token = GenerateToken(clinica, veterinario.Id, expiresAt);

        return new TokenResponseDto
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            Usuario = ToVeterinarioResponse(veterinario)
        };
    }

    public async Task<RegisterClinicaResponseDto> RegisterClinicaAsync(RegisterClinicaDto dto)
    {
        if (await _clinicaRepository.ExisteComCnpjAsync(dto.NrCnpj))
            throw new RegraDeNegocioException("Já existe uma clínica cadastrada com este CNPJ.");

        if (await _clinicaRepository.ExisteComEmailAcessoAsync(dto.DsEmailAcesso))
            throw new RegraDeNegocioException("Já existe uma clínica cadastrada com este e-mail de acesso.");

        var senhaHash = BCrypt.Net.BCrypt.HashPassword(dto.DsSenha);

        var clinica = new Clinica
        {
            NmClinica = dto.NmClinica,
            NrCnpj = dto.NrCnpj,
            NmRazaoSocial = dto.NmRazaoSocial,
            DsEndereco = dto.DsEndereco,
            NmCidade = dto.NmCidade,
            SgUf = dto.SgUf,
            NrCep = dto.NrCep,
            NrTelefone = dto.NrTelefone,
            DsEmail = dto.DsEmail,
            DsEmailAcesso = dto.DsEmailAcesso,
            DsSenhaHash = senhaHash,
            StAtiva = true,
            DtCadastro = DateTime.UtcNow
        };

        // TASK-30: Clinica e Veterinario precisam ser atômicas. Cada uma exige seu
        // próprio SaveChangesAsync (o Id da Clinica só existe depois do primeiro
        // commit, e o Veterinario depende dele) — por isso envolvemos as duas
        // escritas numa transação explícita: se a segunda falhar, o rollback desfaz
        // a primeira, evitando uma clínica órfã (sem veterinário) com o e-mail
        // permanentemente "tomado".
        Veterinario veterinario;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _clinicaRepository.AddAsync(clinica);
            await _unitOfWork.CommitAsync();

            veterinario = new Veterinario
            {
                IdClinica = clinica.Id,
                NmVeterinario = dto.NmVeterinarioAdmin,
                NrCrmv = dto.NrCRMV,
                DsEmail = dto.DsEmailAcesso,
                NrTelefone = dto.NrTelefone ?? string.Empty
            };

            await _veterinarioRepository.AddAsync(veterinario);
            await _unitOfWork.CommitAsync();

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        var expiresAt = DateTime.UtcNow.AddHours(
            _configuration.GetValue<int>("Jwt:ExpiryHours", 8));

        var token = GenerateToken(clinica, veterinario.Id, expiresAt);

        return new RegisterClinicaResponseDto
        {
            IdClinica = clinica.Id,
            NmClinica = clinica.NmClinica,
            DsEmailAcesso = clinica.DsEmailAcesso,
            DtCriacao = clinica.DtCriacao,
            IdVeterinarioAdmin = veterinario.Id,
            AccessToken = token,
            ExpiresAt = expiresAt,
            Usuario = ToVeterinarioResponse(veterinario)
        };
    }

    private string GenerateToken(Clinica clinica, long veterinarioId, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("clinicaId", clinica.Id.ToString()),
            new Claim("veterinarioId", veterinarioId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, clinica.DsEmailAcesso)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static VeterinarioResponseDto ToVeterinarioResponse(Veterinario v) => new()
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
