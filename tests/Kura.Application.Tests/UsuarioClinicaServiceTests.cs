namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.DTOs.UsuarioClinica;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// FD-04 (ciclo FIN) — CRUD de <see cref="UsuarioClinica"/>.
///
/// <para>
/// 🔴 <b>Estes testes usam os REPOSITÓRIOS REAIS sobre um <c>KuraDbContext</c> InMemory, não
/// fakes.</b> Metade do que esta task garante mora no predicado do repositório
/// (<c>IgnoreQueryFilters()</c> + <c>IdClinica</c> escrito à mão): um fake que reimplemente
/// esses predicados prova o fake, não o produto — trocar
/// <c>u.IdClinica == idClinica</c> por <c>true</c> no repositório continuaria verde.
/// </para>
///
/// <para>
/// 🔴 <b>E o <c>IClinicaContext</c> do DbContext é montado com <c>IdClinicaFiltro = null</c>
/// DE PROPÓSITO — ou seja, com os query filters de tenant DESLIGADOS.</b> É o arranjo mais
/// hostil disponível: com o filtro ligado, um usuário de outra clínica já sumiria sozinho e
/// todo teste de isolamento passaria mesmo que o service não fizesse nada. Aqui a única coisa
/// entre a requisição e o dado alheio é o código desta task. E o arranjo não é artificial:
/// o filtro desliga inteiro (não nega) sempre que não há clínica no contexto — é o estado
/// real de qualquer chamada sem JWT.
/// </para>
/// </summary>
public class UsuarioClinicaServiceTests
{
    private const long ClinicaA = 1L;
    private const long ClinicaB = 2L;
    private const string SenhaValida = "SenhaDeTeste#2026";

