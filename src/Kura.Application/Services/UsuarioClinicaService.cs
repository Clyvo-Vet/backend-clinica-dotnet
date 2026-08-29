namespace Kura.Application.Services;

using Kura.Application.DTOs.UsuarioClinica;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

/// <summary>
/// CRUD de <see cref="UsuarioClinica"/> (FD-04, ciclo FIN) — o caminho pelo qual o
/// <b>segundo humano</b> de uma clínica passa a existir. Até aqui a única forma de criar
/// usuário era o registro da clínica inteira (<c>AuthService.RegisterClinicaAsync</c>) ou a
/// conversão da V17: uma clínica nascia com exatamente um usuário e ficava assim.
///
/// <para>
/// 🔴 <b>TODA escrita é escopada pelo <c>clinicaId</c> do JWT, e nenhum DTO desta task tem
/// campo <c>IdClinica</c>.</b> Não é preferência de estilo: é a lição da <c>FD-05</c>, onde
/// <c>VeterinarioService.CreateAsync</c> grava <c>dto.IdClinica</c> sem comparar com o token
/// e por isso qualquer clínica autenticada cria veterinário dentro de outra. A correção
/// óbvia — aceitar o campo e comparar — deixa a garantia dependendo de alguém lembrar da
/// comparação em cada caminho novo, para sempre. Aqui o campo <b>não existe</b>: a clínica
/// vem de <see cref="IClinicaContext.IdClinica"/> e de mais lugar nenhum.
/// </para>
///
/// <para>
/// 🔴 <b>DECISÃO DE PRODUTO — PROTEÇÃO DO ÚLTIMO GESTOR.</b> Um GESTOR <b>pode</b> rebaixar a
/// si mesmo, <b>pode</b> se desativar e <b>pode</b> rebaixar/desativar outro GESTOR. O que é
/// recusado — com <c>422</c> — é qualquer operação que deixaria a clínica com <b>ZERO GESTOR
/// ativo</b>.
/// </para>
///
/// <para>
/// <b>Por que o invariante é sobre a CLÍNICA e não sobre "si mesmo".</b> Formular a regra
/// como "não pode se rebaixar" exigiria saber QUEM está chamando — e o JWT desta aplicação
/// <b>não carrega o id do <c>USUARIO_CLINICA</c></b> (as claims são <c>clinicaId</c>,
/// <c>perfil</c>, e-mail e, quando há vínculo, <c>veterinarioId</c> — ver
/// <c>AuthService.GenerateToken</c>). Identificar o chamador exigiria uma claim nova, que
/// <b>token pré-FD-03 não teria</b>: a proteção falharia aberta exatamente para o token mais
/// antigo, que é o caso que esta task existe para tratar. O invariante escrito sobre o alvo
/// e a contagem da clínica não precisa saber quem chamou, cobre auto e hétero uniformemente,
/// e vale igual para qualquer vintage de token.
/// </para>
///
/// <para>
/// <b>Por que recusar, em vez de permitir e avisar.</b> Neste escopo não existe recuperação
/// de senha, convite por e-mail nem super-admin (escopo negativo da FD-04). Uma clínica com
/// zero gestores fica <b>permanentemente</b> incapaz de administrar os próprios usuários, e a
/// única saída é cirurgia manual no Oracle da FIAP — cuja conta, neste ciclo, está
/// <c>ORA-28000</c>. Os dois erros possíveis têm custo assimétrico: recusar uma operação
/// legítima custa <b>um passo a mais</b> (promova alguém antes); permiti-la custa um tenant
/// inutilizado sem caminho de volta dentro do produto.
/// </para>
///
/// <para>
/// ⚠️ <b>O que este invariante NÃO garante:</b> ele conta GESTORES, não pessoas alcançáveis.
/// Uma clínica cujo último gestor perdeu a senha continua travada — o invariante impede que
/// o sistema chegue a zero gestor, não que o único gestor deixe de conseguir entrar. Esse é
/// o buraco que um fluxo de recuperação fecharia, e ele está declaradamente fora de escopo.
/// </para>
/// </summary>
public sealed class UsuarioClinicaService : IUsuarioClinicaService
{
    /// <summary>
    /// Mensagem do invariante do último gestor. Constante porque o teste asserta o texto —
    /// um <c>422</c> por outra regra de negócio passaria por um teste que só olhasse o
    /// status.
    /// </summary>
    public const string MensagemUltimoGestor =
        "Esta operação deixaria a clínica sem nenhum gestor ativo. "
        + "Promova outro usuário a GESTOR antes de rebaixar ou desativar este.";

