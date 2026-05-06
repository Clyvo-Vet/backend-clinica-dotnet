namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Agenda;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class AgendaServiceTests
{
    private readonly Mock<IAgendamentoReadRepository> _repoMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly AgendaService _sut;

    public AgendaServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _sut = new AgendaService(_repoMock.Object, _clinicaMock.Object);
    }

    private static DateTime Inicio => new(2026, 5, 6);
    private static DateTime Fim => new(2026, 5, 12);

    [Fact]
    public async Task GetAgendaAsync_IntervaloValido_RetornaAgendamentosMapeados()
    {
        var agendamentos = new List<Agendamento>
        {
            new()
            {
                Id = 1,
                IdClinica = 1,
                IdVeterinario = 10,
                DtAgendamento = Inicio.AddHours(9),
                NrDuracaoMinutos = 30,
                DsTipoConsulta = "Consulta",
                DsStatus = "CONFIRMADO",
                StAtiva = 'S',
                Pet = new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, IdRaca = 1, DtNascimento = DateTime.UtcNow, SgSexo = 'M', SgPorte = 'M' },
                Tutor = new Tutor { Id = 3, NmTutor = "João" },
                Veterinario = new Veterinario { Id = 10, NmVeterinario = "Dr. Ana", IdClinica = 1, NrCrmv = "1234" }
            }
        };

        _repoMock.Setup(r => r.GetByIntervaloAsync(1L, Inicio, Fim, null))
            .ReturnsAsync(agendamentos);

        var result = await _sut.GetAgendaAsync(Inicio, Fim, null);

        result.Should().NotBeNull();
        result.DataInicio.Should().Be(Inicio);
        result.DataFim.Should().Be(Fim);
        result.Agendamentos.Should().HaveCount(1);

        var item = result.Agendamentos[0];
        item.IdAgendamento.Should().Be(1);
        item.NmPet.Should().Be("Rex");
        item.NmTutor.Should().Be("João");
        item.NmVeterinario.Should().Be("Dr. Ana");
        item.IdVeterinario.Should().Be(10);
        item.DsTipoConsulta.Should().Be("Consulta");
        item.DsStatus.Should().Be("CONFIRMADO");
        item.DuracaoMinutos.Should().Be(30);
    }

    [Fact]
    public async Task GetAgendaAsync_DataFimAnteriorDataInicio_LancaRegraDeNegocio()
    {
        var act = async () => await _sut.GetAgendaAsync(Fim, Inicio, null);

        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("DataFim não pode ser anterior à DataInicio.");
    }

    [Fact]
    public async Task GetAgendaAsync_IntervaloMaiorQue31Dias_LancaRegraDeNegocio()
    {
        var inicio = new DateTime(2026, 1, 1);
        var fimFora = inicio.AddDays(32);

        var act = async () => await _sut.GetAgendaAsync(inicio, fimFora, null);

        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("Intervalo máximo de 31 dias.");
    }

    [Fact]
    public async Task GetAgendaAsync_ComVeterinarioId_PassaFiltroAoRepository()
    {
        _repoMock.Setup(r => r.GetByIntervaloAsync(1L, Inicio, Fim, 10L))
            .ReturnsAsync(new List<Agendamento>());

        await _sut.GetAgendaAsync(Inicio, Fim, 10L);

        _repoMock.Verify(r => r.GetByIntervaloAsync(1L, Inicio, Fim, 10L), Times.Once);
    }
}
