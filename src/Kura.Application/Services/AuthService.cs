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

/// <summary>
/// Autenticação do lado clínico.
///
/// <para>🔴 <b>FD-03 (ciclo FIN) — a fonte da autenticação MUDOU.</b> Até esta task o login
/// era POR CLÍNICA: validava e-mail/senha contra <c>CLINICA.DS_EMAIL_ACESSO</c> /
/// <c>CLINICA.DS_SENHA_HASH</c> e escolhia o "veterinário logado" por uma heurística de
/// fallback (bate o e-mail da clínica, senão o primeiro por <c>Id</c>). Consequência: não
/// existia autoria confiável de nenhum ato clínico, porque toda ação nascia do mesmo par
/// e-mail/senha compartilhado pela clínica inteira, e não existia papel.</para>
///
/// <para>Hoje a autenticação é contra <c>USUARIO_CLINICA</c> (V17), uma linha por humano.
/// A heurística <b>morreu</b>: o <c>veterinarioId</c> do token vem do <c>ID_VETERINARIO</c>
/// do usuário logado, e é <b>ausente</b> quando o gestor não é veterinário.</para>
///
/// <para>⛔ <b>O caminho de login por clínica foi REMOVIDO</b> (ruling D-10) — existe um jeito
/// só de autenticar. As colunas <c>CLINICA.DS_EMAIL_ACESSO</c>/<c>DS_SENHA_HASH</c>
/// continuam no schema de propósito: elas são a FONTE da conversão da V17 e derrubá-las é
/// outra migration, depois. <c>RegisterClinicaAsync</c> continua gravando as duas.</para>
///
/// <para>🔴 <b>O contrato HTTP de <c>POST /api/v1/auth/login</c> NÃO mudou.</b> Corpo
/// (<c>dsEmail</c>/<c>dsSenha</c>) e chaves da resposta são idênticos — há consumidores em
/// outros repositórios (<c>mobile-clinica-rn</c>, <c>DevOps-Cloud/scripts/*.sh</c>). O que
/// mudou é CONTRA O QUE o login valida, não COMO ele é chamado.</para>
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IClinicaRepository _clinicaRepository;
    private readonly IVeterinarioRepository _veterinarioRepository;
    private readonly IUsuarioClinicaRepository _usuarioClinicaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public AuthService(
        IClinicaRepository clinicaRepository,
        IVeterinarioRepository veterinarioRepository,
        IUsuarioClinicaRepository usuarioClinicaRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _clinicaRepository = clinicaRepository;
        _veterinarioRepository = veterinarioRepository;
        _usuarioClinicaRepository = usuarioClinicaRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    /// <summary>
    /// Mensagem de credencial inválida. Genérica de propósito: não revela se o e-mail
    /// existe. Idêntica à de antes da FD-03 — <c>AutenticacaoHttpTests</c> asserta o texto.
    /// </summary>
    private const string MensagemCredencialInvalida = "Email ou senha inválidos.";

    /// <summary>
    /// Mensagem do caso ambíguo. Ver <c>ResolverUsuarioAsync</c> para o argumento.
    /// </summary>
    public const string MensagemEmailAmbiguo =
        "Este e-mail está cadastrado em mais de uma clínica. " +
        "Não é possível identificar o usuário só pelo e-mail — " +
        "peça ao gestor da sua clínica um e-mail de acesso exclusivo.";

    public async Task<TokenResponseDto> LoginAsync(LoginDto dto)
    {
        var usuario = await ResolverUsuarioAsync(dto.DsEmail);

        if (usuario is null || !BCrypt.Net.BCrypt.Verify(dto.DsSenha, usuario.DsSenhaHash))
            throw new RegraDeNegocioException(MensagemCredencialInvalida);

        // O veterinário só existe se o usuário TIVER vínculo. Nada de fallback: um gestor
        // não-veterinário loga normalmente e a ficha vem nula (ver TokenResponseDto.Usuario).
        var veterinario = await ObterVeterinarioVinculadoAsync(usuario);

        var expiresAt = DateTime.UtcNow.AddHours(
            _configuration.GetValue<int>("Jwt:ExpiryHours", 8));

        var token = GenerateToken(
            usuario.IdClinica, usuario.IdVeterinario, usuario.DsEmail, usuario.TpPerfil, expiresAt);

        return new TokenResponseDto
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            TpPerfil = usuario.TpPerfil,
            Usuario = veterinario is null ? null : ToVeterinarioResponse(veterinario)
        };
    }

    /// <summary>
    /// Resolve o <see cref="UsuarioClinica"/> de um e-mail, ou <c>null</c> se não houver.
    ///
    /// <para>🔴 <b>Por que isto não é um <c>FirstOrDefault()</c>.</b> A UK da V17 é
    /// <c>(ID_CLINICA, DS_EMAIL)</c>: e-mail é único <b>por clínica</b>, não globalmente —
    /// um veterinário que atende em duas clínicas é o caso real que motivou a UK. Logo,
    /// "o usuário deste e-mail" pode ter mais de uma resposta, e essa ambiguidade
    /// <b>nasce exatamente aqui</b> (antes da FD-03 não existia: <c>CLINICA.DS_EMAIL_ACESSO</c>
    /// é globalmente único pela <c>UK_CLINICA_EMAIL_ACESSO</c> da V1, então o
    /// <c>FirstOrDefault()</c> do código antigo nunca via 2 linhas).</para>
    ///
    /// <para><b>A decisão: falha explícita quando N&gt;1</b>, antes de qualquer verificação
    /// de senha. Alternativas descartadas e por quê:</para>
    /// <list type="bullet">
    ///   <item><description><b>"Pega o primeiro"</b> — é literalmente o defeito que esta
    ///   task remove, reintroduzido por outra porta: escolha arbitrária e SILENCIOSA de
    ///   tenant. Rejeitada.</description></item>
    ///   <item><description><b>Escopar pelo par (clínica, e-mail)</b> — a resolução correta,
    ///   mas exige a clínica no corpo da requisição, e o corpo de
    ///   <c>POST /api/v1/auth/login</c> é contrato congelado com consumidores em outros
    ///   repositórios. Fica para quem puder mexer no cliente.</description></item>
    ///   <item><description><b>Deixar a senha desempatar</b> (verificar todos os candidatos e
    ///   aceitar o único que casar) — tentador, e ainda assim faz o tenant ser escolhido por
    ///   dado que o CHAMADOR controla; duas pessoas com o mesmo e-mail e a mesma senha (o
    ///   caso mais provável, porque é a MESMA pessoa) autenticariam numa clínica escolhida
    ///   por colisão. Além disso custa uma verificação BCrypt por candidato. Rejeitada.
    ///   </description></item>
    /// </list>
    ///
    /// <para><b>Trade-off assumido, declarado:</b> a mensagem específica revela que o e-mail
    /// está em ≥2 clínicas — uma enumeração nova, mais estreita que "este e-mail existe".
    /// Aceita porque a alternativa (devolver o genérico "Email ou senha inválidos.") tornaria
    /// um problema de CADASTRO indistinguível de senha errada, e o usuário legítimo ficaria
    /// num beco sem saída não diagnosticável. Erro explícito &gt; falha silenciosa.</para>
    /// </summary>
    private async Task<UsuarioClinica?> ResolverUsuarioAsync(string email)
    {
        var candidatos = await _usuarioClinicaRepository.BuscarAtivosPorEmailAsync(email);

        if (candidatos.Count > 1)
            throw new RegraDeNegocioException(MensagemEmailAmbiguo);

        return candidatos.SingleOrDefault();
    }

    /// <summary>
    /// Ficha do veterinário vinculado, ou <c>null</c> quando o usuário não tem vínculo.
    ///
    /// <para>⚠️ O predicado escopa por <c>IdClinica</c> <b>explicitamente</b>, e isso não é
    /// redundância: no login não há clínica no contexto, e o query filter de tenant
    /// <b>DESLIGA INTEIRO</b> (não nega) quando <c>IdClinicaFiltro</c> é nulo — travado em
    /// <c>UsuarioClinicaTenantIsolationTests.SemContextoDeClinica_FiltroDesligaInteiro_RetornaAsDuasClinicas</c>.
    /// Sem o <c>IdClinica</c> escrito aqui, um <c>ID_VETERINARIO</c> apontando para outra
    /// clínica (dado corrompido, ou uma FK futura mal escrita) devolveria a ficha do tenant
    /// errado sem nenhum aviso.</para>
    /// </summary>
    private async Task<Veterinario?> ObterVeterinarioVinculadoAsync(UsuarioClinica usuario)
    {
        if (usuario.IdVeterinario is not { } idVeterinario)
            return null;

        var vinculados = await _veterinarioRepository.FindAsync(
            v => v.Id == idVeterinario && v.IdClinica == usuario.IdClinica);

        return vinculados.FirstOrDefault();
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
        //
        // 🔴 FD-03: agora são TRÊS escritas, e a terceira é o PAR DE RUNTIME da conversão
        // da V17. A migration converte CLINICA -> USUARIO_CLINICA para o dado que JÁ
        // EXISTIA quando ela rodou; num ambiente do zero (`docker compose down -v && up -d`)
        // o schema nasce vazio e ela converte ZERO linhas. Quem cria clínica em runtime é
        // este método — então, sem esta escrita, a FD-03 entregaria um login por
        // USUARIO_CLINICA que NINGUÉM conseguiria exercer num ambiente novo: a clínica
        // existiria e não teria usuário. Ela entra na MESMA transação porque uma clínica
        // sem usuário é, a partir da FD-03, exatamente tão inutilizável quanto a clínica
        // órfã que a TASK-30 evitou — e com o e-mail igualmente "tomado".
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
                // TASK-36: sem fallback para string.Empty — Oracle trata VARCHAR2
                // vazio como NULL na escrita de qualquer forma, então "" era uma
                // garantia falsa. NULL é o valor correto para "telefone não
                // informado" (a coluna física já é NULLABLE).
                NrTelefone = dto.NrTelefone
            };

            await _veterinarioRepository.AddAsync(veterinario);
            await _unitOfWork.CommitAsync();

            // ID_VETERINARIO preenchido aqui NÃO é heurística — é a diferença exata entre
            // este ponto e a conversão da V17, que deixa NULL de propósito. A V17 teria de
            // ADIVINHAR o vínculo casando CLINICA.DS_EMAIL_ACESSO com VETERINARIO.DS_EMAIL,
            // e um palpite errado produz autoria ERRADA (pior que ausente). Aqui o vínculo é
            // CONHECIDO: este mesmo método acabou de criar os dois objetos, um a partir do
            // outro, na mesma transação. Efeito colateral desejado: no fluxo de
            // demonstração o gestor tem vínculo, então `Usuario` da resposta de login
            // continua preenchido e o app da clínica segue funcionando sem alteração.
            var usuarioGestor = new UsuarioClinica
            {
                IdClinica = clinica.Id,
                IdVeterinario = veterinario.Id,
                DsEmail = dto.DsEmailAcesso,
                // MESMO hash da clínica — nada de re-hashear: BCrypt gera salt novo a cada
                // chamada, então dois hashes da mesma senha divergem. Reaproveitar é o que
                // mantém as duas credenciais idênticas enquanto a coluna antiga existir, e
                // é o mesmo princípio da conversão da V17.
                DsSenhaHash = senhaHash,
                TpPerfil = PerfisUsuarioClinica.Gestor,
                StAtiva = true
            };

            await _usuarioClinicaRepository.AddAsync(usuarioGestor);
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

        var token = GenerateToken(
            clinica.Id, veterinario.Id, dto.DsEmailAcesso, PerfisUsuarioClinica.Gestor, expiresAt);

        return new RegisterClinicaResponseDto
        {
            IdClinica = clinica.Id,
            NmClinica = clinica.NmClinica,
            DsEmailAcesso = clinica.DsEmailAcesso,
            DtCriacao = clinica.DtCriacao,
            IdVeterinarioAdmin = veterinario.Id,
            AccessToken = token,
            ExpiresAt = expiresAt,
            TpPerfil = PerfisUsuarioClinica.Gestor,
            Usuario = ToVeterinarioResponse(veterinario)
        };
    }

    /// <summary>
    /// <para>Claims emitidas: <c>clinicaId</c>, <c>perfil</c>, e-mail e — <b>só quando
    /// houver vínculo</b> — <c>veterinarioId</c>.</para>
    ///
    /// <para>⚠️ Quando <paramref name="idVeterinario"/> é nulo a claim é <b>OMITIDA</b>, e
    /// não emitida com valor vazio ou <c>"0"</c>: <c>ClinicaContext.IdVeterinario</c> resolve
    /// por <c>long.TryParse</c>, então <c>""</c> viraria <c>null</c> do mesmo jeito, mas
    /// <c>"0"</c> viraria um id de veterinário INEXISTENTE — um valor errado com cara de
    /// valor certo. Claim ausente é a única codificação honesta de "não tem".</para>
    ///
    /// <para>O e-mail da claim é o do USUÁRIO (antes era o da clínica). Para tudo que existe
    /// hoje os dois coincidem; a partir da FD-04, quando a clínica tiver um segundo humano,
    /// não coincidem mais — e o valor correto é o de quem digitou a senha.</para>
    /// </summary>
    private string GenerateToken(
        long idClinica, long? idVeterinario, string email, string tpPerfil, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("clinicaId", idClinica.ToString()),
            new("perfil", tpPerfil),
            new(JwtRegisteredClaimNames.Email, email)
        };

        if (idVeterinario is { } vinculo)
            claims.Add(new Claim("veterinarioId", vinculo.ToString()));

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
