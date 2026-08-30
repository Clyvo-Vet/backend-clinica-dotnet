namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.DTOs.ServicoPreco;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// FD-09 (ciclo FIN) — CRUD de <see cref="ServicoPreco"/>.
///
/// <para>
/// 🔴 <b>Estes testes usam o REPOSITÓRIO REAL sobre um <c>KuraDbContext</c> InMemory, não um
/// fake.</b> Metade do que esta task garante mora no predicado do repositório
/// (<c>IgnoreQueryFilters()</c> + <c>IdClinica</c> escrito à mão): um fake que reimplemente
/// esses predicados prova o fake, não o produto — trocar <c>s.IdClinica == idClinica</c> por
/// <c>true</c> continuaria verde.
/// </para>
///
/// <para>
/// 🔴 <b>E o <c>IClinicaContext</c> do DbContext é montado com <c>IdClinicaFiltro = null</c> DE
/// PROPÓSITO — ou seja, com os query filters de tenant DESLIGADOS.</b> É o arranjo mais hostil
/// disponível: com o filtro ligado, uma linha de outra clínica já sumiria sozinha e todo teste
/// de isolamento passaria mesmo que o service não fizesse nada. E o arranjo não é artificial:
/// o filtro desliga inteiro (não nega) sempre que não há clínica no contexto.
/// </para>
/// </summary>
public class ServicoPrecoServiceTests
{
    private const long ClinicaA = 1L;
    private const long ClinicaB = 2L;