    /// <summary>
    /// 🔴 <b>Fix wave pós-G2 (achado A-3).</b> Mensagem da recusa de alterar usuário
    /// DESATIVADO. Antes desta correção, <c>AtualizarAsync</c> e <c>DefinirSenhaAsync</c>
    /// respondiam <b><c>200</c>/<c>204</c></b> sobre um usuário desativado: a alteração era
    /// gravada, mas o usuário continuava sem conseguir entrar (o login filtra
    /// <c>ST_ATIVA</c>) e a resposta não dizia isso em lugar nenhum. Quem administrava via
    /// sucesso e nada acontecia — a mesma classe de defeito da TASK-69 (tela de cadastro de
    /// pet que fingia sucesso com <c>setTimeout</c>).
    /// </summary>
    public const string MensagemUsuarioDesativado =
        "Este usuário está DESATIVADO e alterações não têm efeito enquanto ele estiver assim. "
        + "Reative-o primeiro (operação de reativação deste mesmo recurso) e refaça a alteração.";

    /// <summary>Mensagem do conflito de e-mail na reativação. Ver <c>ReativarAsync</c>.</summary>
    public const string MensagemReativacaoComEmailOcupado =
        "Não é possível reativar: o e-mail deste usuário já está em uso por outro usuário "
        + "desta clínica. Troque o e-mail do outro usuário antes de reativar este.";

    private readonly IUsuarioClinicaRepository _repository;
    private readonly IVeterinarioRepository _veterinarioRepository;
    private readonly IUnitOfWork _uow;
    private readonly IClinicaContext _clinicaContext;

    public UsuarioClinicaService(
        IUsuarioClinicaRepository repository,
        IVeterinarioRepository veterinarioRepository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext)
    {
        _repository = repository;
        _veterinarioRepository = veterinarioRepository;
        _uow = uow;
        _clinicaContext = clinicaContext;
    }

    public async Task<IEnumerable<UsuarioClinicaResponseDto>> ListarAsync()
    {
        var usuarios = await _repository.ListarDaClinicaAsync(_clinicaContext.IdClinica);
        return usuarios.Select(ToResponse);
    }

    public async Task<UsuarioClinicaResponseDto> ObterPorIdAsync(long id) =>
        ToResponse(await ObterOuFalharAsync(id));

    public async Task<UsuarioClinicaResponseDto> CriarAsync(UsuarioClinicaCreateDto dto)
    {
        var idClinica = _clinicaContext.IdClinica;
        var email = NormalizarEmail(dto.DsEmail);
        var perfil = NormalizarPerfil(dto.TpPerfil);

        await GarantirEmailDisponivelAsync(idClinica, email);
        await GarantirVeterinarioDaClinicaAsync(dto.IdVeterinario, idClinica);

        var usuario = new UsuarioClinica
        {
            // 🔴 A clínica sai do JWT. Não existe caminho por onde o corpo da requisição
            // influencie esta linha — ver a documentação da classe e do DTO.
            IdClinica = idClinica,
            IdVeterinario = dto.IdVeterinario,
            DsEmail = email,
            // Mesmo algoritmo e mesma API que AuthService usa para gravar e para verificar
            // (BCrypt.Net.BCrypt.HashPassword / .Verify). Trocar de biblioteca ou de custo
            // aqui, sem trocar no AuthService, produziria usuário que nasce e nunca loga.
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(dto.DsSenha),
            TpPerfil = perfil,
            StAtiva = true,
        };

        await _repository.AddAsync(usuario);
        await _uow.CommitAsync();

        return ToResponse(usuario);
    }

