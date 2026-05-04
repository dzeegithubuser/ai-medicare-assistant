# Medicare Assistant

A ChatGPT-style Medicare healthcare assistant that helps users understand their prescriptions, find affordable pharmacies, and choose the right Medicare plan. Built with **Angular 21** on the frontend and **.NET 10 Clean Architecture** on the backend.

---

## Features

- **AI-Powered Drug Analysis** — Two-step engine: drug name verification (RxNorm-normalized) followed by full clinical analysis (interactions, dosage validation, therapeutic alternatives)
- **Medicare Plan Recommendations** — Personalized Part D, Medicare Advantage, and Medigap plan rankings based on user profile and drug list
- **Pharmacy Search & Pricing** — Nearby pharmacy lookup with AI-generated per-drug pricing comparison
- **Cost Projections** — Lifetime Medicare cost dashboards and LIS/Extra Help tier determination
- **Conversational Chat** — Real-time AI chat via SignalR WebSocket with 17 classified intents and session persistence
- **User Profile Onboarding** — Name, demographics, income (MAGI tier), health conditions, address (ZIP-to-county resolution)
- **JWT Authentication** — Sign up, sign in, forgot/reset password; SignalR hub auth via query param

---

## Tech Stack

### Frontend (`ui-ai-medicare-assistant/`)
| | |
|---|---|
| Framework | Angular 21 (standalone components, signal-based reactivity) |
| UI Library | Angular Material 21 (M3 theming, 4 switchable themes with per-theme fonts) |
| Styling | Tailwind CSS 4 (PostCSS plugin) |
| Real-time | `@microsoft/signalr` WebSocket client |
| Language | TypeScript 5.9 |

### Backend (`api-ai-medicare-assistant/`)
| | |
|---|---|
| Framework | .NET 10 Web API — Clean Architecture (4 layers) |
| AI | OpenAI GPT-4.1 (primary) · Anthropic Claude Sonnet 4 (secondary) via `IChatClient` |
| Database | MongoDB.Driver 3.4 — all data (users, profiles, sessions, prescriptions, plans) |
| Real-time | ASP.NET Core SignalR (`/hubs/chat`) |
| Auth | JWT Bearer + BCrypt password hashing |
| Logging | Serilog (console + daily rolling file) |

---

## Project Structure

```
ai-medicare-assistant/
├── api-ai-medicare-assistant/          # .NET 10 backend solution
│   ├── AI.MedicareAssistant.Api/       # Controllers, Hubs, Middleware, Prompts
│   ├── AI.MedicareAssistant.Application/  # Services, DTOs
│   ├── AI.MedicareAssistant.Domain/    # Documents, Interfaces, Models, Exceptions
│   ├── AI.MedicareAssistant.Infrastructure/  # MongoDB, AI, Pharmacy, Medicare
│   └── AI.MedicareAssistant.Tests/     # Unit tests
├── ui-ai-medicare-assistant/           # Angular 21 frontend
│   └── src/app/                    # Components, services, guards, models
├── docs/                           # Architecture documentation (10 chapters)
└── .vscode/                        # launch.json + tasks.json for debugging
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/) and npm 10+
- MongoDB instance
- OpenAI or Anthropic API key

### 1. Configure the API

Copy `appsettings.json` and fill in your values:

```json
{
  "ConnectionStrings": {
    "MongoDb": "mongodb://USER:PASSWORD@HOST:27017"
  },
  "MongoDb": {
    "DatabaseName": "ai_medicare_assistant"
  },
  "Jwt": {
    "Secret": "YOUR_JWT_SECRET_AT_LEAST_32_CHARACTERS_LONG"
  },
  "OpenAI": { "ApiKey": "YOUR_OPENAI_API_KEY" },
  "Anthropic": { "ApiKey": "YOUR_ANTHROPIC_API_KEY" }
}
```

> Store secrets locally in `appsettings.Development.json` (git-ignored) or via [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets).

### 2. Run the API

```bash
cd api-ai-medicare-assistant
dotnet run --project AI.MedicareAssistant.Api
# API available at http://localhost:5024
```

### 3. Run the UI

```bash
cd ui-ai-medicare-assistant
npm install
npm run start:dev
# App available at http://localhost:4200
```

---

## Debugging in VS Code

The `.vscode/launch.json` provides four configurations:

| Configuration | Description |
|---|---|
| `API: Launch (Debug)` | Builds and launches the .NET API with the CLR debugger |
| `API: Attach to Process` | Attach to an already-running API process |
| `UI: Serve & Debug (Chrome)` | Starts `ng serve` and opens Chrome with Angular source maps |
| `UI: Serve & Debug (Edge)` | Same, using Edge |
| `Full Stack: API + UI (Chrome/Edge)` | **Compound** — launches both simultaneously |

---

## Running Tests

```bash
cd api-ai-medicare-assistant
dotnet test
```
powershell -ExecutionPolicy Bypass -File .\zip-workspace.ps1

---

## Documentation

Full architecture documentation is in the [`docs/`](docs/) folder:

| Chapter | Topic |
|---|---|
| [ch01](docs/ch01-overview.md) | Overview & Architecture |
| [ch02](docs/ch02-frontend-architecture.md) | Frontend Architecture |
| [ch03](docs/ch03-prompt-architecture.md) | Prompt Architecture |
| [ch04](docs/ch04-backend-architecture.md) | Backend Architecture |
| [ch05](docs/ch05-data-and-auth.md) | Data & Authentication |
| [ch06](docs/ch06-api-contract.md) | API Contract |
| [ch07](docs/ch07-project-structure.md) | Project Structure |
| [ch08](docs/ch08-feature-catalog.md) | Feature Catalog |
| [ch09](docs/ch09-roadmap.md) | Roadmap |
| [ch10](docs/ch10-testing-scenarios.md) | Testing Scenarios |
