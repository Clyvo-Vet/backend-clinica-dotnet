using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Kura.Application.DTOs.Auth;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Kura.Application.Tests;

public class AuthServiceTests
{
    private readonly Mock<IClinicaRepository> _repoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly IConfiguration _config;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "supersecretkey12345678901234567890123456789012",
            ["Jwt:Issuer"] = "kura-api",
            ["Jwt:Audience"] = "kura-client",
            ["Jwt:ExpiryHours"] = "8"
        };
        _config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        _sut = new AuthService(_repoMock.Object, _vetRepoMock.Object, _uowMock.Object, _config);
    }

    private static string GetClaim(string jwt, string claimType) =>
        new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims
            .FirstOrDefault(c => c.Type == claimType)?.Value ?? string.Empty;

    // ── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_EmailNotFound_ThrowsRegraDeNegocio()
    {
        _repoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Clinica, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<Clinica>());

        var act = () => _sut.LoginAsync(new LoginDto { DsEmail = "x@x.com", DsSenha = "pass" });

        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Email ou senha inválidos.");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsRegraDeNegocio()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("correct");
        var clinica = new Clinica { Id = 1, DsEmailAcesso = "a@a.com", DsSenhaHash = hash, StAtiva = true };

        _repoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Clinica, bool>>>()))
            .ReturnsAsync(new[] { clinica });

        var act = () => _sut.LoginAsync(new LoginDto { DsEmail = "a@a.com", DsSenha = "wrong" });

        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Email ou senha inválidos.");
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("secret");
        var clinica = new Clinica { Id = 5, DsEmailAcesso = "vet@clinic.com", DsSenhaHash = hash, StAtiva = true };
        var veterinario = new Veterinario { Id = 42, IdClinica = 5, NmVeterinario = "Dr. Ana", NrCrmv = "SP-123", DsEmail = "vet@clinic.com", NrTelefone = "11999999999" };

        _repoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Clinica, bool>>>()))
            .ReturnsAsync(new[] { clinica });
        _vetRepoMock.Setup(r => r.GetAllByClinicaIdAsync(5))
            .ReturnsAsync(new[] { veterinario });

        var result = await _sut.LoginAsync(new LoginDto { DsEmail = "vet@clinic.com", DsSenha = "secret" });

        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.Usuario.Should().NotBeNull();
        result.Usuario.Id.Should().Be(42);
        result.Usuario.NmVeterinario.Should().Be("Dr. Ana");
        result.Usuario.NrCrmv.Should().Be("SP-123");

        GetClaim(result.AccessToken, "veterinarioId").Should().Be("42");
    }

    [Fact]
    public async Task LoginAsync_ClinicaSemVeterinario_LancaRegraDeNegocioException()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("secret");
        var clinica = new Clinica { Id = 7, DsEmailAcesso = "semvet@clinic.com", DsSenhaHash = hash, StAtiva = true };

        _repoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Clinica, bool>>>()))
            .ReturnsAsync(new[] { clinica });
        _vetRepoMock.Setup(r => r.GetAllByClinicaIdAsync(7))
            .ReturnsAsync(Enumerable.Empty<Veterinario>());

        var act = () => _sut.LoginAsync(new LoginDto { DsEmail = "semvet@clinic.com", DsSenha = "secret" });

        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Clínica sem veterinário responsável cadastrado.");
    }

    [Fact]
    public async Task LoginAsync_SemVetComEmailIgual_UsaPrimeiroVeterinarioOrdenadoPorId()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("secret");
        var clinica = new Clinica { Id = 9, DsEmailAcesso = "acesso@clinic.com", DsSenhaHash = hash, StAtiva = true };
        var vetOutro = new Veterinario { Id = 20, IdClinica = 9, NmVeterinario = "Dr. Outro", NrCrmv = "1", DsEmail = "outro@clinic.com" };
        var vetPrimeiro = new Veterinario { Id = 10, IdClinica = 9, NmVeterinario = "Dr. Primeiro", NrCrmv = "2", DsEmail = "primeiro@clinic.com" };

        _repoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Clinica, bool>>>()))
            .ReturnsAsync(new[] { clinica });
        _vetRepoMock.Setup(r => r.GetAllByClinicaIdAsync(9))
            .ReturnsAsync(new[] { vetOutro, vetPrimeiro });

        var result = await _sut.LoginAsync(new LoginDto { DsEmail = "acesso@clinic.com", DsSenha = "secret" });

        result.Usuario.Id.Should().Be(10);
        GetClaim(result.AccessToken, "veterinarioId").Should().Be("10");
    }

    // ── RegisterClinica ──────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterClinicaAsync_ValidDto_RetornaResponseComIdPreenchido()
    {
        _repoMock.Setup(r => r.ExisteComCnpjAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.ExisteComEmailAcessoAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Clinica>())).Returns(Task.CompletedTask);
        _vetRepoMock.Setup(r => r.AddAsync(It.IsAny<Veterinario>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new RegisterClinicaDto
        {
            NmClinica = "Clínica Teste",
            NrCnpj = "12.345.678/0001-99",
            DsEndereco = "Rua A, 1",
            NrTelefone = "(11) 99999-9999",
            DsEmail = "contato@teste.com",
            DsEmailAcesso = "admin@teste.com",
            DsSenha = "Senha@2026",
            NmVeterinarioAdmin = "Dr. Admin",
            NrCRMV = "SP-000111"
        };

        var result = await _sut.RegisterClinicaAsync(dto);

        result.Should().NotBeNull();
        result.NmClinica.Should().Be("Clínica Teste");
        result.DsEmailAcesso.Should().Be("admin@teste.com");
    }

    [Fact]
    public async Task RegisterClinicaAsync_ValidDto_CriaVeterinarioERetornaTokenEUsuario()
    {
        Veterinario? veterinarioSalvo = null;

        _repoMock.Setup(r => r.ExisteComCnpjAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.ExisteComEmailAcessoAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Clinica>()))
            .Callback<Clinica>(c => c.Id = 100)
            .Returns(Task.CompletedTask);
        _vetRepoMock.Setup(r => r.AddAsync(It.IsAny<Veterinario>()))
            .Callback<Veterinario>(v => { v.Id = 200; veterinarioSalvo = v; })
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new RegisterClinicaDto
        {
            NmClinica = "Clínica Teste",
            NrCnpj = "12.345.678/0001-99",
            DsEndereco = "Rua A, 1",
            NrTelefone = "(11) 99999-9999",
            DsEmail = "contato@teste.com",
            DsEmailAcesso = "admin@teste.com",
            DsSenha = "Senha@2026",
            NmVeterinarioAdmin = "Dr. Admin",
            NrCRMV = "SP-000111"
        };

        var result = await _sut.RegisterClinicaAsync(dto);

        veterinarioSalvo.Should().NotBeNull();
        veterinarioSalvo!.IdClinica.Should().Be(100);
        veterinarioSalvo.NmVeterinario.Should().Be("Dr. Admin");
        veterinarioSalvo.NrCrmv.Should().Be("SP-000111");
        veterinarioSalvo.DsEmail.Should().Be("admin@teste.com");
        veterinarioSalvo.NrTelefone.Should().Be("(11) 99999-9999");

        result.IdVeterinarioAdmin.Should().Be(200);
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.Usuario.Should().NotBeNull();
        result.Usuario.Id.Should().Be(200);
        result.Usuario.NmVeterinario.Should().Be("Dr. Admin");
        result.Usuario.NrCrmv.Should().Be("SP-000111");

        GetClaim(result.AccessToken, "veterinarioId").Should().Be("200");
    }

    [Fact]
    public async Task RegisterClinicaAsync_SemTelefone_NaoAplicaFallbackParaStringVazia()
    {
        // TASK-36 (E-4): dto.NrTelefone ?? string.Empty foi removido — Oracle trata
        // VARCHAR2 vazio como NULL na escrita de qualquer forma, então a "garantia"
        // de string.Empty era falsa e mascarava um NULL real. O comportamento correto
        // é propagar null e deixar a coluna (NULLABLE no schema físico) refletir isso.
        Veterinario? veterinarioSalvo = null;

        _repoMock.Setup(r => r.ExisteComCnpjAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.ExisteComEmailAcessoAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Clinica>()))
            .Callback<Clinica>(c => c.Id = 300)
            .Returns(Task.CompletedTask);
        _vetRepoMock.Setup(r => r.AddAsync(It.IsAny<Veterinario>()))
            .Callback<Veterinario>(v => { v.Id = 301; veterinarioSalvo = v; })
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new RegisterClinicaDto
        {
            NmClinica = "Clínica Sem Telefone",
            NrCnpj = "12.345.678/0001-99",
            DsEndereco = "Rua B, 2",
            NrTelefone = null,
            DsEmail = "contato@semtelefone.com",
            DsEmailAcesso = "admin@semtelefone.com",
            DsSenha = "Senha@2026",
            NmVeterinarioAdmin = "Dr. Sem Telefone",
            NrCRMV = "SP-000222"
        };

        var result = await _sut.RegisterClinicaAsync(dto);

        veterinarioSalvo.Should().NotBeNull();
        veterinarioSalvo!.NrTelefone.Should().BeNull("null é o valor correto para telefone não informado, não \"\"");
        result.Usuario.NrTelefone.Should().BeNull("a resposta HTTP não deve mascarar o NULL como string vazia");
    }

    [Fact]
    public async Task RegisterClinicaAsync_CnpjDuplicado_LancaRegraDeNegocioException()
    {
        _repoMock.Setup(r => r.ExisteComCnpjAsync("12.345.678/0001-99")).ReturnsAsync(true);

        var dto = new RegisterClinicaDto
        {
            NmClinica = "Clínica Teste",
            NrCnpj = "12.345.678/0001-99",
            DsEmailAcesso = "admin@teste.com",
            DsSenha = "Senha@2026"
        };

        var act = () => _sut.RegisterClinicaAsync(dto);

        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Já existe uma clínica cadastrada com este CNPJ.");
    }

    [Fact]
    public async Task RegisterClinicaAsync_EmailDuplicado_LancaRegraDeNegocioException()
    {
        _repoMock.Setup(r => r.ExisteComCnpjAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.ExisteComEmailAcessoAsync("admin@teste.com")).ReturnsAsync(true);

        var dto = new RegisterClinicaDto
        {
            NmClinica = "Clínica Teste",
            NrCnpj = "12.345.678/0001-99",
            DsEmailAcesso = "admin@teste.com",
            DsSenha = "Senha@2026"
        };

        var act = () => _sut.RegisterClinicaAsync(dto);

        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Já existe uma clínica cadastrada com este e-mail de acesso.");
    }

    [Fact]
    public async Task RegisterClinicaAsync_SenhaNaoRetornadaNoResponse()
    {
        var tipo = typeof(RegisterClinicaResponseDto);

        tipo.GetProperty("DsSenhaHash").Should().BeNull("hash nunca deve ser exposto no DTO de resposta");
        tipo.GetProperty("DsSenha").Should().BeNull("senha em texto puro nunca deve ser exposta no DTO de resposta");
    }

    [Fact]
    public async Task RegisterClinicaAsync_SenhaSalvaComoHash_NaoIgualTextoPuro()
    {
        Clinica? clinicaSalva = null;

        _repoMock.Setup(r => r.ExisteComCnpjAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.ExisteComEmailAcessoAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Clinica>()))
            .Callback<Clinica>(c => clinicaSalva = c)
            .Returns(Task.CompletedTask);
        _vetRepoMock.Setup(r => r.AddAsync(It.IsAny<Veterinario>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new RegisterClinicaDto
        {
            NmClinica = "Clínica Teste",
            NrCnpj = "12.345.678/0001-99",
            DsEmailAcesso = "admin@teste.com",
            DsSenha = "Senha@2026"
        };

        await _sut.RegisterClinicaAsync(dto);

        clinicaSalva.Should().NotBeNull();
        clinicaSalva!.DsSenhaHash.Should().NotBe("Senha@2026");
        BCrypt.Net.BCrypt.Verify("Senha@2026", clinicaSalva.DsSenhaHash).Should().BeTrue();
    }
}
