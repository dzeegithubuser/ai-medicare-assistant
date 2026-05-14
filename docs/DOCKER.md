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

### Database
| Variable              | Description                              |
|-----------------------|------------------------------------------|
| `MONGO_CONNECTION`    | MongoDB connection string                |
| `MONGO_DB_NAME`       | MongoDB database name (default: `ai_medicare_assistant`) |

### Authentication
| Variable              | Description                              |
|-----------------------|------------------------------------------|
| `JWT_SECRET`          | JWT signing key (32+ chars)              |

### AI Providers
| Variable              | Description                              |
|-----------------------|------------------------------------------|
| `AI_PROVIDER`         | `OpenAI`, `Anthropic`, or `Gemini`       |
| `OPENAI_API_KEY`      | OpenAI API key                           |
| `OPENAI_MODEL`        | OpenAI model (e.g. `gpt-4.1`)           |
| `ANTHROPIC_API_KEY`   | Anthropic API key                        |
| `ANTHROPIC_MODEL`     | Anthropic model (e.g. `claude-sonnet-4-20250514`) |
| `ANTHROPIC_BASE_URL`  | Anthropic API base URL                   |
| `ANTHROPIC_MAX_TOKENS`| Anthropic max tokens                     |
| `GEMINI_API_KEY`      | Google Gemini API key                    |
| `GEMINI_MODEL`        | Gemini model (e.g. `gemini-2.0-flash`)   |
| `GEMINI_BASE_URL`     | Gemini API base URL                      |
| `GEMINI_MAX_OUTPUT_TOKENS` | Gemini max output tokens            |

### Financial Planner
| Variable              | Description                              |
|-----------------------|------------------------------------------|
| `FP_BASE_URL`         | Financial Planner API base URL           |
| `FP_AUTH_TOKEN`       | Financial Planner auth token (Basic)     |

### Email / SMTP
| Variable                 | Description                           |
|--------------------------|---------------------------------------|
| `EMAIL_HOST`             | SMTP server host                      |
| `EMAIL_PORT`             | SMTP server port (e.g. `587`)         |
| `EMAIL_ENABLE_SSL`       | Enable SSL (`true`/`false`)           |
| `EMAIL_FROM_ADDRESS`     | Sender email address                  |
| `EMAIL_FROM_DISPLAY_NAME`| Sender display name                   |
| `EMAIL_PASSWORD`         | SMTP password                         |
| `EMAIL_TIMEOUT_MS`       | SMTP timeout in ms (e.g. `20000`)     |
| `EMAIL_FRONTEND_BASE_URL`| Frontend base URL for email links     |

### CORS
| Variable                 | Description                           |
|--------------------------|---------------------------------------|
| `CORS_ALLOWED_ORIGINS_0` | First allowed origin (e.g. `http://localhost:4200`) |
| `CORS_ALLOWED_ORIGINS_1` | Second allowed origin (e.g. `http://localhost:9600`) |

### CMS
| Variable                          | Description                    |
|-----------------------------------|--------------------------------|
| `CMS_MEDICARE_PART_D_SPENDING_URL`| CMS Part D spending data URL   |

### Admin Seed
| Variable         | Description                                                   |
|------------------|---------------------------------------------------------------|
| `ADMIN_EMAIL`    | Email for the seeded admin user (default: `admin@aivante.com`) |
| `ADMIN_PHONE`    | Phone for the seeded admin user (default: `5550199999`)        |
| `ADMIN_PASSWORD` | Gates the seed. Leave blank to skip (production-safe). Set to a strong password on first deploy against a fresh DB. |

These map to `Seed__AdminEmail` / `Seed__AdminPhone` / `Seed__AdminPassword` on the `api` container via `docker-compose.yml`. Once the admin is seeded successfully you can blank `ADMIN_PASSWORD` — subsequent restarts log `Admin user … already exists; skipping seed.` See [ADMIN_SETUP.md](ADMIN_SETUP.md).
