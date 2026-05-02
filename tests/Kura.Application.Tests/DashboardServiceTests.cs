namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;

public class DashboardServiceTests
{
    private readonly Mock<IEventoClinicoRepository> _eventoMock = new();
    private readonly Mock<IRepository<AlertaTemperatura>> _alertaMock = new();
    private readonly Mock<IRepository<Pet>> _petMock = new();
    private readonly Mock<IRepository<Vacina>> _vacinaMock = new();
    private readonly Mock<IAgendamentoRepository> _agendamentoMock = new();
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _sut = new DashboardService(
            _eventoMock.Object, _alertaMock.Object,
            _petMock.Object, _vacinaMock.Object,
            _agendamentoMock.Object);
    }

    [Fact]
    public async Task GetHojeAsync_RetornaDtoComMetricas()
    {
        var hoje = DateTime.UtcNow.Date;
        _eventoMock.Setup(r => r.GetByFiltersAsync(null, null, null, null, null))
            .ReturnsAsync(new List<EventoClinico>
            {
                new() { Id = 1, IdPet = 10, IdVeterinario = 1, IdTipoEvento = 1,
                         DtEvento = hoje, DsObservacao = "ok", IdClinica = 1 }
            });
        _alertaMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<AlertaTemperatura>
            {
                new() { Id = 1, StResolvido = 'N', DsTipoAlerta = "T", VlLimite = 8, DsMensagem = "M", IdLeituraTemperatura = 1 }
            });
        _agendamentoMock.Setup(r => r.GetProximosDoDiaAsync(It.IsAny<DateTime>(), 3))
            .ReturnsAsync(new List<Agendamento>());

        var result = await _sut.GetHojeAsync();

        result.TotalConsultasHoje.Should().Be(1);
        result.TotalAlertasAtivos.Should().Be(1);
    }

    [Fact]
    public async Task GetAlertasAsync_RetornaAlertasAtivosEVacinasVencendo()
    {
        var proximos30Dias = DateTime.UtcNow.AddDays(15).Date;
        _alertaMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<AlertaTemperatura, bool>>>()))
            .ReturnsAsync(new List<AlertaTemperatura>
            {
                new() { Id = 1, StResolvido = 'N', DsTipoAlerta = "ACIMA_LIMITE", VlLimite = 8, DsMensagem = "Temp alta", IdLeituraTemperatura = 1, DtCriacao = DateTime.UtcNow }
            });
        _vacinaMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Vacina, bool>>>()))
            .ReturnsAsync(new List<Vacina>
            {
                new() { Id = 5, NmVacina = "Raiva", DtProximaDose = proximos30Dias, NrLote = "L1", DsFabricante = "F", IdEventoClinico = 1, DtCriacao = DateTime.UtcNow }
            });

        var result = (await _sut.GetAlertasAsync()).ToList();

        result.Should().HaveCount(2);
    }
}