    public async Task<UsuarioClinicaResponseDto> AtualizarAsync(
        long id, UsuarioClinicaUpdateDto dto)
    {
        var idClinica = _clinicaContext.IdClinica;
        var usuario = await ObterOuFalharAsync(id);

        // A-3 (fix wave pós-G2): NADA de sucesso silencioso sobre usuário desativado.
        GarantirUsuarioAtivo(usuario);

        var email = NormalizarEmail(dto.DsEmail);
        var perfil = NormalizarPerfil(dto.TpPerfil);

        if (!string.Equals(email, usuario.DsEmail, StringComparison.Ordinal))
            await GarantirEmailDisponivelAsync(idClinica, email);

        // Invariante do último gestor: só é consultado quando a mudança de fato REMOVE um
        // gestor ativo do quadro. Um usuário já desativado não conta como gestor ativo, então
        // rebaixá-lo não pode zerar contagem nenhuma — checar ali daria 422 sem motivo.
        if (usuario.TpPerfil == PerfisUsuarioClinica.Gestor
            && perfil != PerfisUsuarioClinica.Gestor)
        {
            await GarantirQueSobraGestorAsync(idClinica, usuario.Id);
        }

        await GarantirVeterinarioDaClinicaAsync(dto.IdVeterinario, idClinica);

        usuario.DsEmail = email;
        usuario.TpPerfil = perfil;
        usuario.IdVeterinario = dto.IdVeterinario;

        _repository.Update(usuario);
        await _uow.CommitAsync();

        return ToResponse(usuario);
    }

    public async Task DefinirSenhaAsync(long id, UsuarioClinicaSenhaUpdateDto dto)
    {
        var usuario = await ObterOuFalharAsync(id);

        // A-3: definir senha de usuário desativado é gravação sem efeito observável — o login
        // filtra ST_ATIVA, então a senha nova nunca seria usada. 422 em vez de 204 mentiroso.
        GarantirUsuarioAtivo(usuario);

        usuario.DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(dto.DsSenha);

        _repository.Update(usuario);
        await _uow.CommitAsync();
    }

    public async Task DesativarAsync(long id)
    {
        var usuario = await ObterOuFalharAsync(id);

        // Já desativado: nada a fazer. Repetir o soft delete só reescreveria DT_ATUALIZACAO,
        // e disparar o invariante do último gestor aqui recusaria uma operação que não muda
        // nada.
        if (!usuario.StAtiva)
            return;

        if (usuario.TpPerfil == PerfisUsuarioClinica.Gestor)
            await GarantirQueSobraGestorAsync(_clinicaContext.IdClinica, usuario.Id);

        _repository.SoftDelete(usuario);
        await _uow.CommitAsync();
    }

    /// <summary>
    /// 🔴 <b>A-3 (fix wave pós-G2) — a saída da porta de mão única.</b>
    ///
    /// <para><b>O problema, como a revisão G2 o descreveu:</b> desativar um usuário sumia com
    /// ele da lista, <b>queimava o e-mail para sempre</b> (a checagem de UK inclui inativo, e
    /// isso está certo — a linha continua ocupando <c>UK_USUARIO_CLINICA_EMAIL</c> no Oracle) e
    /// <b>não havia volta</b>. Pior: o <c>PUT</c> respondia <c>200</c> sem reativar.</para>
    ///
    /// <para><b>DECISÃO: endpoint de reativação, e não recusa com instrução.</b> As duas
    /// opções matavam o <c>200</c> silencioso; o que as separa é o que sobra depois. Recusar
    /// com uma mensagem clara deixaria a clínica com um e-mail permanentemente inutilizável e
    /// uma pessoa que só volta ao quadro por cirurgia no Oracle — cuja conta, neste ciclo,
    /// está <c>ORA-28000</c>. Como o escopo negativo da FD-04 já retirou recuperação de senha,
    /// convite por e-mail e super-admin, "peça ao suporte" não descreve nenhum caminho que
    /// exista. Um <c>POST .../reativacao</c> custa um método, herda a política
    /// <c>SomenteGestor</c> e o escopo de tenant do controller inteiro, e não abre superfície
    /// nova: só volta um <c>ST_ATIVA</c> que um gestor da MESMA clínica já tinha desligado.</para>
    ///
    /// <para><b>Idempotente por decisão:</b> reativar quem já está ativo devolve o usuário sem
    /// erro. O estado pedido é o estado vigente — e isto <b>não</b> é sucesso silencioso: o
    /// corpo devolve <c>stAtiva: true</c>, que é exatamente o que foi pedido. É o mesmo
    /// critério que mantém <c>DesativarAsync</c> tolerante com quem já está inativo.</para>
    ///
    /// <para>⚠️ <b>O que a reativação NÃO revalida, declarado:</b> o vínculo com
    /// <c>VETERINARIO</c>. Se o veterinário vinculado tiver sido desativado nesse meio-tempo, a
    /// reativação passa mesmo assim. Recusar ali transformaria a desativação de um veterinário
    /// em bloqueio da volta de um usuário administrativo que não tem nada a ver com aquilo —
    /// custo maior que o do vínculo pendurado, que qualquer <c>PUT</c> posterior corrige.</para>
    /// </summary>
    public async Task<UsuarioClinicaResponseDto> ReativarAsync(long id)
    {
        var idClinica = _clinicaContext.IdClinica;
        var usuario = await ObterOuFalharAsync(id);

        if (usuario.StAtiva)
            return ToResponse(usuario);

        // Conflito de e-mail com OUTRO usuário (por isso o excetoId): em base já existente
        // pode haver linha ativa com o mesmo e-mail — a UK do Oracle não distingue ativo de
        // inativo, mas dado herdado ou criado antes desta task pode. Reativar às cegas
        // devolveria ORA-00001 (500) ou, pior, deixaria o login ambíguo dentro da clínica.
        var outro = await _repository.BuscarPorEmailNaClinicaAsync(
            idClinica, usuario.DsEmail, excetoId: usuario.Id);

        if (outro is not null)
            throw new RegraDeNegocioException(MensagemReativacaoComEmailOcupado);

        usuario.StAtiva = true;
        _repository.Update(usuario);
        await _uow.CommitAsync();

        return ToResponse(usuario);
    }

