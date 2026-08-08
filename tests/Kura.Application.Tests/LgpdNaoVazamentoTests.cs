namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Luna;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

/// <summary>
/// TASK-67, ruling do maestro (Bloco 0 §B0.3): DS_CONTEUDO (mensagem do tutor) e o
/// telefone podem ser PERSISTIDOS na tabela (é o propósito dela), mas NUNCA podem
/// aparecer em log de aplicação, em LOG_ERRO, ou em mensagem de exceção — mesma classe
/// de vazamento que a TASK-46 corrigiu do lado da Luna (telefone cru em log).
///
/// Por que isto é suficiente como prova: <c>ExceptionHandlerMiddleware</c> (Kura.Api)
/// só loga/devolve <c>ex.Message</c> — nunca lê campos do request diretamente — e
/// documenta explicitamente que não escreve em LOG_ERRO (essa tabela é escrita só pela
/// Luna, PL/SQL). Logo, "nenhuma exceção lançada pelo caminho novo (TASK-67) interpola
/// ds_conteudo/telefone na sua Message" fecha o requisito de ponta a ponta: não há
/// nenhum outro lugar no código novo que logue esses campos.
/// </summary>
public class LgpdNaoVazamentoTests
{
    private const string ConteudoSensivel = "MARCADOR_LGPD_conteudo_de_mensagem_x9k2";
    private const string TelefoneSensivel = "5511900009999";

    [Fact]
    public async Task RegistrarInteracaoAsync_IdTutorNull_MensagemDeExcecaoNaoContemDsConteudo()
    {
        var sut = new LunaService(
            Mock.Of<ITriagemLunaRepository>(),
            Mock.Of<IRepository<InteracaoCanal>>(),
            Mock.Of<ITutorRepository>(),
            Mock.Of<IUnitOfWork>());

        var dto = new InteractionRequestDto
        {
            IdTutor = null,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = ConteudoSensivel,
            DtRecebimento = DateTime.UtcNow
        };

        var act = async () => await sut.RegistrarInteracaoAsync(dto);

        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().NotContain(ConteudoSensivel);
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_TutorInexistente_MensagemDeExcecaoNaoContemDsConteudo()
    {
        var tutorRepo = new Mock<ITutorRepository>();
        tutorRepo.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync((Tutor?)null);

        var sut = new LunaService(
            Mock.Of<ITriagemLunaRepository>(),
            Mock.Of<IRepository<InteracaoCanal>>(),
            tutorRepo.Object,
            Mock.Of<IUnitOfWork>());

        var dto = new InteractionRequestDto
        {
            IdTutor = 999,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = ConteudoSensivel,
            DtRecebimento = DateTime.UtcNow
        };

        var act = async () => await sut.RegistrarInteracaoAsync(dto);

        var ex = await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        ex.Which.Message.Should().NotContain(ConteudoSensivel);
    }

    [Fact]
    public async Task BuscarContextoPorTelefoneAsync_TutorNaoEncontrado_RetornaNullSemLancarNadaComOTelefone()
    {
        var tutorRepo = new Mock<ITutorRepository>();
        tutorRepo.Setup(r => r.GetByTelefoneAsync(TelefoneSensivel)).ReturnsAsync((Tutor?)null);

        var sut = new TutorService(
            tutorRepo.Object,
            Mock.Of<ITutorPetRepository>(),
            Mock.Of<IRepository<Especie>>(),
            Mock.Of<IRepository<Raca>>(),
            Mock.Of<IInviteTutorRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IClinicaContext>());

        // "Não encontrado" é modelado como null, nunca como exceção — a forma mais
        // segura possível de reportar ausência sem correr risco de interpolar o
        // telefone numa mensagem que sobe até o middleware/log.
        var result = await sut.BuscarContextoPorTelefoneAsync(TelefoneSensivel);

        result.Should().BeNull();
    }
}
