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

app.Run();
