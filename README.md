# Cost Tracker MVP

Aplicação simples para substituir a planilha mensal de custos.

## Stack

- Backend: ASP.NET Core Web API + EF Core + PostgreSQL
- Frontend: React + Vite + TypeScript
- Infra local: Docker Compose

Nota: o ambiente local de geração deste projeto só possui SDK .NET 9, então os projetos foram criados em `net9.0`.

## Funcionalidades

- Orçamento por categoria com previsto, gasto e diferença
- Lançamentos com data, categoria, descrição e valor
- Login obrigatório com cookie de sessão e credenciais vindas do ambiente
- Metas por grupo (`Essenciais`, `Desejos`, `Investimento`, `Saving`, `Buffer`)
- Dashboard com gráficos simples
  - treemap: saldo por categoria
  - rosca: saldo por grupo
- Virada manual de mês (`Novo mês`) com clonagem e reset de lançamentos
- Histórico de meses fechados em modo somente leitura
- Seed inicial com dados da planilha

## Como rodar

```bash
export AUTH_USERNAME=admin
export AUTH_PASSWORD_HASH="$(dotnet run --project backend/CostTracker.PasswordHash -- admin 'troque-esta-senha')"
docker compose up --build
```

Depois abra:

- Frontend: http://localhost:5173
- API: http://localhost:8080

Credenciais:

- username: valor de `AUTH_USERNAME`
- password: a senha usada ao gerar `AUTH_PASSWORD_HASH`

## Deploy em servidor

Veja o runbook completo em [README-deploy.md](README-deploy.md).

## Estrutura

- `/Users/igorbmaciel/cost-tracker/backend`
- `/Users/igorbmaciel/cost-tracker/frontend`
- `/Users/igorbmaciel/cost-tracker/docker-compose.yml`

## Endpoints principais

- `GET /api/months`
- `POST /api/months/new`
- `PUT /api/months/{monthId}/salary`
- `GET /api/months/{monthId}/budget`
- `POST /api/months/{monthId}/budget/categories`
- `PUT /api/months/{monthId}/budget/categories/{categoryId}`
- `DELETE /api/months/{monthId}/budget/categories/{categoryId}`
- `GET /api/months/{monthId}/entries`
- `POST /api/months/{monthId}/entries`
- `PUT /api/months/{monthId}/entries/{entryId}`
- `DELETE /api/months/{monthId}/entries/{entryId}`
- `GET /api/months/{monthId}/targets`
- `PUT /api/months/{monthId}/targets`
- `GET /api/months/{monthId}/dashboard`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/session`
- `GET /api/health`

## Testes

- Backend unit + integração em `/Users/igorbmaciel/cost-tracker/backend/CostTracker.Tests`
- Frontend unit em `/Users/igorbmaciel/cost-tracker/frontend/src`

Comandos:

```bash
cd backend && dotnet test
cd frontend && npm test
```
