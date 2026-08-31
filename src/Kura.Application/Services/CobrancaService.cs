namespace Kura.Application.Services;

using Kura.Application.DTOs.Cobranca;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

/// <summary>
/// FD-10 (ciclo FIN) — lançamento de <see cref="Cobranca"/> num evento clínico existente.
/// Até aqui a tabela <c>COBRANCA</c> existia (V18 + FD-08: domínio, mapeamento e isolamento
/// de tenant) sem nenhum caminho de produto que escrevesse uma linha nela.
///
/// <para>
/// 🔴 <b>ONDE ISTO CABE NO FLUXO, que é o que a task realmente entrega.</b> O princípio de
/// desenho do ciclo é que <i>o dado do gestor nasce como subproduto do fluxo do veterinário,
/// nunca como trabalho extra para ele</i>. Por isso a cobrança é <b>subrecurso do evento
/// clínico</b> (<c>POST /api/v1/eventos-clinicos/{id}/cobrancas</c>), lançada no gesto de
/// fechamento do atendimento que o veterinário já faz, e por isso o corpo mínimo é
/// <c>{"idServicoPreco": N}</c> — um toque na tabela de preços, sem digitar valor. Um
/// endpoint que exigisse o gestor redigitando tudo depois seria a inversão exata desse
/// princípio.
/// </para>
///
/// <para>
/// 🔴 <b>O VALOR É COPIADO, NUNCA LIDO POR FK — é o invariante central desta task.</b>
/// Quando o lançamento aponta um <see cref="ServicoPreco"/>, <c>VL_COBRADO</c> recebe uma
/// <b>cópia</b> do <c>VL_PRECO</c> daquele instante. A partir daí a cobrança não olha mais
/// para o serviço: remarcar a tabela de preços amanhã <b>não</b> altera cobrança já lançada.
/// A alternativa (resolver o valor por FK na leitura) faria o histórico financeiro se
/// reescrever sozinho a cada correção de preço — e os KPI da FD-11 mudariam de resposta sem
/// que nada financeiro tivesse acontecido. Provado em
/// <c>CobrancaServiceTests.Cobranca_lancada_NAO_muda_quando_o_preco_de_tabela_muda</c>.
/// </para>
///
/// <para>
/// 🔴 <b>Escopo de tenant é comparação EXPLÍCITA, em dois pontos.</b> O evento vem de
/// <c>IEventoClinicoRepository.BuscarPorIdNaClinicaAsync</c> e o serviço de
/// <c>IServicoPrecoRepository.BuscarPorIdNaClinicaAsync</c>, ambos com o predicado escrito à
/// mão. Não é redundância com o query filter: as FKs do Oracle
/// (<c>FK_COBRANCA_EVENTO</c>, <c>FK_COBRANCA_SERVICO</c>) referenciam só a PK,
/// <b>sem compor com <c>ID_CLINICA</c></b> — o banco aceita alegremente uma cobrança da
/// clínica A pendurada num evento da clínica B. A comparação é a única defesa, e é a mesma
/// forma do achado F1 da FD-03.
/// </para>
///
/// <para>
/// <b>Por que 404 para o evento e 422 para o serviço.</b> O evento é o recurso endereçado
/// pela <b>rota</b>: id inexistente e id de outra clínica devolvem o mesmo <c>404</c>,
/// indistinguíveis de propósito (mesmo padrão da FD-09 — um <c>403</c> aqui confirmaria a
/// existência do id alheio, que é enumeração de outro tenant de graça). O serviço de preço é
/// uma <b>referência dentro do corpo</b>: a rota existe e o pedido é bem-formado, o que falha
/// é uma regra sobre o conteúdo — <c>422</c>, com a mesma mensagem para "não existe" e "é de
/// outra clínica", pelo mesmo motivo de não vazar existência.
/// </para>
///
/// <para>
/// ⛔ <b>Escopo negativo, declarado (D-1/D-6 e o backlog da FD-10):</b> sem parcelamento, sem
/// múltiplas formas de pagamento na mesma cobrança, <b>sem estorno</b>, sem gateway, sem
/// status de processamento, sem imposto/repasse/margem. E sem agregação: receita, ticket
/// médio e mix por serviço são a FD-11.
/// </para>
/// </summary>
public sealed class CobrancaService : ICobrancaService
{
    public const string MensagemServicoIndisponivel =
        "Serviço de preço não encontrado nesta clínica. Confira o idServicoPreco, ou lance a "
        + "cobrança com vlCobrado avulso.";

