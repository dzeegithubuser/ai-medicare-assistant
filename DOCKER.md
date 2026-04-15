# Docker Deployment Guide

## Prerequisites
- Docker Desktop installed
- Copy `.env.example` → `.env` and fill in real API keys/secrets
- Run all commands from workspace root

## Commands (PowerShell — Windows)
| Scenario                       | Command                         |
|--------------------------------|---------------------------------|
| First deploy / Redeploy       | `.\deploy.ps1`                  |
| Full restart (down + rebuild) | `.\deploy.ps1 -Action restart`  |
| Stop everything               | `.\deploy.ps1 -Action down`     |
| View logs                     | `.\deploy.ps1 -Action logs`     |
| Start without rebuild         | `.\deploy.ps1 -Action up`       |

## Commands (Bash — Linux/macOS)
| Scenario                       | Command                |
|--------------------------------|------------------------|
| First deploy / Redeploy       | `./deploy.sh --build`  |
| Full restart                  | `./deploy.sh --restart` |
| Stop everything               | `./deploy.sh --down`   |
| View logs                     | `./deploy.sh --logs`   |
| Start without rebuild         | `./deploy.sh`          |

## Endpoints
- **UI:**  http://localhost:9600
- **API:** http://localhost:5024

## Architecture
```
┌─────────────┐       ┌─────────────────┐
│  Browser     │──────▶│  ui (Nginx)     │ :9600
│              │       │  /api/* ────────┼──▶ api:5024
│              │       │  /hubs/* ───────┼──▶ api:5024 (WebSocket)
│              │       │  /* ── SPA ──── │
└─────────────┘       └─────────────────┘
                              │
                       ┌──────▼──────────┐
                       │  api (.NET 10)  │ :5024
                       │  ├─ MySQL       │──▶ 169.61.105.110:3306
                       │  ├─ MongoDB     │──▶ 169.61.105.110:27017
                       │  └─ FP API      │──▶ 169.61.105.110:8080
                       └─────────────────┘
```

## Key Files
| File                                          | Purpose                              |
|-----------------------------------------------|--------------------------------------|
| `docker-compose.yml`                          | Orchestrates both containers         |
| `api-ai-medicare-assistant/Dockerfile`            | .NET 10 multi-stage build            |
| `ui-ai-medicare-assistant/Dockerfile`             | Node 22 build → Nginx serve          |
| `ui-ai-medicare-assistant/nginx.conf`             | SPA routing + API reverse proxy      |
| `.env.example`                                | Template for secrets                 |
| `deploy.ps1` / `deploy.sh`                   | One-command deploy scripts           |

## Environment Variables (via .env)
| Variable              | Description                       |
|-----------------------|-----------------------------------|
| `MYSQL_CONNECTION`    | MySQL connection string            |
| `MONGO_CONNECTION`    | MongoDB connection string          |
| `JWT_SECRET`          | JWT signing key (32+ chars)        |
| `AI_PROVIDER`         | `OpenAI` or `Anthropic`            |
| `OPENAI_API_KEY`      | OpenAI API key                     |
| `ANTHROPIC_API_KEY`   | Anthropic API key                  |
| `GOOGLE_PLACES_API_KEY` | Google Places API key            |
| `FP_BASE_URL`         | Financial Planner API base URL     |
| `FP_AUTH_TOKEN`       | Financial Planner auth token       |