    /// <summary>Contexto com os query filters DESLIGADOS — ver a documentação da classe.</summary>
    private static KuraDbContext CriarContexto(string dbName)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private static ServicoPrecoService CriarService(KuraDbContext ctx, long idClinicaDoJwt)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinica).Returns(idClinicaDoJwt);

        return new ServicoPrecoService(
            new ServicoPrecoRepository(ctx),
            new UnitOfWork(ctx),
            clinicaContext.Object);
    }

    private static ServicoPreco Semear(
        KuraDbContext ctx,
        long id,
        long idClinica,
        string nome,
        decimal preco = 100.00m,
        bool ativo = true)
    {
        var servico = new ServicoPreco
        {
            Id = id,
            IdClinica = idClinica,
            NmServico = nome,
            VlPreco = preco,
            StAtiva = ativo,
        };

        ctx.ServicosPreco.Add(servico);
        ctx.SaveChanges();
        ctx.Entry(servico).State = EntityState.Detached;
        return servico;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Escopo de tenant
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_grava_a_clinica_do_JWT_e_nada_mais()
    {
        using var ctx = CriarContexto(nameof(Criar_grava_a_clinica_do_JWT_e_nada_mais));
        var service = CriarService(ctx, ClinicaA);

        var criado = await service.CriarAsync(new ServicoPrecoCreateDto
        {
            NmServico = "Consulta",
            VlPreco = 150.00m,
        });

        criado.IdClinica.Should().Be(ClinicaA);

        // Confere no banco, ignorando qualquer filtro: a linha realmente nasceu na clínica A.
        var gravado = await ctx.ServicosPreco.IgnoreQueryFilters()
            .SingleAsync(s => s.Id == criado.Id);
        gravado.IdClinica.Should().Be(ClinicaA);
        gravado.VlPreco.Should().Be(150.00m);
    }

    [Fact]
    public async Task Listar_devolve_so_os_ativos_da_clinica_do_JWT()
    {
        using var ctx = CriarContexto(nameof(Listar_devolve_so_os_ativos_da_clinica_do_JWT));
        Semear(ctx, 1, ClinicaA, "Consulta A");
        Semear(ctx, 2, ClinicaB, "Consulta B");
        Semear(ctx, 3, ClinicaA, "Desativado A", ativo: false);

        var lista = (await CriarService(ctx, ClinicaA).ListarAsync()).ToList();

        // Uma asserção só de contagem passaria com a linha errada dentro; a de conteúdo é
        // que morde. Com o predicado de tenant trocado por `true`, "Consulta B" apareceria.
        lista.Select(s => s.NmServico).Should().BeEquivalentTo(["Consulta A"]);
        lista.Should().OnlyContain(s => s.IdClinica == ClinicaA);
    }

    [Fact]
    public async Task Obter_por_id_de_outra_clinica_lanca_EntidadeNaoEncontrada()
    {
        using var ctx = CriarContexto(nameof(Obter_por_id_de_outra_clinica_lanca_EntidadeNaoEncontrada));
        Semear(ctx, 1, ClinicaB, "Da clínica B");
        Semear(ctx, 2, ClinicaA, "Da clínica A");
        var service = CriarService(ctx, ClinicaA);

        var acao = async () => await service.ObterPorIdAsync(1);

        await acao.Should().ThrowAsync<EntidadeNaoEncontradaException>();

        // 🔴 Controle positivo: o mesmo método, com id da própria clínica, devolve o item.
        (await service.ObterPorIdAsync(2)).NmServico.Should().Be("Da clínica A");
    }

    [Fact]
    public async Task Atualizar_item_de_outra_clinica_lanca_e_nao_grava()
    {
        using var ctx = CriarContexto(nameof(Atualizar_item_de_outra_clinica_lanca_e_nao_grava));
        Semear(ctx, 1, ClinicaB, "Intocável", 200.00m);
        var service = CriarService(ctx, ClinicaA);

        var acao = async () => await service.AtualizarAsync(
            1, new ServicoPrecoUpdateDto { NmServico = "Invadido", VlPreco = 1.00m });

        await acao.Should().ThrowAsync<EntidadeNaoEncontradaException>();

        var intacto = await ctx.ServicosPreco.IgnoreQueryFilters().SingleAsync(s => s.Id == 1);
        intacto.NmServico.Should().Be("Intocável");
        intacto.VlPreco.Should().Be(200.00m);
    }

    [Fact]
    public async Task Desativar_item_de_outra_clinica_lanca_e_o_item_continua_ativo()
    {
        using var ctx = CriarContexto(nameof(Desativar_item_de_outra_clinica_lanca_e_o_item_continua_ativo));
        Semear(ctx, 1, ClinicaB, "Da clínica B");
        var service = CriarService(ctx, ClinicaA);

        var acao = async () => await service.DesativarAsync(1);

        await acao.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        (await ctx.ServicosPreco.IgnoreQueryFilters().SingleAsync(s => s.Id == 1))
            .StAtiva.Should().BeTrue();
    }

    [Fact]
    public async Task Nome_duplicado_e_avaliado_DENTRO_da_clinica_e_nao_entre_clinicas()
    {
        // Duas clínicas podem ter "Consulta de rotina" — é o caso NORMAL. Uma checagem de
        // unicidade que esquecesse o ID_CLINICA no predicado recusaria o cadastro da segunda
        // clínica e só apareceria em produção, com dois tenants reais na mesma base.
        using var ctx = CriarContexto(nameof(Nome_duplicado_e_avaliado_DENTRO_da_clinica_e_nao_entre_clinicas));
        Semear(ctx, 1, ClinicaB, "Consulta de rotina");

        var criado = await CriarService(ctx, ClinicaA).CriarAsync(new ServicoPrecoCreateDto
        {
            NmServico = "Consulta de rotina",
            VlPreco = 120.00m,
        });

        criado.IdClinica.Should().Be(ClinicaA);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Decisão de produto: nome duplicado × soft delete
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_com_nome_de_item_ATIVO_da_mesma_clinica_lanca_RegraDeNegocio()
    {
        using var ctx = CriarContexto(nameof(Criar_com_nome_de_item_ATIVO_da_mesma_clinica_lanca_RegraDeNegocio));
        Semear(ctx, 1, ClinicaA, "Banho");
        var service = CriarService(ctx, ClinicaA);

        var acao = async () => await service.CriarAsync(
            new ServicoPrecoCreateDto { NmServico = "Banho", VlPreco = 50.00m });

        (await acao.Should().ThrowAsync<RegraDeNegocioException>())
            .WithMessage(ServicoPrecoService.MensagemNomeDuplicado);
    }

    [Fact]
    public async Task Comparacao_de_nome_ignora_caixa_e_espacos_nas_pontas()
    {
        // Sem normalizar, "banho" e " Banho " passariam pela checagem e a clínica ficaria com
        // três linhas ativas que o gestor lê como a mesma coisa — o estado ambíguo que a
        // FD-10 herdaria na hora de lançar cobrança.
        using var ctx = CriarContexto(nameof(Comparacao_de_nome_ignora_caixa_e_espacos_nas_pontas));
        Semear(ctx, 1, ClinicaA, "Banho");
        var service = CriarService(ctx, ClinicaA);

        var porCaixa = async () => await service.CriarAsync(
            new ServicoPrecoCreateDto { NmServico = "banho", VlPreco = 50.00m });
        var porEspaco = async () => await service.CriarAsync(
            new ServicoPrecoCreateDto { NmServico = "  Banho  ", VlPreco = 50.00m });

        await porCaixa.Should().ThrowAsync<RegraDeNegocioException>();
        await porEspaco.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task Nome_de_item_DESATIVADO_pode_ser_recadastrado()
    {
        // 🔴 A DECISÃO DE PRODUTO DESTA TASK. A FD-07 deliberadamente NÃO criou
        // UNIQUE (ID_CLINICA, NM_SERVICO) para que isto seja possível; uma checagem que
        // olhasse TODAS as linhas seria aquela unique reescrita em código e queimaria o nome
        // para sempre — o defeito A-3 da FD-04.
        using var ctx = CriarContexto(nameof(Nome_de_item_DESATIVADO_pode_ser_recadastrado));
        Semear(ctx, 1, ClinicaA, "Tosa", ativo: false);
        var service = CriarService(ctx, ClinicaA);

        var criado = await service.CriarAsync(
            new ServicoPrecoCreateDto { NmServico = "Tosa", VlPreco = 80.00m });

        criado.Id.Should().NotBe(1);
        criado.NmServico.Should().Be("Tosa");

        // 🔴 Controle positivo do teste acima e deste: com o item ATIVO, o mesmo nome é
        // recusado. Ou seja, a checagem existe — o que muda é só o estado do concorrente.
        Semear(ctx, 3, ClinicaA, "Vacinação");
        var recusado = async () => await service.CriarAsync(
            new ServicoPrecoCreateDto { NmServico = "Vacinação", VlPreco = 80.00m });
        await recusado.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task Renomear_para_o_proprio_nome_nao_colide_consigo_mesmo()
    {
        // Um `excetoId` esquecido faria todo PUT que não muda o nome falhar com "nome já em
        // uso" — o item colidindo consigo mesmo.
        using var ctx = CriarContexto(nameof(Renomear_para_o_proprio_nome_nao_colide_consigo_mesmo));
        Semear(ctx, 1, ClinicaA, "Consulta", 100.00m);
        var service = CriarService(ctx, ClinicaA);

        var atualizado = await service.AtualizarAsync(
            1, new ServicoPrecoUpdateDto { NmServico = "Consulta", VlPreco = 130.00m });

        atualizado.VlPreco.Should().Be(130.00m);
    }

    [Fact]
    public async Task Renomear_para_o_nome_de_OUTRO_item_ativo_lanca_RegraDeNegocio()
    {
        using var ctx = CriarContexto(nameof(Renomear_para_o_nome_de_OUTRO_item_ativo_lanca_RegraDeNegocio));
        Semear(ctx, 1, ClinicaA, "Consulta");
        Semear(ctx, 2, ClinicaA, "Banho");
        var service = CriarService(ctx, ClinicaA);

        var acao = async () => await service.AtualizarAsync(
            2, new ServicoPrecoUpdateDto { NmServico = "Consulta", VlPreco = 50.00m });

        await acao.Should().ThrowAsync<RegraDeNegocioException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Soft delete e a porta de volta
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Desativar_faz_soft_delete_e_a_linha_permanece_no_banco()
    {
        using var ctx = CriarContexto(nameof(Desativar_faz_soft_delete_e_a_linha_permanece_no_banco));
        Semear(ctx, 1, ClinicaA, "Consulta");

        await CriarService(ctx, ClinicaA).DesativarAsync(1);

        var linha = await ctx.ServicosPreco.IgnoreQueryFilters()
            .SingleOrDefaultAsync(s => s.Id == 1);
        linha.Should().NotBeNull("soft delete nunca apaga a linha");
        linha!.StAtiva.Should().BeFalse();
    }

    [Fact]
    public async Task Atualizar_item_desativado_lanca_em_vez_de_gravar_em_silencio()
    {
        // A-3 da FD-04: um 200 que não muda o que o gestor vê na lista é indistinguível de bug.
        using var ctx = CriarContexto(nameof(Atualizar_item_desativado_lanca_em_vez_de_gravar_em_silencio));
        Semear(ctx, 1, ClinicaA, "Consulta", 100.00m, ativo: false);
        var service = CriarService(ctx, ClinicaA);

        var acao = async () => await service.AtualizarAsync(
            1, new ServicoPrecoUpdateDto { NmServico = "Consulta", VlPreco = 999.00m });

        (await acao.Should().ThrowAsync<RegraDeNegocioException>())
            .WithMessage(ServicoPrecoService.MensagemServicoDesativado);

        (await ctx.ServicosPreco.IgnoreQueryFilters().SingleAsync(s => s.Id == 1))
            .VlPreco.Should().Be(100.00m);
    }

    [Fact]
    public async Task Reativar_devolve_o_item_ao_quadro_ativo()
    {
        using var ctx = CriarContexto(nameof(Reativar_devolve_o_item_ao_quadro_ativo));
        Semear(ctx, 1, ClinicaA, "Consulta", ativo: false);
        var service = CriarService(ctx, ClinicaA);

        var reativado = await service.ReativarAsync(1);

        reativado.StAtiva.Should().BeTrue();
        reativado.Id.Should().Be(1);
        (await service.ListarAsync()).Should().ContainSingle(s => s.Id == 1);
    }

    [Fact]
    public async Task Reativar_com_o_nome_ja_recadastrado_lanca_RegraDeNegocio()
    {
        using var ctx = CriarContexto(nameof(Reativar_com_o_nome_ja_recadastrado_lanca_RegraDeNegocio));
        Semear(ctx, 1, ClinicaA, "Consulta", ativo: false);
        Semear(ctx, 2, ClinicaA, "Consulta");
        var service = CriarService(ctx, ClinicaA);

        var acao = async () => await service.ReativarAsync(1);

        (await acao.Should().ThrowAsync<RegraDeNegocioException>())
            .WithMessage(ServicoPrecoService.MensagemReativacaoComNomeOcupado);
    }

    [Fact]
    public async Task Reativar_item_ja_ativo_e_idempotente()
    {
        using var ctx = CriarContexto(nameof(Reativar_item_ja_ativo_e_idempotente));
        Semear(ctx, 1, ClinicaA, "Consulta");
        var service = CriarService(ctx, ClinicaA);

        var resultado = await service.ReativarAsync(1);

        resultado.StAtiva.Should().BeTrue();
    }

    [Fact]
    public async Task Preco_com_centavos_sobrevive_ao_round_trip_do_service()
    {
        // ⚠️ CONTRATO DE TIPO, NÃO PROVA DE BANCO. O provider InMemory não reprova precisão —
        // este teste ficaria verde com HasPrecision errado. O que ele pega é a troca de
        // `decimal` por `double` em qualquer ponto da cadeia DTO → entidade → DTO, onde
        // 10,55 deixaria de voltar exatamente 10,55. A prova de banco é a FD-12.
        using var ctx = CriarContexto(nameof(Preco_com_centavos_sobrevive_ao_round_trip_do_service));
        var service = CriarService(ctx, ClinicaA);

        var criado = await service.CriarAsync(
            new ServicoPrecoCreateDto { NmServico = "Consulta", VlPreco = 10.55m });

        (await service.ObterPorIdAsync(criado.Id)).VlPreco.Should().Be(10.55m);
    }
}
