# Política de Migrations — KURA API
## Fluxo Híbrido EF Core + Flyway

Este projeto utiliza um fluxo híbrido onde:

- **EF Core é o GERADOR**: arquivos `.cs` em `Migrations/` são gerados via
  `dotnet ef migrations add` e ficam commitados no repositório.
- **Flyway é o EXECUTOR**: aplica o schema no banco Oracle. Os scripts
  Flyway vivem em outro repositório do time.

## Regras inegociáveis

- **NUNCA** chamar `Database.Migrate()` no `Program.cs`.
- **NUNCA** chamar `Database.EnsureCreated()` em código de produção.
- Toda nova migration deve ser gerada localmente com:

```bash
dotnet ef migrations add <Nome> \
  --project src/Kura.Infrastructure \
  --startup-project src/Kura.Api
```

- Após gerar, extrair o SQL com:

```bash
dotnet ef migrations script <Anterior> <Nova> \
  --project src/Kura.Infrastructure \
  --startup-project src/Kura.Api \
  --output migrations-sql/<Nova>.sql
```

O SQL extraído é entregue ao responsável pelo Flyway.

## Por que esse fluxo?

- A rubrica FIAP (Advanced .NET, item 3d) exige "Uso de Migrations" (5 pontos).
  Apagar a pasta `Migrations/` zeraria esse critério.
- O banco Oracle é compartilhado com o backend Java. Apenas um sistema pode
  aplicar o schema. Por acordo de time, esse sistema é o Flyway.
- O `.cs` continua sendo evidência de competência técnica para o avaliador.
