---
description: Medicare Assistant repo helper — run, test, or scaffold features across the API and UI.
argument-hint: <run-api | run-ui | run-all | test-api | new-endpoint <name> | new-component <name> | new-prompt <name>>
---

You are working in the AI Medicare Assistant repo (.NET 10 API + Angular 21 UI). Load the `medicare-assistant` skill for domain conventions before acting.

The user invoked: `/medicare $ARGUMENTS`

Interpret `$ARGUMENTS` as one of the subcommands below and execute it. If `$ARGUMENTS` is empty or unrecognized, list the subcommands and ask which one they want.

## Subcommands

### `run-api`
Start the .NET API in the background:
```powershell
dotnet run --project api-ai-medicare-assistant/AI.MedicareAssistant.Api
```
Report the listening URL (default `http://localhost:5024`).

### `run-ui`
Start the Angular UI dev server in the background:
```powershell
npm --prefix ui-ai-medicare-assistant run start:dev
```
Report the listening URL (default `http://localhost:4200`).

### `run-all`
Start both API and UI in the background (two separate background processes). Confirm both are up before returning.

### `test-api`
Run the .NET test suite:
```powershell
dotnet test api-ai-medicare-assistant
```
Summarize pass/fail counts.

### `new-endpoint <Name>`
Scaffold a new API endpoint following Clean Architecture:
1. Add a controller method in `api-ai-medicare-assistant/AI.MedicareAssistant.Api/Controllers/`
2. Add a service method in `AI.MedicareAssistant.Application/Services/`
3. If it touches data, add/extend a repository in `AI.MedicareAssistant.Infrastructure/` and document/interface in `Domain/`
4. Wire DI registrations
Show the user the planned files before writing.

### `new-component <name>`
Scaffold a new Angular standalone component under `ui-ai-medicare-assistant/src/app/`. Use signals for state, Tailwind for styling, Angular Material for complex widgets. Do **not** create an NgModule.

### `new-prompt <Name>`
Add a new prompt template under `api-ai-medicare-assistant/AI.MedicareAssistant.Api/Prompts/`. Wire it to the appropriate service that calls `IChatClient`. For drug-related prompts, remember the two-step pattern: verification (RxNorm-normalized) → full clinical analysis.

## Rules

- Never hardcode the OpenAI/Anthropic SDKs in business logic — always go through `IChatClient`.
- Never add a second ORM — MongoDB.Driver only.
- Never invert the Clean Architecture dependency direction (Domain depends on nothing).
- Frontend: standalone components only, signals preferred over RxJS for new state.
