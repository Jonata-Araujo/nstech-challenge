# Order Service

API REST de gestão de pedidos com itens e controle de estoque — solução do teste técnico .NET Sênior.

**Stack:** .NET 8 · ASP.NET Core Minimal APIs · EF Core 8 + Npgsql · PostgreSQL 16 · JWT · xUnit · Docker Compose

Decisões técnicas detalhadas: [`docs/decisions.md`](docs/decisions.md).

---

## Como rodar

### Docker (API + banco)

```bash
docker compose up --build
```

- API: http://localhost:8080
- Swagger: http://localhost:8080/swagger
- Health check: http://localhost:8080/health

As migrations são aplicadas automaticamente na subida da API (com retry enquanto o Postgres inicializa) e já criam um catálogo de produtos de exemplo.

### Local (sem Docker para a API)

Pré-requisito: .NET 8 SDK e um Postgres acessível (pode subir só o banco: `docker compose up db`).

```bash
dotnet run --project src/OrderService.Api
```

Sobe em http://localhost:5080 (Swagger em `/swagger`). A connection string fica em `src/OrderService.Api/appsettings.json` (`ConnectionStrings:OrdersDb`) e pode ser sobrescrita por variável de ambiente `ConnectionStrings__OrdersDb`.

## Como testar

```bash
dotnet test
```

| Projeto | O que cobre | Dependências |
|---|---|---|
| `OrderService.Domain.Tests` | Invariantes do agregado `Order`, `Product` e `Currency` (criação, transições, idempotência, estoque) | nenhuma |
| `OrderService.Application.Tests` | Casos de uso com fakes: autorização por cliente, validações, retry de concorrência, paginação | nenhuma |
| `OrderService.Api.IntegrationTests` | Pipeline HTTP completo (`WebApplicationFactory`): JWT, status codes, ProblemDetails, fluxos ponta a ponta | nenhuma (EF InMemory) |
| `OrderService.Infrastructure.IntegrationTests` | **Postgres real**: migrations, lock de linha, `xmin`, corrida entre requisições concorrentes | Docker (Testcontainers) |

Os testes de Postgres sobem um container automaticamente via Testcontainers. Sem Docker disponível eles são **pulados** (não falham), então `dotnet test` passa em qualquer máquina. Também é possível apontar para um Postgres existente:

```bash
ORDERS_TEST_CONNECTION_STRING="Host=localhost;Database=orders_tests;Username=postgres;Password=postgres" dotnet test
```

## Como usar

### Usuários de demonstração

| Usuário | Senha | Perfil | CustomerId |
|---|---|---|---|
| `admin` | `Admin@123` | admin — acessa pedidos de qualquer cliente | — |
| `customer1` | `Customer@123` | customer | `c1a1e7b4-0000-4000-8000-000000000001` |
| `customer2` | `Customer@123` | customer | `c1a1e7b4-0000-4000-8000-000000000002` |

Um `customer` só cria, vê, lista, confirma e cancela os **próprios** pedidos.

### Produtos (seed)

| ProductId | Produto | Preço | Estoque |
|---|---|---|---|
| `8f2b6c1e-0001-4a6e-9b1a-000000000001` | Notebook Pro 14 | BRL 4.500,00 | 10 |
| `8f2b6c1e-0002-4a6e-9b1a-000000000002` | Mouse sem fio | BRL 150,00 | 100 |
| `8f2b6c1e-0003-4a6e-9b1a-000000000003` | Teclado mecânico | BRL 350,00 | 50 |
| `8f2b6c1e-0004-4a6e-9b1a-000000000004` | Monitor 27" | BRL 1.200,00 | 5 |
| `8f2b6c1e-0005-4a6e-9b1a-000000000005` | Headset (importado) | USD 80,00 | 20 |

### Fluxo com curl

