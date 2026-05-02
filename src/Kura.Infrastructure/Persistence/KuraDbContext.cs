namespace Kura.Infrastructure.Persistence;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
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
    public DbSet<LogErro> LogsErro => Set<LogErro>();
    public DbSet<TimelineItem> TimelineItems => Set<TimelineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.HasDefaultSchema("KURA");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KuraDbContext).Assembly);

        modelBuilder.Entity<TimelineItem>()
            .HasNoKey()
            .ToView("VW_TIMELINE_PET");

        ApplyTenantFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Veterinario>()
            .HasQueryFilter(e => e.StAtiva == 'S' &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro));

        modelBuilder.Entity<Pet>()
            .HasQueryFilter(e => e.StAtiva == 'S' &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro));

        modelBuilder.Entity<EventoClinico>()
            .HasQueryFilter(e => e.StAtiva == 'S' &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro));

        modelBuilder.Entity<Notificacao>()
            .HasQueryFilter(e => e.StAtiva == 'S' &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro));

        modelBuilder.Entity<DispositivoIot>()
            .HasQueryFilter(e => e.StAtiva == 'S' &&
                (_clinicaContext.IdClinicaFiltro == null ||
                 e.IdClinica == _clinicaContext.IdClinicaFiltro));
    }
}
