# Deploy de Produção - Cost Tracker (com Caddy do BloodWatch)

Este guia assume que:
- o Caddy público já está no projeto **BloodWatch**
- o Cost Tracker vai rodar sem Caddy próprio
- o PostgreSQL já existe no servidor

## Visão de arquitetura

- BloodWatch Caddy continua dono das portas `80/443`
- Cost Tracker expoe `18080/18081` no host para o Caddy de outro stack Docker
- PostgreSQL externo existente (host)
- Firewall/security group deve bloquear acesso externo direto a `18080/18081` e `5432`

## 1) Preparar banco (reaproveitando PostgreSQL existente)

Nao e obrigatorio criar usuario novo.

Se quiser reaproveitar usuario existente (ex.: `bloodwatch`), crie so a base:

```bash
psql -h 127.0.0.1 -U postgres -c "CREATE DATABASE costtracker OWNER bloodwatch;"
```

Ou com usuario dedicado:

```bash
psql -h 127.0.0.1 -U postgres -c "CREATE ROLE costtracker WITH LOGIN PASSWORD 'SENHA_FORTE_AQUI';"
psql -h 127.0.0.1 -U postgres -c "CREATE DATABASE costtracker OWNER costtracker;"
```

## 2) Preparar projeto no servidor

```bash
sudo mkdir -p /opt/cost-tracker
sudo chown -R "$USER":"$USER" /opt/cost-tracker
cd /opt/cost-tracker
git clone https://github.com/igorbmaciel94/cost-tracker.git .
cp deploy/.env.prod.example deploy/.env.prod
```

Edite `deploy/.env.prod`:

- `IMAGE_TAG=main`
- `APP_WEB_BIND_ADDRESS=0.0.0.0`
- `APP_WEB_BIND_PORT=18080`
- `APP_API_BIND_ADDRESS=0.0.0.0`
- `APP_API_BIND_PORT=18081`
- `DB_HOST=host.docker.internal`
- `DB_PORT=5432`
- `DB_NAME=costtracker`
- `DB_USER=<seu usuario postgres>`
- `DB_PASSWORD=<sua senha>`
- `DB_SSL_MODE=Disable`
- `AUTH_USERNAME=<username do app>`
- `AUTH_PASSWORD_HASH=<hash gerado pelo utilitário>`
- `GHCR_USERNAME` e `GHCR_TOKEN`

Gerar hash:

```bash
dotnet run --project backend/CostTracker.PasswordHash -- <username> <password>
```

## 3) Primeiro deploy do Cost Tracker

```bash
cd /opt/cost-tracker
./deploy/deploy.sh
```

Validar stack:

```bash
docker compose --env-file deploy/.env.prod -f deploy/docker-compose.prod.yml ps
curl -I http://127.0.0.1:18080
curl -sS http://127.0.0.1:18081/api/health
```

Checagem rapida de conflito de portas (esperado):
- BloodWatch usa `80/443` publicamente (Caddy).
- Cost Tracker usa `18080` (web) e `18081` (api) para roteamento interno via Caddy.
- Postgres do Cost Tracker nao sobe container proprio.

## 4) Ajustar Caddy do BloodWatch para publicar o dominio do Cost Tracker

No projeto BloodWatch (exemplo: `/opt/bloodwatch/compose/Caddyfile`), adicione:

```caddy
cost.lighthousedev.uk {
  encode zstd gzip

  # compatibilidade temporaria para builds antigas do frontend
  @api_dup path /api/api/*
  handle @api_dup {
    uri strip_prefix /api
    reverse_proxy host.docker.internal:18081
  }

  @api path /api/*
  reverse_proxy @api host.docker.internal:18081

  reverse_proxy host.docker.internal:18080
}
```

No compose de producao do BloodWatch (exemplo: `/opt/bloodwatch/compose/docker-compose.prod.yml`), no servico `caddy`, adicione:

```yaml
extra_hosts:
  - "host.docker.internal:host-gateway"
```

Aplicar no BloodWatch:

```bash
docker compose -f /opt/bloodwatch/compose/docker-compose.prod.yml --env-file /opt/bloodwatch/compose/.env up -d --force-recreate caddy
```

## 5) Validar domínio público

```bash
curl -I https://cost.lighthousedev.uk
curl -sS https://cost.lighthousedev.uk/api/health
```

## 6) Atualização manual do Cost Tracker

```bash
cd /opt/cost-tracker
./deploy/deploy.sh main
# ou
./deploy/deploy.sh sha-abcdef1
```

## 7) Deploy manual via GitHub Actions

Workflow: **Deploy Manual**

Secrets necessários:
- `DEPLOY_HOST=46.225.216.71`
- `DEPLOY_USER`
- `DEPLOY_SSH_KEY`
- `GHCR_USERNAME`
- `GHCR_TOKEN`

## 8) Backup (opcional)

Sem backup por enquanto:
- nao configure cron
- nao rode `backup.sh`
- pode ignorar as variaveis `BACKUP_*` no `.env.prod`

Com backup diário:

```bash
/opt/cost-tracker/deploy/backup.sh
```

Cron:

```cron
0 3 * * * /opt/cost-tracker/deploy/backup.sh >> /opt/cost-tracker/backups/backup.log 2>&1
```

## 9) Rollback

```bash
cd /opt/cost-tracker
./deploy/deploy.sh sha-TAG_ANTIGA
```
