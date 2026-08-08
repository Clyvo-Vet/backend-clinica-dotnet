namespace Kura.Application.Services;

using Kura.Application.DTOs.Luna;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class LunaService : ILunaService
{
    private const int MaxIntervaloDias = 90;

    // DS_CONTEUDO é VARCHAR2(4000) NOT NULL (V15__interacao_canal.sql). Truncar em vez
    // de rejeitar: o objetivo deste backlog é parar de perder mensagem de WhatsApp
    // silenciosamente — devolver 422 para uma mensagem longa recria exatamente esse
    // sintoma (a Luna cai no except genérico e a interação nunca é persistida). Perder
    // a cauda de uma mensagem rara acima de 4000 chars é preferível a perder o registro
    // inteiro. ds_conteudo nunca é vazio aqui (InteractionRequestValidator.NotEmpty()),
    // então truncar não reintroduz o bug '' -> NULL.
    private const int MaxTamanhoConteudo = 4000;

    // DS_DESCRICAO é VARCHAR2(2000) NOT NULL (V9__schema_drift_clinico.sql) e é onde
    // sintomas[]/nr_score/ds_recomendacao são compostos (decisão 2, ver RegistrarTriagemAsync).
    private const int MaxTamanhoDescricaoTriagem = 2000;

    private readonly ITriagemLunaRepository _triagemRepository;
    private readonly IRepository<InteracaoCanal> _interacaoRepository;
    private readonly ITutorRepository _tutorRepository;
    private readonly IUnitOfWork _uow;

    public LunaService(
        ITriagemLunaRepository triagemRepository,
        IRepository<InteracaoCanal> interacaoRepository,
        ITutorRepository tutorRepository,
        IUnitOfWork uow)
    {
        _triagemRepository = triagemRepository;
        _interacaoRepository = interacaoRepository;
        _tutorRepository = tutorRepository;
        _uow = uow;
    }

    public async Task<RelatorioTriagensDto> GerarRelatorioAsync(DateTime dataInicio, DateTime dataFim)
    {
        if (dataFim < dataInicio)
            throw new RegraDeNegocioException("DataFim não pode ser anterior à DataInicio.");

        if ((dataFim - dataInicio).TotalDays > MaxIntervaloDias)
            throw new RegraDeNegocioException($"Intervalo máximo de {MaxIntervaloDias} dias.");

        var triagens = await _triagemRepository.GetByIntervaloAsync(dataInicio, dataFim);

        var porUrgencia = triagens
            .GroupBy(t => t.DsNivelUrgencia)
            .ToDictionary(g => g.Key, g => g.Count());

        return new RelatorioTriagensDto
        {
            DataInicio = dataInicio,
            DataFim = dataFim,
            TotalTriagens = triagens.Count,
            PorUrgencia = porUrgencia,
            EncaminhadasParaVet = triagens.Count(t => t.StEncaminhadoVet)
        };
    }

    public async Task<InteractionResponseDto> RegistrarInteracaoAsync(InteractionRequestDto dto)
    {
        // Decisão 1 (TASK-67, brief §"Mapeamentos que exigem decisão explícita"):
        // ID_CLINICA é NOT NULL em INTERACAO_CANAL e o Pydantic InteractionRequestDTO
        // declara id_tutor como int | None — a Luna PODE mandar null. Sem id_tutor não
        // há como derivar id_clinica, então rejeitamos com 422 (RegraDeNegocioException
        // — mesmo mapeamento de status que o resto deste projeto usa para regra de
        // negócio violada, ex.: IotController leitura fora de faixa). Nunca 500: não
        // deixamos o Oracle reclamar do NOT NULL. Mensagem sem PII de propósito — não
        // interpola nem telefone nem ds_conteudo (ver InteracaoCanalLgpdTests).
        if (dto.IdTutor is null)
        {
            throw new RegraDeNegocioException(
                "id_tutor é obrigatório para registrar uma interação: sem ele não é " +
                "possível derivar id_clinica (INTERACAO_CANAL.ID_CLINICA é NOT NULL).");
        }

        var tutor = await _tutorRepository.GetByIdAsync(dto.IdTutor.Value)
            ?? throw new EntidadeNaoEncontradaException("Tutor", dto.IdTutor.Value);

        var conteudo = dto.DsConteudo.Length > MaxTamanhoConteudo
            ? dto.DsConteudo[..MaxTamanhoConteudo]
            : dto.DsConteudo;

        var interacao = new InteracaoCanal
        {
            IdClinica = tutor.IdClinica,
            IdTutor = tutor.Id,
            DsCanal = dto.DsCanal,
            DsDirecao = dto.DsDirecao,
            DsConteudo = conteudo,
            DtRecebimento = dto.DtRecebimento,
            DsMetadados = dto.DsMetadados?.GetRawText()
        };

        await _interacaoRepository.AddAsync(interacao);
        await _uow.CommitAsync();

        return new InteractionResponseDto { IdInteracao = interacao.Id };
    }

    public async Task<TriageResponseDto> RegistrarTriagemAsync(TriageRequestDto dto)
    {
        var interacao = await _interacaoRepository.GetByIdAsync(dto.IdInteracao)
            ?? throw new EntidadeNaoEncontradaException("InteracaoCanal", dto.IdInteracao);

        var tutor = await _tutorRepository.GetByIdAsync(dto.IdTutor)
            ?? throw new EntidadeNaoEncontradaException("Tutor", dto.IdTutor);

        var triagem = new TriagemLuna
        {
            IdClinica = tutor.IdClinica,
            IdTutor = tutor.Id,
            IdInteracao = interacao.Id,
            DsNivelUrgencia = dto.DsUrgencia,
            DsDescricao = ComporDescricao(dto.Sintomas, dto.NrScore, dto.DsRecomendacao),
            // Decisão 3 (TASK-67): DT_TRIAGEM é NOT NULL e não vem do payload — coalesce
            // no service para "agora" (mesmo padrão TASK-56/60: nunca NotEmpty() no
            // validator pra consertar shape de coluna que o cliente não popula).
            DtTriagem = DateTime.UtcNow
        };

        await _triagemRepository.AddAsync(triagem);
        await _uow.CommitAsync();

        return new TriageResponseDto { IdTriagem = triagem.Id };
    }

    /// <summary>
    /// Decisão 2 (TASK-67): sintomas[]/nr_score/ds_recomendacao não têm coluna própria
    /// em TRIAGEM_LUNA (schema V9, anterior a esta feature). Composto em DS_DESCRICAO
    /// (VARCHAR2(2000)) em vez de pedir uma V16 — mantém os 3 endpoints desbloqueados
    /// nesta task sem tocar Flyway (que vive no backend-tutor-java). Ressalva no
    /// relatório: isso é *lossy* para consulta estruturada (ex.: filtrar triagens por
    /// nr_score) — recomendação é uma V16 futura com colunas próprias se esse tipo de
    /// consulta vier a ser necessário.
    /// </summary>
    private static string ComporDescricao(List<string> sintomas, int score, string recomendacao)
    {
        var sintomasTexto = sintomas.Count > 0 ? string.Join(", ", sintomas) : "não informado";
        var texto = $"Sintomas: {sintomasTexto}. Score: {score}. Recomendação: {recomendacao}";
        return texto.Length > MaxTamanhoDescricaoTriagem
            ? texto[..MaxTamanhoDescricaoTriagem]
            : texto;
    }
}
