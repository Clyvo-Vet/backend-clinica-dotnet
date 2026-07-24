namespace Kura.Application.DTOs.Teleconsulta;

public sealed class DailyRoomResult
{
    public bool Sucesso { get; init; }
    public string? Url { get; init; }

    public static DailyRoomResult ComSucesso(string url) => new() { Sucesso = true, Url = url };
    public static DailyRoomResult Falha() => new() { Sucesso = false, Url = null };
}
