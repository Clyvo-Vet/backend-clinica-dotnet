namespace Kura.Application.Services;

using Kura.Application.DTOs.ServicoPreco;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

/// <summary>
/// CRUD de <see cref="ServicoPreco"/> (FD-09, ciclo FIN) — a tabela de preços da clínica
/// ganha caminho de produto. Até aqui a entidade existia (FD-08: domínio, mapeamento e
/// isolamento de tenant) sem nenhum endpoint que a alimentasse.
///
/// <para>
/// 🔴 <b>TODA escrita é escopada pelo <c>clinicaId</c> do JWT, e nenhum DTO desta task tem
/// campo <c>IdClinica</c>.</b> Lição da <c>FD-05</c>, onde
/// <c>VeterinarioService.CreateAsync</c> grava <c>dto.IdClinica</c> sem comparar com o token.
/// Aceitar o campo e comparar deixa a garantia dependendo de alguém lembrar da comparação em
/// cada caminho novo; aqui o campo não existe.
/// </para>
///
/// <para>
/// 🔴 <b>DECISÃO DE PRODUTO — NOME DUPLICADO É RECUSADO ENTRE OS <i>ATIVOS</i>, E SÓ ENTRE
/// ELES.</b> Criar (ou renomear para) um nome que já pertence a um serviço <b>ativo</b> desta
/// clínica devolve <c>422</c>. Um serviço <b>desativado</b> com o mesmo nome <b>não bloqueia
/// nada</b>: recadastrar é permitido e é justamente o caminho que a FD-07 preservou ao
/// deliberadamente NÃO criar <c>UNIQUE (ID_CLINICA, NM_SERVICO)</c>.
/// </para>
///
/// <para>
/// <b>Por que não checar contra todas as linhas.</b> Seria a versão em código da unique que o
/// schema evitou de propósito, e reintroduziria o defeito <c>A-3</c> da FD-04: lá, o soft
/// delete mantinha a linha ocupando <c>UK_USUARIO_CLINICA_EMAIL</c> e o e-mail ficava
/// <b>reservado para sempre</b>, sem caminho de volta dentro do produto. "Consulta de rotina"
/// desativada hoje não pode impedir "Consulta de rotina" amanhã.
/// </para>
///
/// <para>
/// <b>E por que checar entre os ativos, em vez de não checar nada.</b> Duas linhas ativas com
/// o mesmo nome são indistinguíveis para quem lança cobrança na FD-10 — a escolha vira
/// sorteio, e o erro só aparece na fatura. Diferente da unique total, esta recusa <b>não é
/// porta de mão única</b>: há duas saídas dentro do produto (renomear um dos dois, ou
/// desativar o antigo e recadastrar), e as duas estão cobertas por teste.
/// </para>
///
/// <para>
/// ⚠️ <b>O piso de zero do preço NÃO mora aqui, e sim em
/// <c>ServicoPrecoCreateValidator</c>/<c>ServicoPrecoUpdateValidator</c></b> — é contrato de
/// entrada (<c>400</c>), não regra de negócio. O que ele evita é concreto: o Oracle tem
/// <c>CHK_SERVICO_PRECO_VALOR CHECK (VL_PRECO &gt;= 0)</c> e o InMemory da suíte não aplica
/// CHECK nenhuma, então sem validator um preço negativo passaria verde no teste e viraria
/// <c>ORA-02290</c>/<c>500</c> em produção.
/// </para>
/// </summary>
public sealed class ServicoPrecoService : IServicoPrecoService
{
    public const string MensagemNomeDuplicado =
        "Já existe um serviço ATIVO com este nome nesta clínica. Renomeie um dos dois, ou "
        + "desative o antigo antes de recadastrar.";

    public const string MensagemServicoDesativado =
        "Este serviço está DESATIVADO e alterações não têm efeito enquanto ele estiver assim. "
        + "Reative-o primeiro (operação de reativação deste mesmo recurso) e refaça a alteração.";

    public const string MensagemReativacaoComNomeOcupado =
        "Não é possível reativar: já existe um serviço ATIVO com este nome nesta clínica. "
        + "Renomeie o outro antes de reativar este.";

    private readonly IServicoPrecoRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly IClinicaContext _clinicaContext;

    public ServicoPrecoService(
        IServicoPrecoRepository repository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext)
    {
        _repository = repository;
        _uow = uow;
        _clinicaContext = clinicaContext;
    }

    public async Task<IEnumerable<ServicoPrecoResponseDto>> ListarAsync(bool incluirInativos = false)
    {
        var itens = await _repository.ListarDaClinicaAsync(_clinicaContext.IdClinica, incluirInativos);
        return itens.Select(ToResponse);
    }

    public async Task<ServicoPrecoResponseDto> ObterPorIdAsync(long id) =>
        ToResponse(await ObterOuFalharAsync(id));

