namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Agenda;

/// <summary>
/// FD-06 — valores que o <c>.NET</c> pode <b>escrever</b> em <c>AGENDAMENTO.ST_STATUS</c>.
///
/// <para>
/// 🔴 <b>Um validator de escrita não é fonte de verdade sobre os valores possíveis de uma
/// coluna.</b> O <c>CHECK</c> do Oracle (<c>V1__initial_schema.sql:283</c>) e o enum
/// <c>StatusAgendamento</c> do backend Java já aceitavam os <b>seis</b> valores
/// (<c>INTENCAO, AGENDADO, CONFIRMADO, REALIZADO, CANCELADO, NAO_COMPARECEU</c>) enquanto esta
/// regra aceitava dois — e o <c>mobile-clinica-rn</c> já <b>lia</b> <c>NAO_COMPARECEU</c>
/// (<c>agenda.service.ts:51</c>) sem que nada no ecossistema pudesse escrevê-lo.
/// </para>
///
/// <para>
/// ⚠️ <b>Esta lista é de DESTINOS, não de estados.</b> <c>INTENCAO</c> e <c>AGENDADO</c> ficam
/// deliberadamente de fora: são estados de <b>partida</b>, escritos pelo backend Java quando o
/// tutor cria ou reagenda. O <c>.NET</c> só faz o agendamento <b>avançar</b> — devolver um
/// agendamento para «agendado» é reabertura, não atualização de status, e não tem dono definido
/// nesta tabela compartilhada.
/// </para>
///
/// <para>
/// <b>De quais origens cada destino é alcançável</b> é a outra metade da regra, e ela mora em
/// <c>AgendaService</c> — aqui não dá para saber, porque o validator roda antes de o
/// agendamento ser lido do banco.
/// </para>
/// </summary>
public sealed class AtualizarStatusAgendamentoValidator : AbstractValidator<AtualizarStatusAgendamentoDto>
{
    /// <summary>
    /// Destinos aceitos (D-5). Exposto porque é contrato de escrita desta API — a mensagem de
    /// erro é derivada daqui, e não redigitada, para que acrescentar um valor não deixe a
    /// mensagem mentindo (a documentação que garante o que o código não faz já custou caro
    /// neste projeto).
    /// </summary>
    public static readonly IReadOnlySet<string> StatusPermitidos =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "REALIZADO",
            "CANCELADO",
            "NAO_COMPARECEU",
            "CONFIRMADO",
        };

    public AtualizarStatusAgendamentoValidator()
    {
        RuleFor(x => x.DsStatus)
            .Must(StatusPermitidos.Contains)
            .WithMessage(
                "'DsStatus' deve ser um de: "
                + string.Join(", ", StatusPermitidos.OrderBy(s => s, StringComparer.Ordinal))
                + ".");

        RuleFor(x => x.NrVersion)
            .GreaterThanOrEqualTo(0)
            .WithMessage("'NrVersion' deve ser >= 0.");
    }
}
