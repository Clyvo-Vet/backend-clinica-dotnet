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

/// <summary>
/// FD-03 (ciclo FIN): o login passou a validar contra <c>USUARIO_CLINICA</c>, e não mais
/// contra <c>CLINICA.DS_EMAIL_ACESSO</c>/<c>DS_SENHA_HASH</c>.
///
/// <para>⚠️ <b>Nota de leitura para quem revisar estes testes:</b> dois deles asseriam, antes
/// desta task, o comportamento OPOSTO — <c>LoginAsync_SemVetComEmailIgual_
/// UsaPrimeiroVeterinarioOrdenadoPorId</c> travava a heurística de fallback como se fosse
/// contrato, e <c>LoginAsync_ClinicaSemVeterinario_LancaRegraDeNegocioException</c> travava o
/// "Clínica sem veterinário responsável cadastrado." que a FD-03 remove. Os dois foram
/// SUBSTITUÍDOS, não apagados: os cenários equivalentes hoje provam que a escolha arbitrária
/// não acontece mais e que um gestor sem veterinário loga em vez de ser rejeitado.</para>
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IClinicaRepository> _repoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<IUsuarioClinicaRepository> _usuarioRepoMock = new();
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
        _sut = new AuthService(
            _repoMock.Object, _vetRepoMock.Object, _usuarioRepoMock.Object, _uowMock.Object, _config);

        // Default: nenhum usuário para nenhum e-mail. Cada teste planta o que precisa.
        _usuarioRepoMock.Setup(r => r.BuscarAtivosPorEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<UsuarioClinica>());
    }

    private static string GetClaim(string jwt, string claimType) =>
        new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims
            .FirstOrDefault(c => c.Type == claimType)?.Value ?? string.Empty;

    private static bool TemClaim(string jwt, string claimType) =>
        new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims.Any(c => c.Type == claimType);

    private void PlantarUsuarios(string email, params UsuarioClinica[] usuarios) =>
        _usuarioRepoMock.Setup(r => r.BuscarAtivosPorEmailAsync(email))
            .ReturnsAsync(usuarios);

    private void PlantarVeterinario(Veterinario veterinario) =>
        _vetRepoMock.Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Veterinario, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Veterinario, bool>> p) =>
                new[] { veterinario }.Where(p.Compile()).ToList());

    // ── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_EmailNotFound_ThrowsRegraDeNegocio()
    {
        // Act
        var act = () => _sut.LoginAsync(new LoginDto { DsEmail = "x@x.com", DsSenha = "pass" });

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Email ou senha inválidos.");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsRegraDeNegocio()
    {
        // Arrange
        PlantarUsuarios("a@a.com", new UsuarioClinica
        {
            Id = 1,
            IdClinica = 1,
            DsEmail = "a@a.com",
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword("correct"),
            TpPerfil = PerfisUsuarioClinica.Gestor,
            StAtiva = true
        });

        // Act
        var act = () => _sut.LoginAsync(new LoginDto { DsEmail = "a@a.com", DsSenha = "wrong" });

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Email ou senha inválidos.");
    }

    /// <summary>
    /// 🔴 <b>Prova de mordida — credencial CONVERTIDA pela V17.</b> A conversão da migration
    /// cria um <c>USUARIO_CLINICA</c> com <c>TP_PERFIL='GESTOR'</c> e
    /// <c>ID_VETERINARIO NULL</c>, reaproveitando e-mail e hash da clínica.
    ///
    /// <para><b>Controle positivo:</b> este teste não passaria contra o código antigo. (a) o
    /// <c>IUsuarioClinicaRepository</c> não era sequer parâmetro do construtor do
    /// <c>AuthService</c>; (b) a claim <c>perfil</c> não existia — <c>GenerateToken</c> emitia
    /// exatamente 3 claims, nenhuma de papel; (c) o código antigo, sem veterinário na clínica,
    /// LANÇAVA "Clínica sem veterinário responsável cadastrado." em vez de devolver token.</para>
    /// </summary>
    [Fact]
    public async Task LoginAsync_CredencialConvertidaPelaV17_DevolveTokenComPapelGestor()
    {
        // Arrange — exatamente o que o INSERT ... SELECT da V17 produz.
        PlantarUsuarios("gestor@clinica.test", new UsuarioClinica
        {
            Id = 100,
            IdClinica = 5,
            IdVeterinario = null,
            DsEmail = "gestor@clinica.test",
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword("secret"),
            TpPerfil = PerfisUsuarioClinica.Gestor,
            StAtiva = true
        });

        // Act
        var result = await _sut.LoginAsync(
            new LoginDto { DsEmail = "gestor@clinica.test", DsSenha = "secret" });

        // Assert
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        GetClaim(result.AccessToken, "perfil").Should().Be("GESTOR");
        GetClaim(result.AccessToken, "clinicaId").Should().Be("5");
        result.TpPerfil.Should().Be("GESTOR");
    }

    /// <summary>
    /// 🔴 <b>Prova de mordida — usuário VETERINÁRIO.</b> O <c>veterinarioId</c> do token vem
    /// do <c>ID_VETERINARIO</c> do usuário logado.
    ///
    /// <para><b>Controle positivo:</b> o veterinário plantado tem e-mail <b>DIFERENTE</b> do
    /// e-mail de login — sob a heurística antiga ele só seria escolhido pelo ramo de
    /// fallback. O teste asserta também a claim <c>perfil</c>, que o código antigo não
    /// emitia, e que <c>GetAllByClinicaIdAsync</c> (o coração da heurística) nunca é
    /// chamado.</para>
    /// </summary>
    [Fact]
    public async Task LoginAsync_UsuarioVeterinario_DevolvePapelVeterinarioEVeterinarioIdPreenchido()
    {
        // Arrange
        PlantarUsuarios("ana@clinica.test", new UsuarioClinica
        {
            Id = 101,
            IdClinica = 5,
            IdVeterinario = 42,
            DsEmail = "ana@clinica.test",
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword("secret"),
            TpPerfil = PerfisUsuarioClinica.Veterinario,
            StAtiva = true
        });
        PlantarVeterinario(new Veterinario
        {
            Id = 42,
            IdClinica = 5,
            NmVeterinario = "Dra. Ana",
            NrCrmv = "SP-123",
            DsEmail = "outro-email-de-cadastro@clinica.test",
            NrTelefone = "11999999999",
            StAtiva = true
        });

        // Act
        var result = await _sut.LoginAsync(
            new LoginDto { DsEmail = "ana@clinica.test", DsSenha = "secret" });

        // Assert
        GetClaim(result.AccessToken, "perfil").Should().Be("VETERINARIO");
        GetClaim(result.AccessToken, "veterinarioId").Should().Be("42");
        result.TpPerfil.Should().Be("VETERINARIO");
        result.Usuario.Should().NotBeNull();
        result.Usuario!.Id.Should().Be(42);
        result.Usuario.NmVeterinario.Should().Be("Dra. Ana");

        // A heurística de fallback vivia exatamente aqui. Se alguém a ressuscitar, este
        // Verify quebra.
        _vetRepoMock.Verify(r => r.GetAllByClinicaIdAsync(It.IsAny<long>()), Times.Never,
            "a escolha do veterinário por varredura da clínica é a heurística que a FD-03 matou");
    }

    /// <summary>
    /// 🔴 <b>Prova de mordida — GESTOR PURO.</b> Sem <c>ID_VETERINARIO</c>, o login FUNCIONA,
    /// a claim <c>veterinarioId</c> é OMITIDA e <c>Usuario</c> vem nulo — sem lançar.
    ///
    /// <para><b>Controle positivo:</b> contra o código antigo este cenário terminava em
    /// <c>RegraDeNegocioException("Clínica sem veterinário responsável cadastrado.")</c>. A
    /// asserção de AUSÊNCIA da claim é o outro lado do controle: emitir <c>"0"</c> ou
    /// <c>""</c> passaria num teste que só checasse "não lançou".</para>
    /// </summary>
    [Fact]
    public async Task LoginAsync_GestorSemVeterinario_LogaComVeterinarioIdAusenteEUsuarioNulo()
    {
        // Arrange
        PlantarUsuarios("dono@clinica.test", new UsuarioClinica
        {
            Id = 102,
            IdClinica = 9,
            IdVeterinario = null,
            DsEmail = "dono@clinica.test",
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword("secret"),
            TpPerfil = PerfisUsuarioClinica.Gestor,
            StAtiva = true
        });

        // Act
        var result = await _sut.LoginAsync(
            new LoginDto { DsEmail = "dono@clinica.test", DsSenha = "secret" });

        // Assert
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Usuario.Should().BeNull(
            "um gestor não-veterinário não tem ficha de veterinário — inventar uma seria autoria errada");
        result.TpPerfil.Should().Be("GESTOR");

        TemClaim(result.AccessToken, "veterinarioId").Should().BeFalse(
            "claim ausente é a única codificação honesta de \"não tem\": \"0\" seria um id inexistente");
        TemClaim(result.AccessToken, "clinicaId").Should().BeTrue();

        // Nenhuma tentativa de descobrir um veterinário para pendurar no token.
        _vetRepoMock.Verify(r => r.GetAllByClinicaIdAsync(It.IsAny<long>()), Times.Never);
        _vetRepoMock.Verify(
            r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Veterinario, bool>>>()),
            Times.Never);
    }

    /// <summary>
    /// 🔴 <b>Prova de mordida — E-MAIL EM 2 CLÍNICAS.</b> A UK da V17 é
    /// <c>(ID_CLINICA, DS_EMAIL)</c>, então isto é um estado LEGAL do banco. O login falha
    /// com mensagem própria em vez de escolher um tenant.
    ///
    /// <para><b>Controle positivo:</b> a senha plantada é VÁLIDA para os dois usuários, e o
    /// de menor <c>IdClinica</c> vem primeiro na lista — é exatamente a situação em que um
    /// <c>FirstOrDefault()</c> devolveria 200 alegremente, com o tenant errado em metade das
    /// vezes. O teste asserta também que a mensagem NÃO é a genérica de credencial inválida:
    /// sem isso, trocar a falha explícita por "Email ou senha inválidos." passaria.</para>
    /// </summary>
    [Fact]
    public async Task LoginAsync_MesmoEmailEmDuasClinicas_FalhaExplicitamenteSemEscolherTenant()
    {
        // Arrange
        var hash = BCrypt.Net.BCrypt.HashPassword("secret");
        PlantarUsuarios("compartilhado@vet.test",
            new UsuarioClinica
            {
                Id = 200,
                IdClinica = 1,
                IdVeterinario = 11,
                DsEmail = "compartilhado@vet.test",
                DsSenhaHash = hash,
                TpPerfil = PerfisUsuarioClinica.Veterinario,
                StAtiva = true
            },
            new UsuarioClinica
            {
                Id = 201,
                IdClinica = 2,
                IdVeterinario = 22,
                DsEmail = "compartilhado@vet.test",
                DsSenhaHash = hash,
                TpPerfil = PerfisUsuarioClinica.Veterinario,
                StAtiva = true
            });

        // Act
        var act = () => _sut.LoginAsync(
            new LoginDto { DsEmail = "compartilhado@vet.test", DsSenha = "secret" });

        // Assert
        var excecao = await act.Should().ThrowAsync<RegraDeNegocioException>();
        excecao.Which.Message.Should().Be(AuthService.MensagemEmailAmbiguo);
        excecao.Which.Message.Should().NotBe("Email ou senha inválidos.",
            "falha por cadastro ambíguo não pode se disfarçar de senha errada — vira beco sem saída");

        // Nenhum veterinário foi consultado: a ambiguidade é resolvida ANTES de qualquer
        // coisa depender de dado controlado pelo chamador.
        _vetRepoMock.Verify(
            r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Veterinario, bool>>>()),
            Times.Never);
    }

    /// <summary>
    /// 🔴 <b>Prova de mordida — o caminho de login POR CLÍNICA foi REMOVIDO (D-10).</b> Uma
    /// clínica com <c>DS_EMAIL_ACESSO</c>/<c>DS_SENHA_HASH</c> válidos e SEM
    /// <c>USUARIO_CLINICA</c> correspondente não autentica mais.
    ///
    /// <para><b>Controle positivo:</b> o mock de <c>IClinicaRepository</c> devolve a clínica
    /// com o hash correto e a clínica tem veterinário — é o arranjo EXATO do antigo
    /// <c>LoginAsync_ValidCredentials_ReturnsToken</c>, que devolvia token. Se alguém
    /// reintroduzir o fallback "tenta a clínica se não achou usuário", este teste quebra.</para>
    /// </summary>
    [Fact]
    public async Task LoginAsync_CredencialDeClinicaSemUsuarioClinica_NaoAutenticaMais()
    {
        // Arrange
        var hash = BCrypt.Net.BCrypt.HashPassword("secret");
        _repoMock.Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Clinica, bool>>>()))
            .ReturnsAsync(new[]
            {
                new Clinica { Id = 5, DsEmailAcesso = "vet@clinic.com", DsSenhaHash = hash, StAtiva = true }
            });
        _vetRepoMock.Setup(r => r.GetAllByClinicaIdAsync(5))
            .ReturnsAsync(new[] { new Veterinario { Id = 42, IdClinica = 5, DsEmail = "vet@clinic.com" } });
        // ...e NENHUM USUARIO_CLINICA (default do construtor).

        // Act
        var act = () => _sut.LoginAsync(new LoginDto { DsEmail = "vet@clinic.com", DsSenha = "secret" });

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Email ou senha inválidos.");

        _repoMock.Verify(
            r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Clinica, bool>>>()),
            Times.Never,
            "LoginAsync não pode mais tocar em CLINICA para autenticar");
    }

    // ── RegisterClinica ──────────────────────────────────────────────────────

    private void ArranjarRegistroFeliz(
        long idClinica, long idVeterinario,
        Action<Clinica>? aoSalvarClinica = null,
        Action<Veterinario>? aoSalvarVeterinario = null,
        Action<UsuarioClinica>? aoSalvarUsuario = null)
    {
        _repoMock.Setup(r => r.ExisteComCnpjAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.ExisteComEmailAcessoAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Clinica>()))
            .Callback<Clinica>(c => { c.Id = idClinica; aoSalvarClinica?.Invoke(c); })
            .Returns(Task.CompletedTask);
        _vetRepoMock.Setup(r => r.AddAsync(It.IsAny<Veterinario>()))
            .Callback<Veterinario>(v => { v.Id = idVeterinario; aoSalvarVeterinario?.Invoke(v); })
            .Returns(Task.CompletedTask);
        _usuarioRepoMock.Setup(r => r.AddAsync(It.IsAny<UsuarioClinica>()))
            .Callback<UsuarioClinica>(u => aoSalvarUsuario?.Invoke(u))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);
    }

    private static RegisterClinicaDto BuildDto(string emailAcesso = "admin@teste.com") => new()
    {
        NmClinica = "Clínica Teste",
        NrCnpj = "12.345.678/0001-99",
        DsEndereco = "Rua A, 1",
        NrTelefone = "(11) 99999-9999",
        DsEmail = "contato@teste.com",
        DsEmailAcesso = emailAcesso,
        DsSenha = "Senha@2026",
        NmVeterinarioAdmin = "Dr. Admin",
        NrCRMV = "SP-000111"
    };

    [Fact]
    public async Task RegisterClinicaAsync_ValidDto_RetornaResponseComIdPreenchido()
    {
        // Arrange
        ArranjarRegistroFeliz(idClinica: 1, idVeterinario: 1);

        // Act
        var result = await _sut.RegisterClinicaAsync(BuildDto());

        // Assert
        result.Should().NotBeNull();
        result.NmClinica.Should().Be("Clínica Teste");
        result.DsEmailAcesso.Should().Be("admin@teste.com");
    }

    /// <summary>
    /// 🔴 <b>Prova de mordida — o PAR DE RUNTIME da conversão D-10.</b> A V17 converte
    /// <c>CLINICA</c> → <c>USUARIO_CLINICA</c> só para o dado que já existia quando ela
    /// rodou; num ambiente do zero ela converte <b>zero</b> linhas. Quem cria clínica em
    /// runtime é este método, então é ele que precisa criar o gestor — senão a FD-03 entrega
    /// um login que ninguém consegue exercer em ambiente novo.
    ///
    /// <para><b>Controle positivo:</b> o código antigo não conhecia
    /// <c>IUsuarioClinicaRepository</c>; <c>AddAsync</c> nunca era chamado, e o
    /// <c>Times.Once</c> abaixo é impossível de satisfazer sem a escrita nova. As asserções
    /// de hash (mesmo hash da clínica) e de <c>IdVeterinario</c> fecham as duas formas de
    /// "criou, mas errado".</para>
    /// </summary>
    [Fact]
    public async Task RegisterClinicaAsync_CriaUsuarioClinicaGestorNaMesmaTransacao()
    {
        // Arrange
        Clinica? clinicaSalva = null;
        Veterinario? veterinarioSalvo = null;
        UsuarioClinica? usuarioSalvo = null;

        ArranjarRegistroFeliz(
            idClinica: 100, idVeterinario: 200,
            aoSalvarClinica: c => clinicaSalva = c,
            aoSalvarVeterinario: v => veterinarioSalvo = v,
            aoSalvarUsuario: u => usuarioSalvo = u);

        // Act
        var result = await _sut.RegisterClinicaAsync(BuildDto());

        // Assert
        _usuarioRepoMock.Verify(r => r.AddAsync(It.IsAny<UsuarioClinica>()), Times.Once);

        usuarioSalvo.Should().NotBeNull();
        usuarioSalvo!.IdClinica.Should().Be(100);
        usuarioSalvo.TpPerfil.Should().Be(PerfisUsuarioClinica.Gestor);
        usuarioSalvo.DsEmail.Should().Be("admin@teste.com");
        usuarioSalvo.StAtiva.Should().BeTrue();

        usuarioSalvo.IdVeterinario.Should().Be(200,
            "o vínculo aqui é CONHECIDO (o método acabou de criar o veterinário), não adivinhado — " +
            "é a diferença exata entre este ponto e a conversão da V17, que deixa NULL");
        usuarioSalvo.IdVeterinario.Should().Be(veterinarioSalvo!.Id);

        usuarioSalvo.DsSenhaHash.Should().Be(clinicaSalva!.DsSenhaHash,
            "re-hashear geraria salt novo e as duas credenciais divergiriam em silêncio");
        BCrypt.Net.BCrypt.Verify("Senha@2026", usuarioSalvo.DsSenhaHash).Should().BeTrue();

        // A escrita é dentro da transação aberta pela TASK-30 — nunca depois do commit dela.
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Never);

        result.TpPerfil.Should().Be(PerfisUsuarioClinica.Gestor);
    }

    /// <summary>
    /// 🔴 <b>Prova de mordida — o usuário criado no registro CONSEGUE LOGAR.</b> Fecha o ciclo
    /// que os dois testes acima cobrem só pela metade: registro grava, login lê. É a versão
    /// unitária do que <c>seed-demo.sh</c> faz ponta a ponta em outro repositório.
    ///
    /// <para><b>Controle positivo:</b> o login é feito com o <c>USUARIO_CLINICA</c> que o
    /// PRÓPRIO registro produziu (capturado no callback), não com um objeto montado à mão —
    /// se o registro gravar hash re-hasheado ou e-mail diferente, o <c>BCrypt.Verify</c> do
    /// login falha e este teste quebra.</para>
    /// </summary>
    [Fact]
    public async Task RegisterClinicaAsync_UsuarioCriado_ConsegueLogarEmSeguida()
    {
        // Arrange
        UsuarioClinica? usuarioSalvo = null;
        Veterinario? veterinarioSalvo = null;
        ArranjarRegistroFeliz(
            idClinica: 300, idVeterinario: 301,
            aoSalvarVeterinario: v => veterinarioSalvo = v,
            aoSalvarUsuario: u => usuarioSalvo = u);

        await _sut.RegisterClinicaAsync(BuildDto("demo@kura.local"));

        // O "banco" agora contém exatamente o que o registro gravou.
        PlantarUsuarios("demo@kura.local", usuarioSalvo!);
        PlantarVeterinario(veterinarioSalvo!);

        // Act
        var login = await _sut.LoginAsync(
            new LoginDto { DsEmail = "demo@kura.local", DsSenha = "Senha@2026" });

        // Assert
        login.AccessToken.Should().NotBeNullOrWhiteSpace();
        GetClaim(login.AccessToken, "perfil").Should().Be(PerfisUsuarioClinica.Gestor);
        GetClaim(login.AccessToken, "clinicaId").Should().Be("300");
        GetClaim(login.AccessToken, "veterinarioId").Should().Be("301");
        login.Usuario.Should().NotBeNull(
            "no fluxo de demonstração o gestor TEM vínculo, então o app da clínica continua " +
            "recebendo a ficha que ele já esperava");
        login.Usuario!.Id.Should().Be(301);
    }

    [Fact]
    public async Task RegisterClinicaAsync_ValidDto_CriaVeterinarioERetornaTokenEUsuario()
    {
        // Arrange
        Veterinario? veterinarioSalvo = null;
        ArranjarRegistroFeliz(
            idClinica: 100, idVeterinario: 200,
            aoSalvarVeterinario: v => veterinarioSalvo = v);

        // Act
        var result = await _sut.RegisterClinicaAsync(BuildDto());

        // Assert
        veterinarioSalvo.Should().NotBeNull();
        veterinarioSalvo!.IdClinica.Should().Be(100);
        veterinarioSalvo.NmVeterinario.Should().Be("Dr. Admin");
        veterinarioSalvo.NrCrmv.Should().Be("SP-000111");
        veterinarioSalvo.DsEmail.Should().Be("admin@teste.com");
        veterinarioSalvo.NrTelefone.Should().Be("(11) 99999-9999");

        result.IdVeterinarioAdmin.Should().Be(200);
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        // Contrato NÃO afrouxado aqui: seed-demo.sh:162 e smoke-contratos.sh:251 leem
        // `usuario.id` DESTA resposta e o usam como idVeterinario nos POSTs seguintes.
        result.Usuario.Should().NotBeNull();
        result.Usuario.Id.Should().Be(200);
        result.Usuario.NmVeterinario.Should().Be("Dr. Admin");
        result.Usuario.NrCrmv.Should().Be("SP-000111");

        GetClaim(result.AccessToken, "veterinarioId").Should().Be("200");
    }

    [Fact]
    public async Task RegisterClinicaAsync_SemTelefone_NaoAplicaFallbackParaStringVazia()
    {
        // Arrange
        // TASK-36 (E-4): dto.NrTelefone ?? string.Empty foi removido — Oracle trata
        // VARCHAR2 vazio como NULL na escrita de qualquer forma, então a "garantia"
        // de string.Empty era falsa e mascarava um NULL real. O comportamento correto
        // é propagar null e deixar a coluna (NULLABLE no schema físico) refletir isso.
        Veterinario? veterinarioSalvo = null;
        ArranjarRegistroFeliz(
            idClinica: 300, idVeterinario: 301,
            aoSalvarVeterinario: v => veterinarioSalvo = v);

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

        // Act
        var result = await _sut.RegisterClinicaAsync(dto);

        // Assert
        veterinarioSalvo.Should().NotBeNull();
        veterinarioSalvo!.NrTelefone.Should().BeNull("null é o valor correto para telefone não informado, não \"\"");
        result.Usuario.NrTelefone.Should().BeNull("a resposta HTTP não deve mascarar o NULL como string vazia");
    }

    [Fact]
    public async Task RegisterClinicaAsync_CnpjDuplicado_LancaRegraDeNegocioException()
    {
        // Arrange
        _repoMock.Setup(r => r.ExisteComCnpjAsync("12.345.678/0001-99")).ReturnsAsync(true);

        var dto = new RegisterClinicaDto
        {
            NmClinica = "Clínica Teste",
            NrCnpj = "12.345.678/0001-99",
            DsEmailAcesso = "admin@teste.com",
            DsSenha = "Senha@2026"
        };

        // Act
        var act = () => _sut.RegisterClinicaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Já existe uma clínica cadastrada com este CNPJ.");
    }

    [Fact]
    public async Task RegisterClinicaAsync_EmailDuplicado_LancaRegraDeNegocioException()
    {
        // Arrange
        _repoMock.Setup(r => r.ExisteComCnpjAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.ExisteComEmailAcessoAsync("admin@teste.com")).ReturnsAsync(true);

        var dto = new RegisterClinicaDto
        {
            NmClinica = "Clínica Teste",
            NrCnpj = "12.345.678/0001-99",
            DsEmailAcesso = "admin@teste.com",
            DsSenha = "Senha@2026"
        };

        // Act
        var act = () => _sut.RegisterClinicaAsync(dto);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Já existe uma clínica cadastrada com este e-mail de acesso.");
    }

    [Fact]
    public void RegisterClinicaAsync_DtoDeResponseViaReflection_SenhaNaoRetornadaNoResponse()
    {
        // Act
        var tipo = typeof(RegisterClinicaResponseDto);

        // Assert
        tipo.GetProperty("DsSenhaHash").Should().BeNull("hash nunca deve ser exposto no DTO de resposta");
        tipo.GetProperty("DsSenha").Should().BeNull("senha em texto puro nunca deve ser exposta no DTO de resposta");
    }

    [Fact]
    public async Task RegisterClinicaAsync_SenhaSalvaComoHash_NaoIgualTextoPuro()
    {
        // Arrange
        Clinica? clinicaSalva = null;
        ArranjarRegistroFeliz(
            idClinica: 1, idVeterinario: 1,
            aoSalvarClinica: c => clinicaSalva = c);

        var dto = new RegisterClinicaDto
        {
            NmClinica = "Clínica Teste",
            NrCnpj = "12.345.678/0001-99",
            DsEmailAcesso = "admin@teste.com",
            DsSenha = "Senha@2026"
        };

        // Act
        await _sut.RegisterClinicaAsync(dto);

        // Assert
        clinicaSalva.Should().NotBeNull();
        clinicaSalva!.DsSenhaHash.Should().NotBe("Senha@2026");
        BCrypt.Net.BCrypt.Verify("Senha@2026", clinicaSalva.DsSenhaHash).Should().BeTrue();
    }
}
