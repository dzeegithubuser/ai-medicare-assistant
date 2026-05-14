---
name: medicare-assistant
description: Domain knowledge and conventions for the AI Medicare Assistant repo. Trigger when working on drug analysis, Medicare plan recommendations, pharmacy pricing, LIS/Extra Help logic, the SignalR chat hub, or any code under api-ai-medicare-assistant/ or ui-ai-medicare-assistant/.
---

# Medicare Assistant — Working in this Repo

This is a two-project workspace: a .NET 10 Clean Architecture API and an Angular 21 UI. Use this skill to keep changes consistent with the existing architecture.

## Architecture quick reference

**Backend** — `api-ai-medicare-assistant/`
- `AI.MedicareAssistant.Api/` — Controllers, SignalR hubs (`/hubs/chat`), middleware, prompt templates
- `AI.MedicareAssistant.Application/` — Services and DTOs (orchestration layer)
- `AI.MedicareAssistant.Domain/` — Documents, interfaces, models, exceptions (no dependencies on other layers)
- `AI.MedicareAssistant.Infrastructure/` — MongoDB.Driver 3.4, OpenAI/Anthropic via `IChatClient`, pharmacy + Medicare integrations
- `AI.MedicareAssistant.Tests/` — xUnit-style unit tests

**Frontend** — `ui-ai-medicare-assistant/`
- Angular 21 standalone components, signal-based reactivity
- Angular Material 21 (M3 theming, 4 switchable themes)
- Tailwind CSS 4 via PostCSS plugin
- `@microsoft/signalr` WebSocket client
- TypeScript 5.9

## Conventions to follow

- **Clean Architecture direction**: Domain has no dependencies. Application depends on Domain. Infrastructure depends on Application + Domain. Api depends on all. Never invert.
- **Data access**: All persistence goes through MongoDB.Driver 3.4. There is no EF Core or SQL.
- **AI calls**: Go through `IChatClient`. Primary model is OpenAI GPT-4.1; secondary is Anthropic Claude Sonnet 4. Don't hardcode provider SDK calls in Application/Domain.
- **Auth**: JWT Bearer with BCrypt password hashing. SignalR hub auth uses query param (not header) — check existing hub code before changing auth flow.
- **Drug analysis is two-step**: (1) drug name verification with RxNorm normalization, (2) full clinical analysis (interactions, dosage, alternatives). Don't collapse into one call.
- **Frontend reactivity**: Prefer signals over RxJS for new component state. Use standalone components, not NgModules.
- **Styling**: Tailwind utility classes are the default; reach for Angular Material components for complex widgets.

## Common tasks — where to look

| Task | Start in |
|---|---|
| New AI prompt | `AI.MedicareAssistant.Api/Prompts/` |
| New chat intent | Chat hub + intent classifier in `Api/Hubs` and `Application/` |
| New API endpoint | `Api/Controllers/`, then a service in `Application/` |
| MongoDB schema change | `Domain/Documents/` first, then update repositories in `Infrastructure/` |
| New UI screen | `ui-ai-medicare-assistant/src/app/` — standalone component + route |
| Theme/styling tweak | Tailwind classes first; Material M3 tokens for theme-level changes |

## Run/test commands

```powershell
# API
dotnet run --project api-ai-medicare-assistant/AI.MedicareAssistant.Api
# Tests
dotnet test api-ai-medicare-assistant
# UI
npm --prefix ui-ai-medicare-assistant run start:dev
```

API runs on `http://localhost:5024`, UI on `http://localhost:4200`.

## Don't

- Don't add a second ORM or data-access library — MongoDB.Driver only.
- Don't bypass `IChatClient` to call OpenAI/Anthropic SDKs directly from business logic.
- Don't add NgModules to new frontend code; standalone components only.
- Don't store secrets in `appsettings.json` — use `appsettings.Development.json` (git-ignored) or .NET User Secrets.

## Reference docs

Architecture chapters live in [docs/](../../../docs/):
- [ch01-overview.md](../../../docs/ch01-overview.md)
- [ch03-prompt-architecture.md](../../../docs/ch03-prompt-architecture.md)
- [ch04-backend-architecture.md](../../../docs/ch04-backend-architecture.md)
- [ch06-api-contract.md](../../../docs/ch06-api-contract.md)