    public const string MensagemServicoDesativado =
        "Este serviço de preço está DESATIVADO e não pode originar novos lançamentos. "
        + "Reative-o na tabela de preços, ou lance a cobrança com vlCobrado avulso.";

    private readonly ICobrancaRepository _cobrancaRepository;
    private readonly IEventoClinicoRepository _eventoRepository;
    private readonly IServicoPrecoRepository _servicoPrecoRepository;
    private readonly IUnitOfWork _uow;
    private readonly IClinicaContext _clinicaContext;

    public CobrancaService(
        ICobrancaRepository cobrancaRepository,
        IEventoClinicoRepository eventoRepository,
        IServicoPrecoRepository servicoPrecoRepository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext)
    {
        _cobrancaRepository = cobrancaRepository;
        _eventoRepository = eventoRepository;
        _servicoPrecoRepository = servicoPrecoRepository;
        _uow = uow;
        _clinicaContext = clinicaContext;
    }

    public async Task<CobrancaResponseDto> LancarAsync(long idEventoClinico, CobrancaCreateDto dto)
    {
        var idClinica = _clinicaContext.IdClinica;

        // 🔴 Trava de tenant nº 1. Evento de outra clínica é indistinguível de inexistente.
        var evento = await _eventoRepository.BuscarPorIdNaClinicaAsync(idEventoClinico, idClinica)
            ?? throw new EntidadeNaoEncontradaException("EventoClinico", idEventoClinico);

        // 🔴 Trava de tenant nº 2 + regra de disponibilidade do catálogo.
        var servico = await ResolverServicoAsync(dto.IdServicoPreco, idClinica);

        var cobranca = new Cobranca
        {
            IdEventoClinico = evento.Id,

            // A clínica sai do JWT, e o evento acabou de ser provado desta mesma clínica —
            // ou seja, ID_CLINICA da cobrança é coerente com o do evento por construção, que
            // é o que o comentário da coluna na V18 delega ao service.
            IdClinica = idClinica,

            IdServicoPreco = servico?.Id,
            VlCobrado = ResolverValor(dto, servico),
            DsFormaPagamento = NormalizarFormaPagamento(dto.DsFormaPagamento),
            DtCobranca = dto.DtCobranca ?? DateTime.UtcNow,
            StAtiva = true,
        };

        await _cobrancaRepository.AddAsync(cobranca);
        await _uow.CommitAsync();

        return ToResponse(cobranca);
    }

    public async Task<IEnumerable<CobrancaResponseDto>> ListarDoEventoAsync(long idEventoClinico)
    {
        var idClinica = _clinicaContext.IdClinica;

        // Mesma trava da escrita: listar cobranças de um evento alheio devolve 404, e não
        // uma lista vazia. Lista vazia mentiria — seria indistinguível de "este atendimento
        // não teve cobrança", que é uma afirmação sobre um atendimento que não é seu.
        _ = await _eventoRepository.BuscarPorIdNaClinicaAsync(idEventoClinico, idClinica)
            ?? throw new EntidadeNaoEncontradaException("EventoClinico", idEventoClinico);

        var itens = await _cobrancaRepository.ListarDoEventoNaClinicaAsync(
            idEventoClinico, idClinica);

        return itens.Select(ToResponse);
    }

