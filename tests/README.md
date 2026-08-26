# Testes — convenção Unit × Integration

Este documento é o que `Kura.IntegrationTests.ConvencaoDeTestes` referencia. Ele existe para que
a convenção não viva apenas num comentário XML.

## Os dois recortes, e por que convivem

O repositório separa testes por **camada de arquitetura**; a rubrica da Sprint 3 nomeia o par
**(Unit, Integration)**. Os dois recortes coexistem:

| Projeto | Recorte | O que faz |
|---|---|---|
| `tests/Kura.Domain.Tests` | **Unit** | regras de domínio, em processo |
| `tests/Kura.Application.Tests` | **Unit** | services com dependências dubladas |
| `tests/Kura.Infrastructure.Tests` | **Unit** | políticas de persistência, EF InMemory, sem host HTTP |
| `tests/Kura.IntegrationTests` | **Integration** | sobe o `Program.cs` real e faz requisições HTTP ponta a ponta |

Os 3 projetos por camada **não foram renomeados** de propósito: o churn quebraria referências e
histórico sem ganho.

## Como rodar cada recorte

```bash
dotnet test KuraApi.slnx                                    # tudo
dotnet test KuraApi.slnx --filter "Categoria=Integracao"    # só integração
dotnet test KuraApi.slnx --filter "Categoria!=Integracao"   # só unitários
```

Contagens medidas (branch `s3d-07-collection-fixtures`): **304** sem filtro · **19** com
`Categoria=Integracao` · **285** com `Categoria!=Integracao`. **19 + 285 = 304.**

⚠️ O segundo filtro funciona porque o `!=` do VSTest casa **também** teste que não declara a
propriedade. É por isso que os ~40 arquivos unitários não precisaram ser anotados um a um — e é
por isso que uma classe de integração que **esqueça** o `[Trait]` cai silenciosamente no balde
unitário sem quebrar nada. `ConvencaoDeTestesCoverageTests` existe exatamente para impedir isso.

## Fixtures

- **Collection Fixture** (integração): `ColecaoDeIntegracao` — uma `KuraApiFactory` compartilhada
  por `AutenticacaoHttpTests` e `FluxoDeNegocioHttpTests`.
- **Class Fixture** (unitário): `ModeloEfInMemoryFixture` — consumida por
  `InteracaoCanalColumnTypesTests`.
- `AmbienteEFiacaoDoHostTests` fica **fora** da collection, com `IClassFixture` próprio, por razão
  medida (o teste do `/health` custa ~2,1s no health check `luna`; numa collection separada esse
  custo roda em paralelo). Ver o XML da classe.

🔴 **Restrição de quem acrescentar teste em `ColecaoDeIntegracao`:** as 2 classes compartilham o
mesmo banco InMemory. Asserção do tipo `HaveCount(n)` sobre recurso que qualquer teste da
collection crie **passa a depender da ordem de execução**, que o xUnit não garante — verificado
por mutação no G2 desta task (a mesma asserção passa na ordem padrão e falha na ordem reversa).
