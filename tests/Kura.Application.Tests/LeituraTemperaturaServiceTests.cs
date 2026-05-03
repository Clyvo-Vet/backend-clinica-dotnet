namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.LeituraTemperatura;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using System.Linq.Expressions;

public class LeituraTemperaturaServiceTests
{
    private readonly Mock<IRepository<LeituraTemperatura>> _leituraRepoMock = new();
    private readonly Mock<IRepository<AlertaTemperatura>> _alertaRepoMock = new();
    private readonly Mock<IRepository<DispositivoIot>> _dispositivoRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly LeituraTemperaturaService _sut;

    public LeituraTemperaturaServiceTests()
    {
        _sut = new LeituraTemperaturaService(
            _leituraRepoMock.Object, _alertaRepoMock.Object,
            _dispositivoRepoMock.Object, _uowMock.Object);
    }

    private void SetupDispositivo(long id, char stAtiva = 'S')
    {
        _dispositivoRepoMock.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(new DispositivoIot
            {
                Id = id, IdClinica = 1, CdDispositivo = "DEV01",
                DsDescricao = "Teste", DsLocalizacao = "Sala", StAtiva = stAtiva
            });
    }

    [Fact]
    public async Task RegistrarLeituraAsync_DispositivoNaoExiste_LancaEntidadeNaoEncontrada()
    {
        _dispositivoRepoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((DispositivoIot?)null);

        var act = async () => await _sut.RegistrarLeituraAsync(new LeituraTemperaturaCreateDto
        {
            IdDispositivoIot = 99L, VlTemperatura = 5.0m, DtLeitura = DateTime.UtcNow
        });

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task RegistrarLeituraAsync_DispositivoInativo_LancaRegraDeNegocio()
    {
        SetupDispositivo(1L, 'N');

        var act = async () => await _sut.RegistrarLeituraAsync(new LeituraTemperaturaCreateDto
        {
            IdDispositivoIot = 1L, VlTemperatura = 5.0m, DtLeitura = DateTime.UtcNow
        });

        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("Dispositivo IoT inativo.");
    }

    [Fact]
    public async Task RegistrarLeituraAsync_Temperatura95_CriaLeituraEAlerta()
    {
        SetupDispositivo(1L);
        AlertaTemperatura? alertaCriado = null;
        _leituraRepoMock.Setup(r => r.AddAsync(It.IsAny<LeituraTemperatura>())).Returns(Task.CompletedTask);
        _alertaRepoMock.Setup(r => r.AddAsync(It.IsAny<AlertaTemperatura>()))
            .Callback<AlertaTemperatura>(a => alertaCriado = a)
            .Returns(Task.CompletedTask);

        await _sut.RegistrarLeituraAsync(new LeituraTemperaturaCreateDto
        {
            IdDispositivoIot = 1L, VlTemperatura = 9.5m, DtLeitura = DateTime.UtcNow
        });

        _leituraRepoMock.Verify(r => r.AddAsync(It.IsAny<LeituraTemperatura>()), Times.Once);
        _alertaRepoMock.Verify(r => r.AddAsync(It.IsAny<AlertaTemperatura>()), Times.Once);
        alertaCriado!.DsTipoAlerta.Should().Be("ACIMA_LIMITE");
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task RegistrarLeituraAsync_Temperatura50_CriaApenasLeitura()
    {
        SetupDispositivo(1L);
        _leituraRepoMock.Setup(r => r.AddAsync(It.IsAny<LeituraTemperatura>())).Returns(Task.CompletedTask);

        await _sut.RegistrarLeituraAsync(new LeituraTemperaturaCreateDto
        {
            IdDispositivoIot = 1L, VlTemperatura = 5.0m, DtLeitura = DateTime.UtcNow
        });

        _leituraRepoMock.Verify(r => r.AddAsync(It.IsAny<LeituraTemperatura>()), Times.Once);
        _alertaRepoMock.Verify(r => r.AddAsync(It.IsAny<AlertaTemperatura>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task RegistrarLeituraAsync_Temperatura1_CriaLeituraEAlertaAbaixoLimite()
    {
        SetupDispositivo(1L);
        AlertaTemperatura? alertaCriado = null;
        _leituraRepoMock.Setup(r => r.AddAsync(It.IsAny<LeituraTemperatura>())).Returns(Task.CompletedTask);
        _alertaRepoMock.Setup(r => r.AddAsync(It.IsAny<AlertaTemperatura>()))
            .Callback<AlertaTemperatura>(a => alertaCriado = a)
            .Returns(Task.CompletedTask);

        await _sut.RegistrarLeituraAsync(new LeituraTemperaturaCreateDto
        {
            IdDispositivoIot = 1L, VlTemperatura = 1.0m, DtLeitura = DateTime.UtcNow
        });

        _leituraRepoMock.Verify(r => r.AddAsync(It.IsAny<LeituraTemperatura>()), Times.Once);
        _alertaRepoMock.Verify(r => r.AddAsync(It.IsAny<AlertaTemperatura>()), Times.Once);
        alertaCriado!.DsTipoAlerta.Should().Be("ABAIXO_LIMITE");
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }
}