    /// <summary>
    /// 🔴 <b>F1 da revisão G2: o <c>idEventoClinico</c> da ROTA participa da busca.</b> Antes
    /// esta leitura filtrava só por id + clínica, e o segmento do meio da rota era aceito com
    /// qualquer valor — evento de outro tenant e evento inexistente devolviam <c>200</c>.
    ///
    /// <para><b>Por que a checagem mora no predicado do repositório, e não numa segunda
    /// consulta ao evento como faz <see cref="ListarDoEventoAsync"/>.</b> São perguntas
    /// diferentes: o <c>Listar</c> precisa saber se o EVENTO é seu, porque a resposta natural
    /// de um evento alheio seria uma lista vazia — indistinguível de "este atendimento não
    /// teve cobrança", uma afirmação sobre um atendimento que não é seu. Aqui a resposta
    /// natural já é a linha, e exigir que ela esteja pendurada NAQUELE evento é estritamente
    /// mais forte do que confirmar que o evento existe: cobre de uma vez o evento alheio, o
    /// evento inexistente e o par (evento, cobrança) que simplesmente não casa. Uma consulta,
    /// não duas.</para>
    /// </summary>
    public async Task<CobrancaResponseDto> ObterPorIdAsync(long idEventoClinico, long id) =>
        ToResponse(await _cobrancaRepository.BuscarNoEventoDaClinicaAsync(
            id, idEventoClinico, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("Cobranca", id));

    /// <summary>
    /// 🔴 <b>A cópia acontece aqui, e em lugar nenhum mais.</b> Valor informado ganha do
    /// preço de tabela de propósito: desconto de balcão é lançamento legítimo (D-2), e o
    /// serviço continua gravado como <b>origem</b>. Sem valor informado, copia-se o
    /// <c>VlPreco</c> do serviço <b>deste instante</b>.
    ///
    /// <para>O <c>InvalidOperationException</c> final é inalcançável pelo caminho HTTP — o
    /// validator recusa o corpo sem nenhuma das duas origens com <c>400</c>. Ele existe para
    /// que um chamador futuro que instancie o service direto não grave silenciosamente
    /// <c>0</c>: "cobrança de graça" é um lançamento legítimo demais para nascer de um
    /// default.</para>
    /// </summary>
    private static decimal ResolverValor(CobrancaCreateDto dto, ServicoPreco? servico)
    {
        if (dto.VlCobrado.HasValue)
            return dto.VlCobrado.Value;

        if (servico is not null)
            return servico.VlPreco;

        throw new InvalidOperationException(
            "Lançamento sem valor informado e sem serviço de preço: não há origem de valor. "
            + "Este caso é barrado por CobrancaCreateValidator antes de chegar aqui.");
    }

    private async Task<ServicoPreco?> ResolverServicoAsync(long? idServicoPreco, long idClinica)
    {
        if (idServicoPreco is null)
            return null;

        var servico = await _servicoPrecoRepository.BuscarPorIdNaClinicaAsync(
            idServicoPreco.Value, idClinica);

        // Mesma mensagem para "não existe" e "é de outra clínica" — ver a documentação da
        // classe.
        if (servico is null)
            throw new RegraDeNegocioException(MensagemServicoIndisponivel);

        // Desativado é um NÃO explícito, não um sucesso silencioso: o preço de um serviço
        // que saiu do catálogo não é mais a referência comercial da clínica. O caminho de
        // volta existe dentro do produto (reativação, FD-09) e a alternativa também
        // (vlCobrado avulso) — não é porta de mão única.
        if (!servico.StAtiva)
            throw new RegraDeNegocioException(MensagemServicoDesativado);

        return servico;
    }

    private static string? NormalizarFormaPagamento(string? forma)
    {
        if (string.IsNullOrWhiteSpace(forma))
            return null;

        return forma.Trim();
    }

    private static CobrancaResponseDto ToResponse(Cobranca cobranca) => new()
    {
        Id = cobranca.Id,
        IdEventoClinico = cobranca.IdEventoClinico,
        IdClinica = cobranca.IdClinica,
        IdServicoPreco = cobranca.IdServicoPreco,
        VlCobrado = cobranca.VlCobrado,
        DsFormaPagamento = cobranca.DsFormaPagamento,
        DtCobranca = cobranca.DtCobranca,
        StAtiva = cobranca.StAtiva,
        DtCriacao = cobranca.DtCriacao,
        DtAtualizacao = cobranca.DtAtualizacao,
    };
}
