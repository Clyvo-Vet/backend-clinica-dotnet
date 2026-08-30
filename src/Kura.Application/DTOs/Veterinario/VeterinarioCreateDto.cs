namespace Kura.Application.DTOs.Veterinario;

/// <summary>
/// Corpo de <c>POST /api/v1/veterinarios</c>.
///
/// <para>
/// 🔴 <b>NÃO EXISTE CAMPO <c>IdClinica</c> AQUI, e a ausência é a correção da FD-05.</b> Até o
/// ciclo FIN este DTO carregava <c>IdClinica</c> e <c>VeterinarioService.CreateAsync</c>
/// gravava esse valor <b>sem nunca compará-lo com o <c>clinicaId</c> do JWT</b> — o controller
/// exige apenas <c>[Authorize]</c>. Consequência medida sobre HTTP real: com o token da
/// clínica 1 e <c>idClinica: 2</c> no corpo, o veterinário nascia na clínica <b>2</b>.
/// </para>
///
/// <para>
/// <b>Por que remover o campo em vez de compará-lo com o token.</b> Comparar corrige este
/// endpoint e deixa a garantia dependendo de alguém lembrar de escrever a comparação em cada
/// caminho novo, para sempre — e o modo de falha é silencioso: um <c>CreateAsync</c> futuro que
/// esqueça a linha volta a aceitar a clínica do corpo sem que nada quebre. Sem o campo, a
/// clínica não tem <b>por onde</b> entrar: ela sai de <c>IClinicaContext.IdClinica</c> e de mais
/// lugar nenhum. É o mesmo padrão que a FD-04 estabeleceu em <c>UsuarioClinicaCreateDto</c> e a
/// mesma escolha que o Felipe fez na TASK-74a (FIX_7), onde o servidor passou a <b>derivar</b> a
/// clínica em vez de exigi-la no corpo.
/// </para>
///
/// <para>
/// ⚠️ <b>Nenhum consumidor conhecido quebra.</b> Medido antes da remoção:
/// <c>DevOps-Cloud/scripts/smoke-contratos.sh</c> e <c>seed-demo.sh</c> não chamam este
/// endpoint, o <c>mobile-clinica-rn</c> não tem service de veterinário, e dentro deste
/// repositório o único chamador de <c>IVeterinarioService.CreateAsync</c> é
/// <c>VeterinariosController.Create</c>. (<c>AuthService.RegisterClinicaAsync</c> cria
/// <c>Veterinario</c> direto pela entidade, sem passar por este DTO — caminho intencionalmente
/// não tocado.)
/// </para>
///
/// <para>
/// ⚠️ <b>Um corpo que ainda mande <c>idClinica</c> NÃO é recusado — é ignorado.</b> O
/// <c>System.Text.Json</c> deste projeto despreza propriedade desconhecida (não há
/// <c>UnmappedMemberHandling.Disallow</c> configurado em lugar nenhum de <c>src/</c>). Cliente
/// antigo continua funcionando, e o campo que ele manda não influencia nada. Isso está travado
/// em <c>VeterinariosTenantHttpTests</c>.
/// </para>
/// </summary>
public sealed class VeterinarioCreateDto
{
    public string NmVeterinario { get; init; } = string.Empty;
    public string NrCrmv { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;
}
