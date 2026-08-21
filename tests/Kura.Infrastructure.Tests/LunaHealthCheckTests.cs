namespace Kura.Infrastructure.Tests;

using System.Net;
using System.Threading;
using FluentAssertions;
using Kura.Api.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

/// <summary>
/// S3D-03: prova, com <see cref="HttpClient"/> mockado (sem rede real), que
/// <see cref="LunaHealthCheck"/> nunca devolve <see cref="HealthStatus.Unhealthy"/> —
/// decisão travada no backlog: Luna é dependência de terceiro, a API da clínica
/// continua operacional sem ela, então toda falha (HTTP não-2xx, exceção de rede,
/// timeout) vira <see cref="HealthStatus.Degraded"/>, nunca <see cref="HealthStatus.Unhealthy"/>.
/// </summary>
public class LunaHealthCheckTests
{
    private static LunaHealthCheck CriarCheck(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://luna-fake:8000") };
        return new LunaHealthCheck(httpClient, NullLogger<LunaHealthCheck>.Instance);
    }

    private static Mock<HttpMessageHandler> MockHandlerRespondendo(HttpStatusCode statusCode)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));
        return handlerMock;
    }

    private static Mock<HttpMessageHandler> MockHandlerLancando(Exception exception)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);
        return handlerMock;
    }

    [Fact]
    public async Task CheckHealthAsync_LunaResponde200_RetornaHealthy()
    {
        // Arrange
        var handler = MockHandlerRespondendo(HttpStatusCode.OK);
        var check = CriarCheck(handler.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("luna", check, HealthStatus.Degraded, tags: null),
        };

        // Act
        var result = await check.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_LunaRespondeErroHttp_RetornaDegradedNuncaUnhealthy()
    {
        // Arrange
        var handler = MockHandlerRespondendo(HttpStatusCode.InternalServerError);
        var check = CriarCheck(handler.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("luna", check, HealthStatus.Degraded, tags: null),
        };

        // Act
        var result = await check.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Status.Should().NotBe(HealthStatus.Unhealthy,
            "Luna é serviço externo — indisponibilidade dela não pode marcar a API da clínica como doente");
    }

    [Fact]
    public async Task CheckHealthAsync_LunaLancaHttpRequestException_RetornaDegradedNuncaUnhealthy()
    {
        // Arrange — simula Luna totalmente fora do ar (conexão recusada/DNS falhou)
        var handler = MockHandlerLancando(new HttpRequestException("conexão recusada"));
        var check = CriarCheck(handler.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("luna", check, HealthStatus.Degraded, tags: null),
        };

        // Act
        var result = await check.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Exception.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckHealthAsync_LunaEstouraTimeout_RetornaDegradedNuncaUnhealthy()
    {
        // Arrange — TaskCanceledException é o que o HttpClient lança quando o
        // HttpClient.Timeout (3s, configurado em ServiceCollectionExtensions) estoura.
        var handler = MockHandlerLancando(new TaskCanceledException("timeout"));
        var check = CriarCheck(handler.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("luna", check, HealthStatus.Degraded, tags: null),
        };

        // Act
        var result = await check.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Status.Should().NotBe(HealthStatus.Unhealthy);
    }

    /// <summary>
    /// Prova de mordida: sem o catch específico de exceção de rede/timeout em
    /// <see cref="LunaHealthCheck.CheckHealthAsync"/>, uma <see cref="TaskCanceledException"/>
    /// propagaria sem tratamento — o pipeline de health checks do ASP.NET Core capturaria a
    /// exceção e aplicaria o <c>failureStatus</c> da registration (que também é
    /// <see cref="HealthStatus.Degraded"/> por decisão redundante de defesa em profundidade),
    /// mas SEM a mensagem descritiva que o catch produz. Este teste confirma o
    /// comportamento explícito, não o fallback do framework.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_LunaEstouraTimeout_DescricaoExplicaIndisponibilidade()
    {
        // Arrange
        var handler = MockHandlerLancando(new TaskCanceledException("timeout"));
        var check = CriarCheck(handler.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("luna", check, HealthStatus.Degraded, tags: null),
        };

        // Act
        var result = await check.CheckHealthAsync(context);

        // Assert
        result.Description.Should().Contain("indisponível",
            "a descrição precisa explicar o motivo real, não só o status");
    }
}
