namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.UsuarioClinica;

/// <summary>
/// CRUD de usuários da clínica (FD-04). Toda operação é escopada pela clínica do JWT
/// (<c>IClinicaContext.IdClinica</c>) — nenhum método aceita clínica por parâmetro nem por
/// corpo de requisição.
/// </summary>
public interface IUsuarioClinicaService
{
    Task<IEnumerable<UsuarioClinicaResponseDto>> ListarAsync();

    Task<UsuarioClinicaResponseDto> ObterPorIdAsync(long id);

    Task<UsuarioClinicaResponseDto> CriarAsync(UsuarioClinicaCreateDto dto);

    Task<UsuarioClinicaResponseDto> AtualizarAsync(long id, UsuarioClinicaUpdateDto dto);

    Task DefinirSenhaAsync(long id, UsuarioClinicaSenhaUpdateDto dto);

    Task DesativarAsync(long id);
}