```bash
# 1. Token
TOKEN=$(curl -s -X POST http://localhost:8080/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"customer1","password":"Customer@123"}' | jq -r .accessToken)

# 2. Criar pedido (nasce Placed)
curl -s -X POST http://localhost:8080/orders \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{
        "customerId": "c1a1e7b4-0000-4000-8000-000000000001",
        "currency": "BRL",
        "items": [
          { "productId": "8f2b6c1e-0001-4a6e-9b1a-000000000001", "quantity": 1 },
          { "productId": "8f2b6c1e-0002-4a6e-9b1a-000000000002", "quantity": 2 }
        ]
      }'

# 3. Confirmar / cancelar (idempotentes — pode chamar quantas vezes quiser)
curl -s -X POST http://localhost:8080/orders/{id}/confirm -H "Authorization: Bearer $TOKEN"
curl -s -X POST http://localhost:8080/orders/{id}/cancel  -H "Authorization: Bearer $TOKEN"

# 4. Consultar e listar
curl -s http://localhost:8080/orders/{id} -H "Authorization: Bearer $TOKEN"
curl -s "http://localhost:8080/orders?status=Placed&from=2026-01-01T00:00:00Z&page=1&pageSize=20" -H "Authorization: Bearer $TOKEN"
```

Também há um arquivo pronto em [`src/OrderService.Api/OrderService.Api.http`](src/OrderService.Api/OrderService.Api.http) (VS / Rider / REST Client).

### Endpoints

| Método | Rota | Descrição | Sucesso |
|---|---|---|---|
| POST | `/auth/token` | Emite JWT (anônimo) | 200 |
| POST | `/orders` | Cria pedido em `Placed` | 201 + `Location` |
| POST | `/orders/{id}/confirm` | `Placed → Confirmed`, reserva estoque. Idempotente | 200 |
| POST | `/orders/{id}/cancel` | `Placed/Confirmed → Canceled`, libera estoque reservado. Idempotente | 200 |
| GET | `/orders/{id}` | Pedido + itens | 200 |
| GET | `/orders?customerId=&status=&from=&to=&page=&pageSize=` | Listagem paginada (padrão 20, máx. 100), mais recentes primeiro | 200 |

Erros seguem RFC 7807 (`application/problem+json`) com um `code` estável, por exemplo:

```json
{ "status": 409, "title": "product.insufficient_stock", "code": "product.insufficient_stock",
  "detail": "Insufficient stock for product '...': requested 6, available 5." }
```

| Status | Quando |
|---|---|
| 400 | Validação: sem itens, quantidade ≤ 0, produto inexistente, moeda inválida/diferente da do produto, paginação/filtros inválidos |
| 401 | Sem token, token inválido/expirado, credenciais erradas |
| 403 | Cliente tentando criar/listar pedidos de outro cliente |
| 404 | Pedido inexistente **ou de outro cliente** (não revela existência) |
| 409 | Estoque insuficiente, transição inválida (ex.: confirmar cancelado), conflito de concorrência persistente |

## Estrutura

```
src/
  OrderService.Domain          Agregados (Order, Product), value object Currency, Result/Error. Sem dependências.
  OrderService.Application     Casos de uso (handlers), DTOs, portas (repositórios, UoW, usuário atual, JWT).
  OrderService.Infrastructure  EF Core/Npgsql, migrations, repositórios, consultas de leitura, emissão de JWT.
  OrderService.Api             Minimal APIs, autenticação/autorização, ProblemDetails, Swagger.
tests/
  OrderService.Domain.Tests
  OrderService.Application.Tests
  OrderService.Api.IntegrationTests
  OrderService.Infrastructure.IntegrationTests
```

Dependências apontam para dentro: `Api → Application ← Infrastructure`, e todos → `Domain`.

### Nova migration

```bash
dotnet tool install --global dotnet-ef --version 8.*
dotnet ef migrations add <Nome> --project src/OrderService.Infrastructure --startup-project src/OrderService.Api --output-dir Persistence/Migrations
```
