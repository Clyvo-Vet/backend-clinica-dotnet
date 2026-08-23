using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FluentValidation.AspNetCore;
using Microsoft.OpenApi;
using Kura.Api.Extensions;
using Kura.Infrastructure.Persistence;
using Oracle.ManagedDataAccess.Client;

// QuestPDF (geração de receituário, TASK-15): licença Community — gratuita para
// organizações com receita anual < US$1M (não é mais MIT puro desde 2023.12,
// mas segue free-first e sem custo para este projeto acadêmico).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Serilog — 3-arg overload so the built IServiceProvider is available for the Oracle sink
// S3D-01: sink de arquivo (rolling diário) ao lado do console — a rubrica pede
// "console/arquivo" e um dos dois já bastaria, mas o custo é baixo e remove ambiguidade
// de leitura. Caminho relativo ("logs/") funciona tanto local (fica ao lado do binário
// em execução) quanto em container (relativo ao WORKDIR da imagem), sem exigir volume
// dedicado nem configuração adicional.
builder.Host.UseSerilog((ctx, sp, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/kura-api-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
// S3D-03: health checks reais (API + Oracle + Luna), substituindo o antigo
// HealthController (200 incondicional). Ver Extensions/HealthCheckExtensions.cs.
builder.Services.AddKuraHealthChecks(builder.Configuration);
// S3D-04: tracing entre camadas + métricas de desempenho (OpenTelemetry, exporter
// Console). Ver Extensions/ObservabilityExtensions.cs para a decisão de cobertura
// (por que EntityFrameworkCore/Prometheus ficaram de fora).
builder.Services.AddKuraObservability();

// JWT
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
        };
    });
builder.Services.AddAuthorization();

// Swagger with JWT Bearer scheme
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "KURA API — Clyvo Vet", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe o token JWT no formato: Bearer {token}",
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
    // XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Validação de migrations pendentes — apenas aviso, NÃO aplica nada (schema é responsabilidade do Flyway)
// Retry com backoff exponencial para aguardar o serviço XEPDB1 registrar-se no listener Oracle
// S3D-05: o bloco abaixo abre conexão real com o Oracle no startup, o que inviabiliza
// subir o host num teste de integração (WebApplicationFactory<Program>). Não é questão de
// lentidão: sem este guard, o processo de teste MORRE no startup, com exceção não tratada
// — medido por mutação (reverter este arquivo para a versão sem o guard derruba a probe).
// As 10 tentativas nunca chegam a acontecer: a PRIMEIRA já derruba o processo.
// O filtro do catch compara ex.Number com o código EXTERNO. Nas duas falhas de conexão
// MEDIDAS neste repo (listener ausente; serviço não registrado no listener), o driver lança
// ORA-50201 e o código real de rede (ORA-12541 / ORA-12514) só aparece DOIS níveis abaixo,
// em NetworkException — que nem é OracleException, então não tem .Number para comparar.
// Logo o "when" não casa e o retry não roda nesses caminhos. Os outros 2 códigos da lista
// (1109, 17002) nunca foram exercitados — nada se afirma sobre eles.
// No cold start do compose isso foi observado como 0 linhas "Oracle não disponível (ORA-…)"
// e RestartCount 10: quem faz a stack convergir ali é a restart policy do Docker, não este
// loop (observação de raspão, ainda sem G0 dedicado). Consequência para quem for escrever
// documentação: NÃO credite o cold-start ao retry deste bloco — nos caminhos medidos, ele
// não executa.
// Fora do ambiente "Testing" nada muda: o bloco roda exatamente como antes.
// ⚠️ O guard só dispara se quem sobe o host pedir o ambiente explicitamente. O
// WebApplicationFactory usa "Development" por PADRÃO — a factory da suíte de integração
// PRECISA declarar UseEnvironment("Testing"), senão este bloco roda, carrega
// appsettings.Development.json e abre conexão contra o Oracle de lá.
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<KuraDbContext>();

        const int maxAttempts = 10;
        int[] retriableErrors = [12514, 1109, 12541, 17002];

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    app.Logger.LogWarning(
                        "Existem {Count} migrations pendentes no EF Core. " +
                        "ATENÇÃO: schema é aplicado pelo Flyway. Migrations EF servem apenas como evidência. " +
                        "Migrations pendentes: {Migrations}",
                        pendingMigrations.Count(),
                        string.Join(", ", pendingMigrations));
                }
                break;
            }
            catch (OracleException ex) when (retriableErrors.Contains(ex.Number) && attempt < maxAttempts)
            {
                var delaySecs = Math.Min(Math.Pow(2, attempt - 1), 60);
                app.Logger.LogWarning(
                    "Oracle não disponível (ORA-{ErrorCode}) — tentativa {Attempt}/{MaxAttempts}. " +
                    "Aguardando {Delay}s antes de nova tentativa...",
                    ex.Number, attempt, maxAttempts, delaySecs);
                await Task.Delay(TimeSpan.FromSeconds(delaySecs));
            }
        }
    }
}

// TASK-84: UseSerilogRequestLogging() precisa vir ANTES de ExceptionHandlerMiddleware
// (ordem, não nível — ver comentário completo em RequestPipelineExtensions).
app.UseRequestLoggingAndExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// S3D-03: substitui o antigo HealthController — não usar [AllowAnonymous] extra
// aqui, MapHealthChecks já não exige autenticação por padrão.
app.MapKuraHealthChecks("/health");

app.Run();

// S3D-05: declaração explícita para destravar WebApplicationFactory<Program> (S3D-06),
// que exige um tipo Program público, e precisa vir DEPOIS de todos os top-level statements.
// ⚠️ MEDIDO neste repo: em net10.0 o Program gerado a partir de top-level statements JÁ SAI
// public, e WebApplicationFactory<Program> compila COM e SEM esta linha. A receita clássica
// ("adicione isto porque o tipo seria internal") descreve TFMs anteriores; hoje é REDUNDANTE.
// O que governa é o TARGET FRAMEWORK, não a versão do SDK: com o MESMO SDK 10.0.203,
// net8.0 → NotPublic, net9.0 → NotPublic, net10.0 → Public. Mantida de propósito como
// fixação explícita da visibilidade, que sobrevive a um downgrade de TFM — mesmo raciocínio
// da guarda de null em KuraDbContext.ApplyTenantFilters.
// A linha é redundante sob REMOÇÃO, mas load-bearing sob mutação de acessibilidade: trocar
// "public" por "internal" aqui compila o Kura.Api normalmente e quebra só o consumidor, com
// CS0122 ("Program" é inacessível) — medido. Ou seja, o modificador escrito neste arquivo é
// quem governa; não é decoração.
// Alternativa descartada: InternalsVisibleTo (mais indireto, e sem precedente neste repo).
public partial class Program;
