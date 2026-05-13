namespace Kura.Application.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kura.Application.DTOs.Auth;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

public sealed class AuthService : IAuthService
{
    private readonly IClinicaRepository _clinicaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public AuthService(
        IClinicaRepository clinicaRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _clinicaRepository = clinicaRepository;
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

        var expiresAt = DateTime.UtcNow.AddHours(
            _configuration.GetValue<int>("Jwt:ExpiryHours", 8));

        var token = GenerateToken(clinica, expiresAt);

        return new TokenResponseDto
        {
            AccessToken = token,
            ExpiresAt = expiresAt
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
            DsEndereco = dto.DsEndereco,
            NrTelefone = dto.NrTelefone,
            DsEmail = dto.DsEmail,
            DsEmailAcesso = dto.DsEmailAcesso,
            DsSenhaHash = senhaHash,
            StAtiva = true,
            DtCriacao = DateTime.UtcNow
        };

        await _clinicaRepository.AddAsync(clinica);
        await _unitOfWork.CommitAsync();

        return new RegisterClinicaResponseDto
        {
            IdClinica = clinica.Id,
            NmClinica = clinica.NmClinica,
            DsEmailAcesso = clinica.DsEmailAcesso,
            DtCriacao = clinica.DtCriacao
        };
    }

    private string GenerateToken(Clinica clinica, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("clinicaId", clinica.Id.ToString()),
            new Claim("veterinarioId", string.Empty),
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
}
