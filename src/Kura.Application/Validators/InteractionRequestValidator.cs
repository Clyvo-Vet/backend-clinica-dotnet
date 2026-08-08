namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Luna;

/// <summary>
/// TASK-67: DS_CANAL/DS_DIRECAO têm CHECK constraint no Oracle
/// (V15__interacao_canal.sql: CHK_INTERACAO_CANAL/CHK_INTERACAO_DIRECAO) — um valor
/// fora da lista estoura ORA-02290 (500) no INSERT se chegar até lá. Validado aqui
/// para responder 400 em vez de deixar o Oracle reclamar (mesma classe de bug do
/// FIX_4, versão CHECK em vez de NOT NULL).
///
/// DsConteudo.NotEmpty() aqui NÃO repete o erro da TASK-47: ds_conteudo é conteúdo de
/// mensagem obrigatório no contrato Pydantic (ds_conteudo: str, sem default) — não é
/// um campo legitimamente opcional num formulário que precisa de coalesce no service.
/// </summary>
public sealed class InteractionRequestValidator : AbstractValidator<InteractionRequestDto>
{
    public InteractionRequestValidator()
    {
        RuleFor(x => x.DsCanal)
            .Must(c => c is "WHATSAPP" or "EMAIL" or "SMS")
            .WithMessage("'ds_canal' deve ser WHATSAPP, EMAIL ou SMS.");

        RuleFor(x => x.DsDirecao)
            .Must(d => d is "INBOUND" or "OUTBOUND")
            .WithMessage("'ds_direcao' deve ser INBOUND ou OUTBOUND.");

        RuleFor(x => x.DsConteudo)
            .NotEmpty()
            .WithMessage("'ds_conteudo' não pode ser vazio.");

        RuleFor(x => x.DtRecebimento)
            .NotEmpty()
            .WithMessage("'dt_recebimento' é obrigatório.");
    }
}
