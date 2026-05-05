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
        _sut = new AuthService(_repoMock.Object, _uowMock.Object, _config);
    }

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
        var clinica = new Clinica { Id = 1, DsEmailAcesso = "a@a.com", DsSenhaHash = hash, StAtiva = 'S' };

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
        var clinica = new Clinica { Id = 5, DsEmailAcesso = "vet@clinic.com", DsSenhaHash = hash, StAtiva = 'S' };

        _repoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Clinica, bool>>>()))
            .ReturnsAsync(new[] { clinica });

        var result = await _sut.LoginAsync(new LoginDto { DsEmail = "vet@clinic.com", DsSenha = "secret" });

        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    // ── RegisterClinica ──────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterClinicaAsync_ValidDto_RetornaResponseComIdPreenchido()
    {
        _repoMock.Setup(r => r.ExisteComCnpjAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.ExisteComEmailAcessoAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Clinica>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new RegisterClinicaDto
        {
            NmClinica = "Clínica Teste",
            NrCnpj = "12.345.678/0001-99",
            DsEndereco = "Rua A, 1",
            NrTelefone = "(11) 99999-9999",
            DsEmail = "contato@teste.com",
            DsEmailAcesso = "admin@teste.com",
            DsSenha = "Senha@2026"
        };

        var result = await _sut.RegisterClinicaAsync(dto);

        result.Should().NotBeNull();
        result.NmClinica.Should().Be("Clínica Teste");
        result.DsEmailAcesso.Should().Be("admin@teste.com");
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
