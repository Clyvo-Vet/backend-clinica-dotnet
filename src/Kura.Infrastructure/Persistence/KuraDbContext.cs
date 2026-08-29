namespace Kura.Infrastructure.Persistence;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

public class KuraDbContext : DbContext
{
    private readonly IClinicaContext _clinicaContext;

    public KuraDbContext(DbContextOptions<KuraDbContext> options, IClinicaContext clinicaContext)
        : base(options)
    {
        _clinicaContext = clinicaContext;
    }

    public DbSet<Clinica> Clinicas => Set<Clinica>();
    public DbSet<Veterinario> Veterinarios => Set<Veterinario>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Tutor> Tutores => Set<Tutor>();
    public DbSet<TutorPet> TutorPets => Set<TutorPet>();
    public DbSet<Especie> Especies => Set<Especie>();
    public DbSet<Raca> Racas => Set<Raca>();
    public DbSet<EventoClinico> EventosClinicos => Set<EventoClinico>();
    public DbSet<TipoEvento> TiposEvento => Set<TipoEvento>();
    public DbSet<Vacina> Vacinas => Set<Vacina>();
    public DbSet<Prescricao> Prescricoes => Set<Prescricao>();
    public DbSet<Medicamento> Medicamentos => Set<Medicamento>();
    public DbSet<Exame> Exames => Set<Exame>();
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<DispositivoIot> DispositivosIot => Set<DispositivoIot>();
    public DbSet<LeituraTemperatura> LeiturasTemperatura => Set<LeituraTemperatura>();
    public DbSet<AlertaTemperatura> AlertasTemperatura => Set<AlertaTemperatura>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<TriagemLuna> TriagensLuna => Set<TriagemLuna>();
    public DbSet<InteracaoCanal> InteracoesCanal => Set<InteracaoCanal>();
    public DbSet<UsuarioClinica> UsuariosClinica => Set<UsuarioClinica>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();
    public DbSet<ContaTutor> ContasTutor => Set<ContaTutor>();
    public DbSet<Consentimento> Consentimentos => Set<Consentimento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.HasDefaultSchema("KURA");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KuraDbContext).Assembly);

        // TASK-63: mapeamento de VW_TIMELINE_PET (keyless entity TimelineItem) removido —
        // TimelineRepository não consulta mais essa view (era a causa do ORA-00904; ver
        // TimelineRepository.cs). Órfão confirmado por grep: nenhum outro código referenciava
        // Kura.Infrastructure.Persistence.ReadModels.TimelineItem além deste mapeamento e do
        // FromSqlRaw removido — TimelineItemDto (Application layer, contrato HTTP) é uma classe
        // diferente e continua em uso normalmente.

        // Conversão global bool → CHAR(1) 'S'/'N' (convenção schema Flyway v3)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(bool) || property.ClrType == typeof(bool?))
                {
                    property.SetValueConverter(new BoolToSimNaoConverter());
                    property.SetColumnType("CHAR(1)");
                    property.SetMaxLength(1);
                }
            }
        }

        ApplyTenantFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Veterinario>()
            .HasQueryFilter(e => e.StAtiva &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro));

        modelBuilder.Entity<Pet>()
            .HasQueryFilter(e => e.StAtiva &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro));

        modelBuilder.Entity<EventoClinico>()
            .HasQueryFilter(e =>
                _clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro);

        modelBuilder.Entity<Notificacao>()
            .HasQueryFilter(e =>
                _clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro);

        modelBuilder.Entity<DispositivoIot>()
            .HasQueryFilter(e =>
                _clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro);

        modelBuilder.Entity<TriagemLuna>()
            .HasQueryFilter(e => e.StAtiva &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro));

        // TASK-67: InteracaoCanal também entra aqui (TenantFilterCoverageTests exige
        // filtro OU allowlist, e a allowlist está fechada só para Agendamento). Na
        // prática, os 3 endpoints consumidos pela Luna são chamados sem JWT de clínica
        // (autenticação por API Key — LunaApiKeyAuthFilter), então IdClinicaFiltro é
        // sempre null nessas chamadas e este filtro fica inerte.
        //
        // CORREÇÃO (fix round 1, Important-3 da revisão): a frase anterior aqui dizia
        // que o isolamento real vinha de "escopo explícito no LINQ" — impreciso. As
        // leituras de LunaService/TutorService são GetByIdAsync por PK (FindAsync), não
        // uma query LINQ escopada por IdClinica. O que existe de fato: ID_CLINICA da
        // linha ESCRITA é derivado do tutor (correto, e suficiente pra essa coluna); a
        // consistência entre ID_INTERACAO (FK) e o tutor é verificada explicitamente em
        // LunaService.RegistrarTriagemAsync (interacao.IdClinica != tutor.IdClinica →
        // 422), não por este query filter nem por uma query LINQ. O filtro continua
        // útil como defesa em profundidade para qualquer leitura futura desta tabela
        // feita com JWT de clínica (ex.: um relatório autenticado).
        //
        // TASK-77 (FIX_7): IdClinica virou `long?` — interação de tutor não identificado
        // grava com IdClinica null (decisão de produto do Felipe, ver
        // LunaService.RegistrarInteracaoAsync). Isso torna `e.IdClinica ==
        // _clinicaContext.IdClinicaFiltro` uma comparação nullable == nullable, e o
        // predicado abaixo escreve o `e.IdClinica != null` de propósito em vez de
        // confiar na tradução de null do EF Core: por padrão (sem UseRelationalNulls,
        // não configurado neste projeto — conferido em ServiceCollectionExtensions.cs) o
        // EF Core replica semântica de comparação C# (null == 5 é false, não UNKNOWN),
        // então a tradução implícita já produziria o resultado certo — mas depender
        // disso é exatamente o tipo de trivia de tradução que este projeto já foi
        // mordido por assumir sem verificar (ver nota em CLAUDE.md sobre "5xx nos logs"
        // do FIX_6). Comportamento provado em
        // InteracaoCanalTenantIsolationTests.SemJwt_InteracaoSemClinica_SempreAparece e
        // ComJwt_InteracaoSemClinica_NuncaAparece (InMemory — replicar contra Oracle
        // real é responsabilidade do G4 do ciclo, não desta suíte).
        modelBuilder.Entity<InteracaoCanal>()
            .HasQueryFilter(e => e.StAtiva &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 (e.IdClinica != null && e.IdClinica == _clinicaContext.IdClinicaFiltro)));

        // TASK-21: Tutor tinha apenas HasQueryFilter(StAtiva) em TutorConfiguration — sem
        // filtro de tenant, vazamento cross-clinica de PII (CPF, e-mail, telefone).
        // Consolidado aqui (removido de TutorConfiguration) porque duas chamadas
        // HasQueryFilter() para a mesma entidade NÃO se combinam com AND no EF Core 10:
        // a última registrada no pipeline de OnModelCreating substitui inteiramente a anterior.
        modelBuilder.Entity<Tutor>()
            .HasQueryFilter(e => e.StAtiva &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro));

        // FD-02 (ciclo FIN): USUARIO_CLINICA é a identidade individual do lado clínico
        // (V17__usuario_clinica.sql, repo Java). ID_CLINICA é NOT NULL — não existe
        // usuário sem tenant —, então o predicado é a forma simples das 8 anteriores,
        // sem a guarda de null que InteracaoCanal precisa.
        //
        // Este é o ÚNICO lugar onde o filtro desta entidade pode viver:
        // UsuarioClinicaConfiguration deliberadamente NÃO declara HasQueryFilter,
        // porque duas chamadas para a mesma entidade não se combinam com AND no EF
        // Core 10 — a segunda substitui a primeira (foi como Tutor perdeu o
        // isolamento até a TASK-21, ver comentário acima).
        //
        // Vale de novo a armadilha documentada em TenantFilterCoverageTests: com
        // IdClinicaFiltro null o filtro DESLIGA inteiro (não nega). Para esta tabela
        // isso importa mais que para as outras — quem lê USUARIO_CLINICA sem JWT é
        // justamente o login (FD-03), que ainda não tem clínica no contexto quando
        // resolve o usuário. A FD-03 precisa escopar a busca por (clínica, e-mail)
        // explicitamente no LINQ; este filtro não vai fazer isso por ela.
        // Comportamento provado em UsuarioClinicaTenantIsolationTests.
        modelBuilder.Entity<UsuarioClinica>()
            .HasQueryFilter(e => e.StAtiva &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro));
    }
}
