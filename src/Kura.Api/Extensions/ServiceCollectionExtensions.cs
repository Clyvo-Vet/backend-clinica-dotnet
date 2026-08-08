namespace Kura.Api.Extensions;

using FluentValidation;
using Kura.Api.Services;
using Kura.Application.Services;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Interceptors;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Kura.Application.AssemblyMarker>();

        // GROUP A — Lookup/reference entities
        services.AddScoped<IEspecieService, EspecieService>();
        services.AddScoped<IRacaService, RacaService>();
        services.AddScoped<ITipoEventoService, TipoEventoService>();
        services.AddScoped<IMedicamentoService, MedicamentoService>();

        // GROUP B — Core clinic entities
        services.AddScoped<IClinicaService, ClinicaService>();
        services.AddScoped<IVeterinarioService, VeterinarioService>();
        services.AddScoped<ITutorService, TutorService>();
        services.AddScoped<IPetService, PetService>();

        // GROUP C — Clinical events
        services.AddScoped<IEventoClinicoService, EventoClinicoService>();
        services.AddScoped<IVacinaService, VacinaService>();
        services.AddScoped<IPrescricaoService, PrescricaoService>();
        services.AddScoped<IExameService, ExameService>();
        services.AddScoped<IConsultaService, ConsultaService>();

        // GROUP D — Supporting entities
        services.AddScoped<INotificacaoService, NotificacaoService>();
        services.AddScoped<IDispositivoIotService, DispositivoIotService>();
        services.AddScoped<ILeituraTemperaturaService, LeituraTemperaturaService>();
        services.AddScoped<IAlertaTemperaturaService, AlertaTemperaturaService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAgendaService, AgendaService>();
        services.AddScoped<ILunaService, LunaService>();
        services.AddScoped<ITeleconsultaService, TeleconsultaService>();
        services.AddScoped<ISoapDraftService, SoapDraftService>();
        services.AddScoped<IReceituarioPdfService, ReceituarioPdfService>();

        return services;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not configured.");

        services.AddDbContext<KuraDbContext>((sp, options) =>
        {
            options.UseOracle(
                connectionString,
                oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
            options.AddInterceptors(new ReadOnlyTablesInterceptor());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IClinicaContext, ClinicaContext>();
        services.AddScoped<Kura.Api.Filters.ApiKeyAuthFilter>();
        services.AddScoped<Kura.Api.Filters.LunaApiKeyAuthFilter>();

        // Repositórios especializados
        services.AddScoped<IClinicaRepository, ClinicaRepository>();
        services.AddScoped<IVeterinarioRepository, VeterinarioRepository>();
        services.AddScoped<ITutorRepository, TutorRepository>();
        services.AddScoped<IPetRepository, PetRepository>();
        services.AddScoped<ITutorPetRepository, TutorPetRepository>();
        services.AddScoped<IEventoClinicoRepository, EventoClinicoRepository>();
        services.AddScoped<ITimelineRepository, TimelineRepository>();
        services.AddScoped<IAgendamentoRepository, AgendamentoRepository>();
        services.AddScoped<IAgendamentoReadRepository, AgendaReadRepository>();
        services.AddScoped<ITriagemLunaRepository, TriagemLunaRepository>();
        services.AddScoped<IInviteTutorRepository, InviteTutorRepository>();
        services.AddScoped<IConsentimentoRepository, ConsentimentoRepository>();

        services.AddHttpClient<IDailyService, DailyService>((sp, http) =>
        {
            var apiKey = configuration["Daily:ApiKey"]
                ?? throw new InvalidOperationException("Daily:ApiKey not configured.");
            http.BaseAddress = new Uri("https://api.daily.co/v1/");
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        });

        services.AddHttpClient<ILunaTranscricaoService, LunaTranscricaoService>((sp, http) =>
        {
            var baseUrl = configuration["Luna:BaseUrl"]
                ?? throw new InvalidOperationException("Luna:BaseUrl not configured.");
            var apiKey = configuration["Luna:InboundApiKey"]
                ?? throw new InvalidOperationException("Luna:InboundApiKey not configured.");
            http.BaseAddress = new Uri(baseUrl);
            http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        });

        return services;
    }
}