    /// <summary>A-3 — ver <see cref="MensagemUsuarioDesativado"/>.</summary>
    private static void GarantirUsuarioAtivo(UsuarioClinica usuario)
    {
        if (!usuario.StAtiva)
            throw new RegraDeNegocioException(MensagemUsuarioDesativado);
    }

    private async Task<UsuarioClinica> ObterOuFalharAsync(long id) =>
        await _repository.BuscarPorIdNaClinicaAsync(id, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("UsuarioClinica", id);

    /// <summary>
    /// Checagem explícita de <c>UK_USUARIO_CLINICA_EMAIL (ID_CLINICA, DS_EMAIL)</c>.
    ///
    /// <para>⚠️ <b>Sem esta checagem o sintoma seria <c>ORA-00001</c> — <c>500</c>, não
    /// <c>422</c>.</b> E nenhum teste desta suíte veria a diferença: o provider InMemory
    /// <b>não valida índice único</b>, então o <c>INSERT</c> duplicado passa verde aqui e só
    /// morre contra o Oracle real. A regra vive no service justamente porque o instrumento de
    /// teste disponível não alcança o banco que a impõe.</para>
    /// </summary>
    private async Task GarantirEmailDisponivelAsync(long idClinica, string email)
    {
        var existente = await _repository.BuscarPorEmailNaClinicaAsync(idClinica, email);
        if (existente is null)
            return;

        // A mensagem diz se o conflito é com usuário desativado porque, para quem administra,
        // "e-mail em uso" com a lista de usuários na tela e ninguém usando aquele e-mail é
        // indistinguível de bug. O soft delete mantém a linha e ela continua ocupando a UK.
        var detalhe = existente.StAtiva
            ? "Já existe um usuário ativo com este e-mail nesta clínica."
            : "Já existe um usuário DESATIVADO com este e-mail nesta clínica. "
              + "O e-mail continua reservado (o registro não é apagado, apenas inativado).";

        throw new RegraDeNegocioException(detalhe);
    }

    /// <summary>
    /// Valida o vínculo opcional com <c>VETERINARIO</c>.
    ///
    /// <para>🔴 <b>A <c>FK_USUARIO_CLINICA_VET</c> da V17 referencia só
    /// <c>VETERINARIO(ID_VETERINARIO)</c>, sem compor com <c>ID_CLINICA</c></b> — o Oracle
    /// aceita, sem reclamar, um usuário da clínica A apontando o veterinário da clínica B.
    /// Esse estado <b>já foi encontrado</b> nesta trilha (achado da revisão G2 da FD-03, que
    /// deixou o cenário semeado em <c>KuraApiFactory.EmailVinculoCruzado</c>). A única defesa
    /// é esta comparação.</para>
    ///
    /// <para>A busca é <c>BuscarPorIdIgnorandoFiltrosAsync</c>, e não <c>GetByIdAsync</c>, de
    /// propósito: com o query filter ligado o veterinário alheio já viria <c>null</c> e a
    /// comparação abaixo seria inalcançável — ou seja, apagá-la não quebraria teste nenhum, e
    /// a garantia deixaria de ser verificável por mutação.</para>
    ///
    /// <para>Veterinário inativo também é recusado: vincular a um registro soft-deletado
    /// criaria autoria apontando para alguém que a clínica considera fora do quadro.</para>
    /// </summary>
    private async Task GarantirVeterinarioDaClinicaAsync(long? idVeterinario, long idClinica)
    {
        if (idVeterinario is not { } id)
            return;

        var veterinario = await _veterinarioRepository.BuscarPorIdIgnorandoFiltrosAsync(id);

        if (veterinario is null || !veterinario.StAtiva || veterinario.IdClinica != idClinica)
            throw new RegraDeNegocioException(
                $"Veterinário {id} não pertence a esta clínica ou não está ativo.");
    }

    /// <summary>
    /// Invariante do último gestor — ver o argumento completo na documentação da classe.
    /// A contagem exclui o próprio alvo porque ele é quem está prestes a sair do quadro de
    /// gestores ativos (por rebaixamento ou por desativação).
    /// </summary>
    private async Task GarantirQueSobraGestorAsync(long idClinica, long idAlvo)
    {
        var gestoresRestantes =
            await _repository.ContarGestoresAtivosAsync(idClinica, excetoId: idAlvo);

        if (gestoresRestantes == 0)
            throw new RegraDeNegocioException(MensagemUltimoGestor);
    }

    /// <summary>
    /// <c>Trim()</c> e <b>nada de <c>ToLower()</c></b>. O login
    /// (<c>UsuarioClinicaRepository.BuscarAtivosPorEmailAsync</c>) compara o e-mail por
    /// igualdade exata, e o Oracle é sensível a caixa nessa comparação: normalizar a caixa só
    /// na escrita criaria usuário que nasce com o e-mail em minúsculas e nunca mais é
    /// encontrado por quem digita como cadastrou. Mudar isso é mudar os dois lados de uma vez,
    /// com migração do dado existente — fora do escopo desta task.
    /// </summary>
    private static string NormalizarEmail(string email) => email.Trim();

    /// <summary>
    /// Espelha <c>CHK_USUARIO_CLINICA_PERFIL</c> da V17.
    ///
    /// <para>⚠️ Redundante com <c>UsuarioClinicaCreateValidator</c>/<c>UpdateValidator</c> de
    /// propósito: o validator protege o contrato HTTP, este método protege a REGRA — qualquer
    /// chamador futuro do service (um seed, um comando de CLI, outro service) não passa pelo
    /// pipeline do FluentValidation, e a constraint do banco não existe no InMemory da suíte.
    /// Um perfil inválido chegaria ao Oracle e morreria com <c>ORA-02290</c>, ou seja,
    /// <c>500</c>.</para>
    /// </summary>
    private static string NormalizarPerfil(string perfil)
    {
        var valor = perfil.Trim().ToUpperInvariant();

        if (valor != PerfisUsuarioClinica.Gestor && valor != PerfisUsuarioClinica.Veterinario)
            throw new RegraDeNegocioException(
                $"Perfil inválido: '{perfil}'. Valores aceitos: "
                + $"{PerfisUsuarioClinica.Gestor}, {PerfisUsuarioClinica.Veterinario}.");

        return valor;
    }

    /// <summary>
    /// ⚠️ <c>DsSenhaHash</c> NÃO é copiado para o DTO de resposta, e o DTO nem tem o campo.
    /// Ver <see cref="UsuarioClinicaResponseDto"/>.
    /// </summary>
    private static UsuarioClinicaResponseDto ToResponse(UsuarioClinica u) => new()
    {
        Id = u.Id,
        IdClinica = u.IdClinica,
        IdVeterinario = u.IdVeterinario,
        DsEmail = u.DsEmail,
        TpPerfil = u.TpPerfil,
        StAtiva = u.StAtiva,
        DtCriacao = u.DtCriacao,
        DtAtualizacao = u.DtAtualizacao,
    };
}
