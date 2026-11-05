# Decisões técnicas

## 1. Arquitetura

- **Clean Architecture em 4 projetos** (`Domain`, `Application`, `Infrastructure`, `Api`). O Domain não referencia nada; a Application só conhece o Domain e define as portas (`IOrderRepository`, `IProductRepository`, `IUnitOfWork`, `IOrderReadRepository`, `ICurrentUser`, `ITokenIssuer`...). Infrastructure implementa as portas; a Api só compõe e traduz HTTP.
- **Um handler por caso de uso** (`CreateOrderHandler`, `ConfirmOrderHandler`, ...) em vez de um "OrderService" genérico: cada classe tem uma responsabilidade e dependências mínimas (SRP/ISP). Não usei MediatR — para seis casos de uso a indireção não se paga, e a licença comercial a partir da v13 é um custo desnecessário.
- **CQRS leve**: escrita carrega o agregado rastreado (repositórios); leitura usa `IOrderReadRepository` com `AsNoTracking` e projeção direta para DTO — nunca materializa o agregado para responder GET.
- **Minimal APIs** com `MapGroup`, metadata de OpenAPI e endpoints finos (mapeiam request → command e `Result` → `IResult`).
- **Central Package Management** (`Directory.Packages.props`) e `TreatWarningsAsErrors` em todos os projetos.

## 2. Domínio

- **`Order` é o agregado raiz**; itens só mudam por ele. Construtores privados + métodos de fábrica/transição (`Place`, `Confirm`, `Cancel`) garantem as invariantes: pedido sem itens, quantidade ≤ 0, total inconsistente ou transição inválida são impossíveis de construir.
- **Pedido nasce `Placed`** (não `Draft`): o enunciado não tem endpoint para editar rascunho, então `Draft` existiria só como estado transitório dentro de `Place`. O enum mantém `Draft` para evolução futura (carrinho), mas nenhum fluxo atual o persiste.
- **Itens com mesmo produto são consolidados** em uma linha. Isso faz a validação de estoque considerar a quantidade total pedida daquele produto e permite a chave natural `(order_id, product_id)`.
- **Preço é snapshot**: `OrderItem.UnitPrice` copia o preço do produto no momento do pedido; mudanças posteriores de preço não alteram pedidos existentes. `Total` é persistido (desnormalizado) para listar sem join/agregação.
- **`Currency` é value object** (ISO 4217, normalizado para maiúsculas). Regra adicional: o produto precisa estar cotado na moeda do pedido — somar preço em USD num pedido em BRL produziria um total sem sentido. Conversão de câmbio ficou fora do escopo.
- **Erros esperados são valores (`Result`/`Error`)**, não exceções. Exceções ficam para falhas inesperadas (bug, banco fora, conflito de concorrência que esgotou retry). Cada `Error` tem `Code` estável e `Type`, que a Api mapeia para status HTTP.

## 3. Modelo de estoque

- `Product.AvailableQuantity` = o que ainda pode ser vendido.
- **Criar** valida contra o disponível, mas **não reserva** (o enunciado diz "não pode exceder estoque disponível" na criação e "confirmação deve reservar/baixar").
- **Confirmar** checa todos os itens antes de alterar qualquer um (falha atômica no domínio) e então decrementa.
- **Cancelar** um pedido `Confirmed` devolve as quantidades; cancelar `Placed` não mexe no estoque.
- Consequência conhecida: um pedido `Placed` pode não conseguir ser confirmado se outro consumiu o estoque entre a criação e a confirmação — a confirmação retorna `409 product.insufficient_stock`. Para reservar já na criação bastaria mover o `Reserve` para `Place`; preferi seguir a semântica do enunciado.
- Check constraint `available_quantity >= 0` no banco como última linha de defesa.

## 4. Idempotência e concorrência

- **Idempotência por estado**: confirmar um pedido já `Confirmed` (ou cancelar um já `Canceled`) retorna `200` com o mesmo corpo e não toca no estoque. Não foi necessário `Idempotency-Key` porque as operações são transições para um estado-alvo. Para `POST /orders` (não idempotente por natureza), o próximo passo seria aceitar `Idempotency-Key` e guardar a resposta.
- **Mesma ordem, requisições simultâneas** (ex.: dois `confirm` ao mesmo tempo): o handler roda numa transação e carrega o pedido com `SELECT ... FOR UPDATE`. A segunda requisição espera a primeira terminar e já enxerga o pedido `Confirmed` → responde 200 sem reservar de novo. Sem o lock existia *read skew* (ler o pedido antes do commit concorrente e o estoque depois), que eu reproduzi em teste e fazia a duplicata responder 409.
- **Pedidos diferentes disputando o mesmo produto**: concorrência otimista com a coluna de sistema `xmin` do Postgres como token, mapeada como *shadow property* no EF (o domínio não conhece esse detalhe de persistência). Em conflito, `OptimisticRetry` limpa o change tracker, recarrega e reexecuta (até 3 tentativas); persistindo, retorna `409 concurrency_conflict` (o cliente pode repetir).
- Isso está coberto em `OrderService.Infrastructure.IntegrationTests` contra Postgres real: confirms/cancels duplicados em paralelo alteram o estoque exatamente uma vez, e 4 pedidos de 2 unidades sobre estoque 5 nunca vendem mais que 4.
- Optei por lock pessimista só na linha do pedido (contenção baixa: é o mesmo pedido) e otimista no produto (linha "quente", compartilhada entre pedidos), evitando segurar lock em produto e deadlocks entre pedidos com itens em ordens diferentes.

