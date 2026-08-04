namespace Kura.Infrastructure.Persistence;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence.Converters;
using Kura.Infrastructure.Persistence.ReadModels;
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
    public DbSet<TimelineItem> TimelineItems => Set<TimelineItem>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<TriagemLuna> TriagensLuna => Set<TriagemLuna>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();
    public DbSet<ContaTutor> ContasTutor => Set<ContaTutor>();
    public DbSet<Consentimento> Consentimentos => Set<Consentimento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.HasDefaultSchema("KURA");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KuraDbContext).Assembly);

        modelBuilder.Entity<TimelineItem>()
            .HasNoKey()
            .ToView("VW_TIMELINE_PET");

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

        // TASK-21: Tutor tinha apenas HasQueryFilter(StAtiva) em TutorConfiguration — sem
        // filtro de tenant, vazamento cross-clinica de PII (CPF, e-mail, telefone).
        // Consolidado aqui (removido de TutorConfiguration) porque duas chamadas
        // HasQueryFilter() para a mesma entidade NÃO se combinam com AND no EF Core 10:
        // a última registrada no pipeline de OnModelCreating substitui inteiramente a anterior.
        modelBuilder.Entity<Tutor>()
            .HasQueryFilter(e => e.StAtiva &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro));
    }
}
