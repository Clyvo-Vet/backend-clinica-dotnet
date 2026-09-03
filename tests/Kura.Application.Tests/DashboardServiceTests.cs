namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;

public class DashboardServiceTests
{
    private const long IdClinicaContexto = 1;

    private readonly Mock<IEventoClinicoRepository> _eventoMock = new();
    private readonly Mock<IRepository<AlertaTemperatura>> _alertaMock = new();
    private readonly Mock<IRepository<Pet>> _petMock = new();
    private readonly Mock<IRepository<Vacina>> _vacinaMock = new();
    private readonly Mock<IAgendamentoRepository> _agendamentoMock = new();
    private readonly Mock<IClinicaContext> _clinicaContextMock = new();
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _clinicaContextMock.Setup(c => c.IdClinica).Returns(IdClinicaContexto);
        // Sem setup explícito, Moq (loose mock) devolve default(int) = 0 para
        // ContarTeleorientacoesHojeAsync -- suficiente para os testes que não avaliam esse campo.
        _sut = new DashboardService(
            _eventoMock.Object, _alertaMock.Object,
            _petMock.Object, _vacinaMock.Object,
            _agendamentoMock.Object, _clinicaContextMock.Object);
    }

    [Fact]
    public async Task GetHojeAsync_ComUmEventoEUmAlertaAtivoHoje_RetornaDtoComMetricas()
    {
        // Arrange
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
                new() { Id = 1, StResolvido = false, DsTipoAlerta = "T", VlLimite = 8, DsMensagem = "M", IdLeituraTemperatura = 1 }
            });
        _agendamentoMock.Setup(r => r.GetProximosDoDiaAsync(IdClinicaContexto, It.IsAny<DateTime>(), 3))
            .ReturnsAsync(new List<Agendamento>());

        // Act
        var result = await _sut.GetHojeAsync();

        // Assert
        result.TotalConsultasHoje.Should().Be(1);
        result.TotalAlertasAtivos.Should().Be(1);
    }

    [Fact]
    public async Task GetHojeAsync_ComDoisPetsDistintosAtendidosHoje_TotalPacientesAtendidosHojeContaOsDois()
    {
        // Arrange -- FD-17 item 2: mais de 5 eventos hoje não pode saturar em 5 como
        // UltimosPetsAtendidos satura; aqui usamos 2 para o teste ficar legível, mas o ponto é
        // que o contador não tem .Take() nenhum.
        var hoje = DateTime.UtcNow.Date;
        var ontem = hoje.AddDays(-1);
        _eventoMock.Setup(r => r.GetByFiltersAsync(null, null, null, null, null))
            .ReturnsAsync(new List<EventoClinico>
            {
                new() { Id = 1, IdPet = 10, IdVeterinario = 1, IdTipoEvento = 1, DtEvento = hoje, DsObservacao = "ok", IdClinica = 1 },
                new() { Id = 2, IdPet = 11, IdVeterinario = 1, IdTipoEvento = 1, DtEvento = hoje, DsObservacao = "ok", IdClinica = 1 },
                new() { Id = 3, IdPet = 10, IdVeterinario = 1, IdTipoEvento = 1, DtEvento = hoje, DsObservacao = "ok", IdClinica = 1 }, // mesmo pet 10, mesmo dia -- não duplica
                new() { Id = 4, IdPet = 12, IdVeterinario = 1, IdTipoEvento = 1, DtEvento = ontem, DsObservacao = "ok", IdClinica = 1 }, // ontem -- não conta
            });
        _alertaMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AlertaTemperatura>());
        _agendamentoMock.Setup(r => r.GetProximosDoDiaAsync(IdClinicaContexto, It.IsAny<DateTime>(), 3))
            .ReturnsAsync(new List<Agendamento>());

        // Act
        var result = await _sut.GetHojeAsync();

        // Assert
        result.TotalPacientesAtendidosHoje.Should().Be(2); // pets 10 e 11, distintos, hoje
    }

    [Fact]
    public async Task GetHojeAsync_ChamaContarTeleorientacoesHojeComIdClinicaDoContextoEPropagaParaODto()
    {
        // Arrange -- FD-17 item 3
        var hoje = DateTime.UtcNow.Date;
        _eventoMock.Setup(r => r.GetByFiltersAsync(null, null, null, null, null))
            .ReturnsAsync(new List<EventoClinico>());
        _alertaMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AlertaTemperatura>());
        _agendamentoMock.Setup(r => r.GetProximosDoDiaAsync(IdClinicaContexto, It.IsAny<DateTime>(), 3))
            .ReturnsAsync(new List<Agendamento>());
        _agendamentoMock.Setup(r => r.ContarTeleorientacoesHojeAsync(IdClinicaContexto, hoje))
            .ReturnsAsync(3);

        // Act
        var result = await _sut.GetHojeAsync();

        // Assert
        result.TotalTeleorientacoesHoje.Should().Be(3);
        _agendamentoMock.Verify(r => r.ContarTeleorientacoesHojeAsync(IdClinicaContexto, hoje), Times.Once);
    }

    [Fact]
    public async Task GetAlertasAsync_ComAlertaAtivoEVacinaProximaEm30Dias_RetornaAlertasAtivosEVacinasVencendo()
    {
        // Arrange
        var proximos30Dias = DateTime.UtcNow.AddDays(15).Date;
        _alertaMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<AlertaTemperatura, bool>>>()))
            .ReturnsAsync(new List<AlertaTemperatura>
            {
                new() { Id = 1, StResolvido = false, DsTipoAlerta = "ACIMA_LIMITE", VlLimite = 8, DsMensagem = "Temp alta", IdLeituraTemperatura = 1, DtCriacao = DateTime.UtcNow }
            });
        _vacinaMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Vacina, bool>>>()))
            .ReturnsAsync(new List<Vacina>
            {
                new() { Id = 5, NmVacina = "Raiva", DtProximaDose = proximos30Dias, NrLote = "L1", DsFabricante = "F", IdEventoClinico = 1, DtCriacao = DateTime.UtcNow }
            });

        // Act
        var result = (await _sut.GetAlertasAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecentesAsync_RetornaAgendamentosPassadosMapeados_NaoOResumoDeHoje()
    {
        // Arrange
        var referencia = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);
        _agendamentoMock.Setup(r => r.GetRecentesAsync(IdClinicaContexto, It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Agendamento>
            {
                new()
                {
                    Id = 42,
                    NmPaciente = "Rex",
                    DtAgendamento = referencia.AddDays(-1),
                    DsServico = "Consulta de rotina",
                    StStatus = "REALIZADO"
                }
            });

        // Act
        var result = (await _sut.GetRecentesAsync()).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(42);
        result[0].NmPaciente.Should().Be("Rex");
        result[0].DsServico.Should().Be("Consulta de rotina");
        result[0].StStatus.Should().Be("REALIZADO");
        result[0].DtAgendamento.Should().Be(referencia.AddDays(-1));

        _agendamentoMock.Verify(r => r.GetRecentesAsync(IdClinicaContexto, It.IsAny<DateTime>(), It.IsAny<int>()), Times.Once);
        _agendamentoMock.Verify(r => r.GetProximosDoDiaAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
    }
}