    public async Task<ServicoPrecoResponseDto> CriarAsync(ServicoPrecoCreateDto dto)
    {
        var idClinica = _clinicaContext.IdClinica;
        var nome = NormalizarNome(dto.NmServico);

        await GarantirNomeDisponivelAsync(idClinica, nome);

        var servico = new ServicoPreco
        {
            // 🔴 A clínica sai do JWT. Não existe caminho por onde o corpo da requisição
            // influencie esta linha — ver a documentação da classe e do DTO.
            IdClinica = idClinica,
            NmServico = nome,
            VlPreco = dto.VlPreco,
            StAtiva = true,
        };

        await _repository.AddAsync(servico);
        await _uow.CommitAsync();

        return ToResponse(servico);
    }

    public async Task<ServicoPrecoResponseDto> AtualizarAsync(long id, ServicoPrecoUpdateDto dto)
    {
        var idClinica = _clinicaContext.IdClinica;
        var servico = await ObterOuFalharAsync(id);

        // A-3 da FD-04, aplicada aqui: nada de sucesso silencioso sobre item desativado. Um
        // 200 que não muda o que o gestor vê na lista é indistinguível de bug.
        GarantirServicoAtivo(servico);

        var nome = NormalizarNome(dto.NmServico);

        if (!string.Equals(nome, servico.NmServico, StringComparison.OrdinalIgnoreCase))
            await GarantirNomeDisponivelAsync(idClinica, nome, excetoId: servico.Id);

        servico.NmServico = nome;
        servico.VlPreco = dto.VlPreco;
        servico.DtAtualizacao = DateTime.UtcNow;

        _repository.Update(servico);
        await _uow.CommitAsync();

        return ToResponse(servico);
    }

    public async Task DesativarAsync(long id)
    {
        var servico = await ObterOuFalharAsync(id);

        // Já desativado: nada a fazer. Repetir o soft delete só reescreveria DT_ATUALIZACAO.
        if (!servico.StAtiva)
            return;

        _repository.SoftDelete(servico);
        await _uow.CommitAsync();
    }

    /// <summary>
    /// Reativa um serviço desativado desta clínica.
    ///
    /// <para>Existe pelo mesmo motivo que <c>UsuariosClinicaController.Reativar</c> (A-3 da
    /// FD-04): sem ele, desativar seria porta de mão única para o <b>id</b>. Recadastrar
    /// resolve o nome, mas cria uma linha NOVA — e <c>COBRANCA.ID_SERVICO_PRECO</c> aponta
    /// para o id, então o histórico financeiro deixaria de reencontrar o serviço que voltou.
    /// </para>
    /// </summary>
    public async Task<ServicoPrecoResponseDto> ReativarAsync(long id)
    {
        var idClinica = _clinicaContext.IdClinica;
        var servico = await ObterOuFalharAsync(id);

        // Idempotente: reativar o que já está ativo devolve o estado atual.
        if (servico.StAtiva)
            return ToResponse(servico);

        // Enquanto ele esteve desativado, o nome pode ter sido recadastrado — que é
        // exatamente o que a ausência da UNIQUE permite. Reativar às cegas deixaria DOIS
        // ativos com o mesmo nome, o estado que GarantirNomeDisponivelAsync recusa na criação.
        var outro = await _repository.BuscarAtivoPorNomeNaClinicaAsync(
            idClinica, servico.NmServico, excetoId: servico.Id);

        if (outro is not null)
            throw new RegraDeNegocioException(MensagemReativacaoComNomeOcupado);

        servico.StAtiva = true;
        servico.DtAtualizacao = DateTime.UtcNow;

        _repository.Update(servico);
        await _uow.CommitAsync();

        return ToResponse(servico);
    }

    private static void GarantirServicoAtivo(ServicoPreco servico)
    {
        if (!servico.StAtiva)
            throw new RegraDeNegocioException(MensagemServicoDesativado);
    }

    private async Task<ServicoPreco> ObterOuFalharAsync(long id) =>
        await _repository.BuscarPorIdNaClinicaAsync(id, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("ServicoPreco", id);

    /// <summary>
    /// 🔴 Só consulta os <b>ATIVOS</b> — ver a decisão de produto na documentação da classe.
    /// Estender esta busca aos inativos "por consistência" reintroduz o defeito A-3 da FD-04.
    /// </summary>
    private async Task GarantirNomeDisponivelAsync(
        long idClinica, string nome, long? excetoId = null)
    {
        var existente = await _repository.BuscarAtivoPorNomeNaClinicaAsync(
            idClinica, nome, excetoId);

        if (existente is not null)
            throw new RegraDeNegocioException(MensagemNomeDuplicado);
    }

    private static string NormalizarNome(string nome) => nome.Trim();

    private static ServicoPrecoResponseDto ToResponse(ServicoPreco servico) => new()
    {
        Id = servico.Id,
        IdClinica = servico.IdClinica,
        NmServico = servico.NmServico,
        VlPreco = servico.VlPreco,
        StAtiva = servico.StAtiva,
        DtCriacao = servico.DtCriacao,
        DtAtualizacao = servico.DtAtualizacao,
    };
}