## 5. Persistência (EF Core)

- Mapeamento explícito via `IEntityTypeConfiguration`; tabelas e colunas em snake_case; `numeric(18,2)` para dinheiro; `character(3)` para moeda; status como texto (legível no banco e estável a reordenação do enum).
- Coleção de itens encapsulada (`IReadOnlyCollection` + backing field), configurada com `PropertyAccessMode.Field`.
- **Índices** pensados para a listagem: `(customer_id, created_at)`, `(status, created_at)` e `(created_at)` — cobrem os filtros mais comuns combinados com o intervalo de datas e a ordenação padrão (mais recentes primeiro, desempate por `id` para paginação estável).
- Listagem faz `COUNT` + página com `Skip/Take` e projeção (`ItemsCount` via subquery, sem carregar itens). Pula a segunda query quando o total é zero. Para volumes muito grandes, a evolução seria paginação por cursor (keyset) em `(created_at, id)`.
- Busca de produtos em lote (`WHERE id = ANY(@ids)`) — uma ida ao banco independentemente do número de itens.
- **Migrations versionadas** no projeto Infrastructure, aplicadas no startup (`Database:ApplyMigrationsOnStartup`, ligado por padrão), com retry enquanto o banco sobe. Em produção com várias réplicas eu moveria isso para um job/initContainer ou um bundle (`dotnet ef migrations bundle`) para não migrar concorrentemente.
- Seed do catálogo via `HasData` (IDs fixos para o README funcionar).

## 6. Segurança

- `POST /auth/token` emite JWT HS256 (issuer, audience, expiração, `sub`, `role`, `customer_id`, `jti`). A mesma `JwtOptions` alimenta emissão e validação (fonte única); a chave é validada no startup (≥ 32 caracteres).
- **Fallback policy exige usuário autenticado** em tudo; só `/auth/token` e `/health` são anônimos.
- **Autorização**: policy `orders` exige `admin` ou `customer` com `customer_id` válido. A regra de *ownership* fica na Application (`ICurrentUser.CanAccessCustomer`): cliente só opera os próprios pedidos; pedido de outro cliente responde **404** (não revela existência); criar/listar para outro cliente responde **403**.
- Usuários de demonstração ficam em configuração com senha em **hash PBKDF2-SHA256** (100k iterações, salt aleatório), comparação em tempo constante e verificação "dummy" quando o usuário não existe (mitiga enumeração por tempo). Num sistema real a emissão de token seria de um IdP (Keycloak, Entra ID, Auth0) e a API só validaria.
- A chave JWT em `appsettings.json`/`docker-compose.yml` é de demonstração; em produção viria de secret store/variável de ambiente.
- Container roda como usuário não-root (`$APP_UID` da imagem oficial).

## 7. API e erros

- Respostas de erro em ProblemDetails (RFC 7807) com `code` estável; `IExceptionHandler` global converte exceções inesperadas em 500 sem vazar detalhes, e `ConcurrencyConflictException` em 409.
- Enums serializados como string; filtros de status aceitam qualquer caixa (`placed`, `Placed`); datas de filtro são normalizadas para UTC.
- `GET /health` verifica conectividade com o banco (usado para observabilidade/orquestração).

## 8. Testes

- Pirâmide: muitos testes de domínio (rápidos, sem infraestrutura), testes de caso de uso com fakes escritos à mão (legíveis, sem framework de mock), testes HTTP com `WebApplicationFactory` (pipeline real, banco InMemory) e uma camada pequena contra **Postgres real** para o que o InMemory não reproduz (transações, `FOR UPDATE`, `xmin`, tradução de queries).
- Os testes de Postgres usam Testcontainers e são pulados automaticamente sem Docker, para que `dotnet test` nunca falhe por ambiente.
- `FluentAssertions` fixado na 6.x (a 8.x mudou para licença comercial).

## 9. O que eu faria a seguir

- `Idempotency-Key` no `POST /orders`.
- Outbox + eventos de domínio (`OrderConfirmed`, `OrderCanceled`) para integrar estoque/faturamento de forma assíncrona.
- Paginação por cursor, rate limiting no `/auth/token`, OpenTelemetry (traces/métricas) e logs estruturados.
- Endpoints de catálogo/estoque (hoje só via seed).
- Refresh token e IdP externo.