    /// <summary>
    /// Contexto com os query filters DESLIGADOS (<c>IdClinicaFiltro = null</c>) — ver a
    /// documentação da classe.
    /// </summary>
    private static KuraDbContext CriarContexto(string dbName)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private static UsuarioClinicaService CriarService(KuraDbContext ctx, long idClinicaDoJwt)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinica).Returns(idClinicaDoJwt);

        return new UsuarioClinicaService(
            new UsuarioClinicaRepository(ctx),
            new VeterinarioRepository(ctx),
            new UnitOfWork(ctx),
            clinicaContext.Object);
    }

    private static UsuarioClinica Semear(
        KuraDbContext ctx,
        long id,
        long idClinica,
        string email,
        string perfil = PerfisUsuarioClinica.Gestor,
        bool ativo = true,
        long? idVeterinario = null)
    {
        var usuario = new UsuarioClinica
        {
            Id = id,
            IdClinica = idClinica,
            IdVeterinario = idVeterinario,
            DsEmail = email,
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaValida),
            TpPerfil = perfil,
            StAtiva = ativo,
        };
        ctx.UsuariosClinica.Add(usuario);
        ctx.SaveChanges();
        return usuario;
    }

    private static Veterinario SemearVeterinario(
        KuraDbContext ctx, long id, long idClinica, bool ativo = true)
    {
        var vet = new Veterinario
        {
            Id = id,
            IdClinica = idClinica,
            NmVeterinario = $"Vet {id}",
            NrCrmv = $"SP-{id:00000}",
            DsEmail = $"vet{id}@kura.test",
            NrTelefone = "11999990000",
            StAtiva = ativo,
        };
        ctx.Veterinarios.Add(vet);
        ctx.SaveChanges();
        return vet;
    }

    private static UsuarioClinicaCreateDto Criar(
        string email, string perfil = PerfisUsuarioClinica.Veterinario, long? idVet = null) =>
        new()
        {
            DsEmail = email,
            DsSenha = SenhaValida,
            TpPerfil = perfil,
            IdVeterinario = idVet,
        };

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Escopo de escrita: a clínica sai do JWT, nunca do corpo
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_grava_na_clinica_do_jwt()
    {
        using var ctx = CriarContexto(nameof(Criar_grava_na_clinica_do_jwt));
        Semear(ctx, 1, ClinicaB, "gestor-b@kura.test");
        var sut = CriarService(ctx, ClinicaA);

        var criado = await sut.CriarAsync(Criar("novo@kura.test"));

        criado.IdClinica.Should().Be(ClinicaA);

        // Controle positivo do instrumento: a clínica B existe e tem linha própria, então a
        // asserção acima seria capaz de falhar se a escrita caísse no tenant errado.
        var gravado = await ctx.UsuariosClinica.IgnoreQueryFilters()
            .SingleAsync(u => u.DsEmail == "novo@kura.test");
        gravado.IdClinica.Should().Be(ClinicaA);
        (await ctx.UsuariosClinica.IgnoreQueryFilters().CountAsync(u => u.IdClinica == ClinicaB))
            .Should().Be(1, "nenhuma linha nova pode ter caído na outra clínica");
    }

    [Fact]
    public async Task Criar_grava_hash_bcrypt_verificavel_e_nunca_a_senha_em_texto()
    {
        using var ctx = CriarContexto(nameof(Criar_grava_hash_bcrypt_verificavel_e_nunca_a_senha_em_texto));
        var sut = CriarService(ctx, ClinicaA);

        await sut.CriarAsync(Criar("hash@kura.test"));

        var gravado = await ctx.UsuariosClinica.IgnoreQueryFilters()
            .SingleAsync(u => u.DsEmail == "hash@kura.test");

        gravado.DsSenhaHash.Should().NotBe(SenhaValida);
        gravado.DsSenhaHash.Should().StartWith("$2", "BCrypt marca o hash com o prefixo da versão");
        // A prova que importa: o hash é verificável pela MESMA API que o AuthService usa no
        // login. Um hash gerado por outro algoritmo, ou truncado, passaria nas duas asserções
        // acima e falharia só quando o usuário tentasse entrar.
        BCrypt.Net.BCrypt.Verify(SenhaValida, gravado.DsSenhaHash).Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("senha-errada", gravado.DsSenhaHash).Should().BeFalse();
    }

    [Fact]
    public async Task Listar_devolve_somente_usuarios_da_clinica_do_jwt()
    {
        using var ctx = CriarContexto(nameof(Listar_devolve_somente_usuarios_da_clinica_do_jwt));
        Semear(ctx, 1, ClinicaA, "a1@kura.test");
        Semear(ctx, 2, ClinicaB, "b1@kura.test");
        Semear(ctx, 3, ClinicaB, "b2@kura.test");
        var sut = CriarService(ctx, ClinicaA);

        var lista = (await sut.ListarAsync()).ToList();

        lista.Should().OnlyContain(u => u.IdClinica == ClinicaA);
        lista.Should().ContainSingle(u => u.DsEmail == "a1@kura.test");
        // Controle positivo: existem 2 linhas da outra clínica para vazar. Sem elas a
        // asserção acima seria logicamente incapaz de falhar.
        (await ctx.UsuariosClinica.IgnoreQueryFilters().CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Obter_usuario_de_outra_clinica_devolve_nao_encontrado()
    {
        using var ctx = CriarContexto(nameof(Obter_usuario_de_outra_clinica_devolve_nao_encontrado));
        Semear(ctx, 1, ClinicaA, "a1@kura.test");
        var alheio = Semear(ctx, 2, ClinicaB, "b1@kura.test");
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.ObterPorIdAsync(alheio.Id);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        // Controle positivo: o MESMO caminho, com id da própria clínica, devolve o usuário —
        // então o 404 acima é isolamento, não um método que sempre falha.
        (await sut.ObterPorIdAsync(1)).DsEmail.Should().Be("a1@kura.test");
    }

    [Fact]
    public async Task Desativar_usuario_de_outra_clinica_devolve_nao_encontrado()
    {
        using var ctx = CriarContexto(nameof(Desativar_usuario_de_outra_clinica_devolve_nao_encontrado));
        Semear(ctx, 1, ClinicaA, "a1@kura.test");
        Semear(ctx, 2, ClinicaB, "b-gestor@kura.test");
        var alheio = Semear(ctx, 3, ClinicaB, "b-vet@kura.test", PerfisUsuarioClinica.Veterinario);
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.DesativarAsync(alheio.Id);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        (await ctx.UsuariosClinica.IgnoreQueryFilters().SingleAsync(u => u.Id == alheio.Id))
            .StAtiva.Should().BeTrue("o usuário da outra clínica não pode ter sido tocado");
    }

    [Fact]
    public async Task Atualizar_nao_move_o_usuario_de_clinica()
    {
        using var ctx = CriarContexto(nameof(Atualizar_nao_move_o_usuario_de_clinica));
        Semear(ctx, 1, ClinicaA, "gestor-a@kura.test");
        var alvo = Semear(ctx, 2, ClinicaA, "vet-a@kura.test", PerfisUsuarioClinica.Veterinario);
        var sut = CriarService(ctx, ClinicaA);

        var atualizado = await sut.AtualizarAsync(alvo.Id, new UsuarioClinicaUpdateDto
        {
            DsEmail = "vet-a-renomeado@kura.test",
            TpPerfil = PerfisUsuarioClinica.Veterinario,
        });

        atualizado.IdClinica.Should().Be(ClinicaA);
        atualizado.DsEmail.Should().Be("vet-a-renomeado@kura.test");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Unicidade de e-mail — UK_USUARIO_CLINICA_EMAIL (ID_CLINICA, DS_EMAIL)
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_com_email_ja_usado_na_mesma_clinica_vira_regra_de_negocio()
    {
        using var ctx = CriarContexto(nameof(Criar_com_email_ja_usado_na_mesma_clinica_vira_regra_de_negocio));
        Semear(ctx, 1, ClinicaA, "repetido@kura.test");
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.CriarAsync(Criar("repetido@kura.test"));

        // RegraDeNegocioException é o que o ExceptionHandlerMiddleware mapeia para 422 —
        // 4xx tratado, e não o ORA-00001 (500) que a constraint devolveria.
        (await act.Should().ThrowAsync<RegraDeNegocioException>())
            .Which.Message.Should().Contain("ativo");
        (await ctx.UsuariosClinica.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Criar_com_email_de_usuario_DESATIVADO_na_mesma_clinica_vira_regra_de_negocio()
    {
        // 🔴 O caso que uma checagem "só entre ativos" deixaria passar. Soft delete mantém a
        // LINHA, e a linha continua ocupando a unique key no Oracle. Como o provider InMemory
        // NÃO valida índice único, sem a checagem explícita este cenário passaria verde aqui
        // e viraria ORA-00001 (500) só contra o banco real.
        using var ctx = CriarContexto(nameof(Criar_com_email_de_usuario_DESATIVADO_na_mesma_clinica_vira_regra_de_negocio));
        Semear(ctx, 1, ClinicaA, "gestor-a@kura.test");
        Semear(ctx, 2, ClinicaA, "desativado@kura.test", PerfisUsuarioClinica.Veterinario, ativo: false);
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.CriarAsync(Criar("desativado@kura.test"));

        (await act.Should().ThrowAsync<RegraDeNegocioException>())
            .Which.Message.Should().Contain("DESATIVADO");
    }

    [Fact]
    public async Task Criar_com_o_mesmo_email_em_OUTRA_clinica_e_permitido()
    {
        // Controle positivo dos dois testes acima: a unicidade é POR CLÍNICA, não global (a
        // UK da V17 é (ID_CLINICA, DS_EMAIL) — um veterinário que atende em duas clínicas é o
        // caso real). Se a checagem fosse global, os dois testes anteriores passariam do
        // mesmo jeito e este falharia.
        using var ctx = CriarContexto(nameof(Criar_com_o_mesmo_email_em_OUTRA_clinica_e_permitido));
        Semear(ctx, 1, ClinicaB, "atende-nas-duas@kura.test");
        var sut = CriarService(ctx, ClinicaA);

        var criado = await sut.CriarAsync(Criar("atende-nas-duas@kura.test"));

        criado.IdClinica.Should().Be(ClinicaA);
        (await ctx.UsuariosClinica.IgnoreQueryFilters()
            .CountAsync(u => u.DsEmail == "atende-nas-duas@kura.test")).Should().Be(2);
    }

    [Fact]
    public async Task Atualizar_para_email_ja_usado_na_clinica_vira_regra_de_negocio()
    {
        using var ctx = CriarContexto(nameof(Atualizar_para_email_ja_usado_na_clinica_vira_regra_de_negocio));
        Semear(ctx, 1, ClinicaA, "ocupado@kura.test");
        var alvo = Semear(ctx, 2, ClinicaA, "livre@kura.test", PerfisUsuarioClinica.Veterinario);
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.AtualizarAsync(alvo.Id, new UsuarioClinicaUpdateDto
        {
            DsEmail = "ocupado@kura.test",
            TpPerfil = PerfisUsuarioClinica.Veterinario,
        });

        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task Atualizar_mantendo_o_proprio_email_nao_colide_consigo_mesmo()
    {
        // Controle negativo do teste acima: a checagem de unicidade não pode disparar quando
        // o e-mail não mudou, senão nenhum PUT funcionaria.
        using var ctx = CriarContexto(nameof(Atualizar_mantendo_o_proprio_email_nao_colide_consigo_mesmo));
        Semear(ctx, 1, ClinicaA, "gestor-a@kura.test");
        var alvo = Semear(ctx, 2, ClinicaA, "eu@kura.test", PerfisUsuarioClinica.Veterinario);
        var sut = CriarService(ctx, ClinicaA);

        var atualizado = await sut.AtualizarAsync(alvo.Id, new UsuarioClinicaUpdateDto
        {
            DsEmail = "eu@kura.test",
            TpPerfil = PerfisUsuarioClinica.Gestor,
        });

        atualizado.TpPerfil.Should().Be(PerfisUsuarioClinica.Gestor);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Vínculo com VETERINARIO — FK_USUARIO_CLINICA_VET não compõe com ID_CLINICA
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_com_vinculo_de_veterinario_de_OUTRA_clinica_vira_regra_de_negocio()
    {
        using var ctx = CriarContexto(nameof(Criar_com_vinculo_de_veterinario_de_OUTRA_clinica_vira_regra_de_negocio));
        SemearVeterinario(ctx, 1, ClinicaA);
        var vetAlheio = SemearVeterinario(ctx, 2, ClinicaB);
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.CriarAsync(
            Criar("cruzado@kura.test", PerfisUsuarioClinica.Veterinario, vetAlheio.Id));

        await act.Should().ThrowAsync<RegraDeNegocioException>();
        (await ctx.UsuariosClinica.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Criar_com_vinculo_de_veterinario_da_propria_clinica_funciona()
    {
        // Controle positivo do teste acima: sem ele, um service que recusasse TODO vínculo
        // passaria igual.
        using var ctx = CriarContexto(nameof(Criar_com_vinculo_de_veterinario_da_propria_clinica_funciona));
        var meuVet = SemearVeterinario(ctx, 1, ClinicaA);
        SemearVeterinario(ctx, 2, ClinicaB);
        var sut = CriarService(ctx, ClinicaA);

        var criado = await sut.CriarAsync(
            Criar("vinculado@kura.test", PerfisUsuarioClinica.Veterinario, meuVet.Id));

        criado.IdVeterinario.Should().Be(meuVet.Id);
    }

    [Fact]
    public async Task Criar_com_vinculo_de_veterinario_DESATIVADO_vira_regra_de_negocio()
    {
        using var ctx = CriarContexto(nameof(Criar_com_vinculo_de_veterinario_DESATIVADO_vira_regra_de_negocio));
        var vetInativo = SemearVeterinario(ctx, 1, ClinicaA, ativo: false);
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.CriarAsync(
            Criar("inativo@kura.test", PerfisUsuarioClinica.Veterinario, vetInativo.Id));

        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task Atualizar_com_vinculo_de_veterinario_de_OUTRA_clinica_vira_regra_de_negocio()
    {
        using var ctx = CriarContexto(nameof(Atualizar_com_vinculo_de_veterinario_de_OUTRA_clinica_vira_regra_de_negocio));
        SemearVeterinario(ctx, 1, ClinicaA);
        var vetAlheio = SemearVeterinario(ctx, 2, ClinicaB);
        Semear(ctx, 1, ClinicaA, "gestor-a@kura.test");
        var alvo = Semear(ctx, 2, ClinicaA, "vet-a@kura.test", PerfisUsuarioClinica.Veterinario);
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.AtualizarAsync(alvo.Id, new UsuarioClinicaUpdateDto
        {
            DsEmail = "vet-a@kura.test",
            TpPerfil = PerfisUsuarioClinica.Veterinario,
            IdVeterinario = vetAlheio.Id,
        });

        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Invariante do último gestor (decisão de produto — ver UsuarioClinicaService)
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rebaixar_o_ultimo_gestor_ativo_e_recusado()
    {
        using var ctx = CriarContexto(nameof(Rebaixar_o_ultimo_gestor_ativo_e_recusado));
        var unico = Semear(ctx, 1, ClinicaA, "unico-gestor@kura.test");
        Semear(ctx, 2, ClinicaA, "vet@kura.test", PerfisUsuarioClinica.Veterinario);
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.AtualizarAsync(unico.Id, new UsuarioClinicaUpdateDto
        {
            DsEmail = "unico-gestor@kura.test",
            TpPerfil = PerfisUsuarioClinica.Veterinario,
        });

        (await act.Should().ThrowAsync<RegraDeNegocioException>())
            .Which.Message.Should().Be(UsuarioClinicaService.MensagemUltimoGestor);
        (await ctx.UsuariosClinica.IgnoreQueryFilters().SingleAsync(u => u.Id == unico.Id))
            .TpPerfil.Should().Be(PerfisUsuarioClinica.Gestor);
    }

    [Fact]
    public async Task Desativar_o_ultimo_gestor_ativo_e_recusado()
    {
        using var ctx = CriarContexto(nameof(Desativar_o_ultimo_gestor_ativo_e_recusado));
        var unico = Semear(ctx, 1, ClinicaA, "unico-gestor@kura.test");
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.DesativarAsync(unico.Id);

        (await act.Should().ThrowAsync<RegraDeNegocioException>())
            .Which.Message.Should().Be(UsuarioClinicaService.MensagemUltimoGestor);
        (await ctx.UsuariosClinica.IgnoreQueryFilters().SingleAsync(u => u.Id == unico.Id))
            .StAtiva.Should().BeTrue();
    }

    [Fact]
    public async Task Rebaixar_gestor_quando_ha_outro_gestor_ativo_e_permitido()
    {
        // 🔴 Controle positivo do invariante, E a decisão de produto em si: rebaixar-se é
        // PERMITIDO — o que é recusado é zerar o quadro de gestores. Sem este teste, um
        // service que recusasse TODA mudança de perfil passaria nos dois testes acima.
        using var ctx = CriarContexto(nameof(Rebaixar_gestor_quando_ha_outro_gestor_ativo_e_permitido));
        var alvo = Semear(ctx, 1, ClinicaA, "gestor-1@kura.test");
        Semear(ctx, 2, ClinicaA, "gestor-2@kura.test");
        var sut = CriarService(ctx, ClinicaA);

        var atualizado = await sut.AtualizarAsync(alvo.Id, new UsuarioClinicaUpdateDto
        {
            DsEmail = "gestor-1@kura.test",
            TpPerfil = PerfisUsuarioClinica.Veterinario,
        });

        atualizado.TpPerfil.Should().Be(PerfisUsuarioClinica.Veterinario);
    }

    [Fact]
    public async Task Desativar_gestor_quando_ha_outro_gestor_ativo_e_permitido()
    {
        using var ctx = CriarContexto(nameof(Desativar_gestor_quando_ha_outro_gestor_ativo_e_permitido));
        var alvo = Semear(ctx, 1, ClinicaA, "gestor-1@kura.test");
        Semear(ctx, 2, ClinicaA, "gestor-2@kura.test");
        var sut = CriarService(ctx, ClinicaA);

        await sut.DesativarAsync(alvo.Id);

        (await ctx.UsuariosClinica.IgnoreQueryFilters().SingleAsync(u => u.Id == alvo.Id))
            .StAtiva.Should().BeFalse();
    }

    [Fact]
    public async Task Gestor_DESATIVADO_nao_conta_para_o_invariante()
    {
        // O quadro tem 2 linhas GESTOR, mas uma está inativa: o invariante conta gestores
        // ATIVOS, então rebaixar o único ativo continua sendo recusado. Um contador que
        // esquecesse o ST_ATIVA acharia 1 restante e deixaria a clínica sem administrador.
        using var ctx = CriarContexto(nameof(Gestor_DESATIVADO_nao_conta_para_o_invariante));
        var ativo = Semear(ctx, 1, ClinicaA, "gestor-ativo@kura.test");
        Semear(ctx, 2, ClinicaA, "gestor-inativo@kura.test", ativo: false);
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.DesativarAsync(ativo.Id);

        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task Gestor_de_OUTRA_clinica_nao_conta_para_o_invariante()
    {
        // Controle de escopo do contador: a clínica B tem 3 gestores; a clínica A tem 1.
        // Um contador sem o predicado de clínica acharia 3 restantes e permitiria zerar A.
        using var ctx = CriarContexto(nameof(Gestor_de_OUTRA_clinica_nao_conta_para_o_invariante));
        var unicoDeA = Semear(ctx, 1, ClinicaA, "gestor-a@kura.test");
        Semear(ctx, 2, ClinicaB, "gestor-b1@kura.test");
        Semear(ctx, 3, ClinicaB, "gestor-b2@kura.test");
        Semear(ctx, 4, ClinicaB, "gestor-b3@kura.test");
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.DesativarAsync(unicoDeA.Id);

        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task Desativar_veterinario_nao_e_barrado_pelo_invariante()
    {
        // Controle negativo: o invariante não pode transbordar para quem não é gestor. Aqui a
        // clínica tem 1 gestor só, e desativar o VETERINARIO continua funcionando.
        using var ctx = CriarContexto(nameof(Desativar_veterinario_nao_e_barrado_pelo_invariante));
        Semear(ctx, 1, ClinicaA, "gestor-a@kura.test");
        var vet = Semear(ctx, 2, ClinicaA, "vet-a@kura.test", PerfisUsuarioClinica.Veterinario);
        var sut = CriarService(ctx, ClinicaA);

        await sut.DesativarAsync(vet.Id);

        (await ctx.UsuariosClinica.IgnoreQueryFilters().SingleAsync(u => u.Id == vet.Id))
            .StAtiva.Should().BeFalse();
    }

    [Fact]
    public async Task Promover_veterinario_a_gestor_libera_o_rebaixamento_do_gestor_atual()
    {
        // O caminho de saída que a decisão de produto exige que exista: "promova outro antes".
        // Se ele não funcionasse, o invariante seria uma prisão, não uma proteção.
        using var ctx = CriarContexto(nameof(Promover_veterinario_a_gestor_libera_o_rebaixamento_do_gestor_atual));
        var gestor = Semear(ctx, 1, ClinicaA, "gestor@kura.test");
        var vet = Semear(ctx, 2, ClinicaA, "vet@kura.test", PerfisUsuarioClinica.Veterinario);
        var sut = CriarService(ctx, ClinicaA);

        var antes = () => sut.DesativarAsync(gestor.Id);
        await antes.Should().ThrowAsync<RegraDeNegocioException>();

        await sut.AtualizarAsync(vet.Id, new UsuarioClinicaUpdateDto
        {
            DsEmail = "vet@kura.test",
            TpPerfil = PerfisUsuarioClinica.Gestor,
        });

        await sut.DesativarAsync(gestor.Id);

        (await ctx.UsuariosClinica.IgnoreQueryFilters().SingleAsync(u => u.Id == gestor.Id))
            .StAtiva.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Perfil e senha
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criar_com_perfil_desconhecido_vira_regra_de_negocio_e_nao_chega_ao_banco()
    {
        // O CHECK CHK_USUARIO_CLINICA_PERFIL existe no Oracle e NÃO existe no InMemory: sem a
        // guarda no service, o valor inválido seria gravado aqui em silêncio e viraria
        // ORA-02290 (500) em produção.
        using var ctx = CriarContexto(nameof(Criar_com_perfil_desconhecido_vira_regra_de_negocio_e_nao_chega_ao_banco));
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.CriarAsync(Criar("x@kura.test", "RECEPCIONISTA"));

        await act.Should().ThrowAsync<RegraDeNegocioException>();
        (await ctx.UsuariosClinica.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Definir_senha_troca_o_hash_e_invalida_a_senha_anterior()
    {
        using var ctx = CriarContexto(nameof(Definir_senha_troca_o_hash_e_invalida_a_senha_anterior));
        Semear(ctx, 1, ClinicaA, "gestor-a@kura.test");
        var alvo = Semear(ctx, 2, ClinicaA, "alvo@kura.test", PerfisUsuarioClinica.Veterinario);
        var sut = CriarService(ctx, ClinicaA);

        await sut.DefinirSenhaAsync(alvo.Id, new UsuarioClinicaSenhaUpdateDto
        {
            DsSenha = "OutraSenha#2026",
        });

        var gravado = await ctx.UsuariosClinica.IgnoreQueryFilters()
            .SingleAsync(u => u.Id == alvo.Id);
        BCrypt.Net.BCrypt.Verify("OutraSenha#2026", gravado.DsSenhaHash).Should().BeTrue();
        BCrypt.Net.BCrypt.Verify(SenhaValida, gravado.DsSenhaHash).Should().BeFalse();
    }

    [Fact]
    public async Task Definir_senha_de_usuario_de_outra_clinica_devolve_nao_encontrado()
    {
        using var ctx = CriarContexto(nameof(Definir_senha_de_usuario_de_outra_clinica_devolve_nao_encontrado));
        Semear(ctx, 1, ClinicaA, "a@kura.test");
        var alheio = Semear(ctx, 2, ClinicaB, "b@kura.test");
        var hashOriginal = alheio.DsSenhaHash;
        var sut = CriarService(ctx, ClinicaA);

        var act = () => sut.DefinirSenhaAsync(alheio.Id, new UsuarioClinicaSenhaUpdateDto
        {
            DsSenha = "InvadindoSenha#2026",
        });

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        (await ctx.UsuariosClinica.IgnoreQueryFilters().SingleAsync(u => u.Id == alheio.Id))
            .DsSenhaHash.Should().Be(hashOriginal);
    }
}
