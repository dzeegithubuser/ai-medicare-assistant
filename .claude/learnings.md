# Claude Code Learnings Log — AI Medicare Assistant

Persistent record of corrections and patterns learned from developer feedback for the
**AI Medicare Assistant** workspace (.NET 10 API at `api-ai-medicare-assistant/` + Angular 21 UI at
`ui-ai-medicare-assistant/`). Claude reads this file every session to avoid repeating past
mistakes.

> **Format**: dated, categorized lesson with a brief description.
> **Do not delete entries** — they are institutional memory.
> **Source-of-truth**: this file is the primary learnings log for both API and UI work.

For user-scoped preferences and project facts that don't fit here, also check the file-based
auto-memory at `~/.claude/projects/<workspace>/memory/MEMORY.md` (loaded automatically).

---

<!-- LEARNINGS START — append new entries below this line -->

## 2026-05-14 · Architecture · Application services must use interfaces

**Rule**: Every service in `AI.MedicareAssistant.Application/Services/` must have a matching
`IFooService` interface. Inject the interface, never the concrete type.

**Why**: Enforced by code review and the saved auto-memory
`feedback_application_services_use_interfaces`. Concrete-type injection makes mocking in xUnit tests
fragile and leaks implementation back into the controller layer.

**Where the interface lives**:
- Interface uses Application DTOs → `AI.MedicareAssistant.Application/Interfaces/`
- Interface uses only Domain types → `AI.MedicareAssistant.Domain/Interfaces/`

**Note**: `ProfileService`, `PrescriptionService`, `RecommendationService`, `ChatSessionService`,
`CostProjectionService` are **legacy violations** (registered as concrete in
`ApplicationServicesExtensions`). They predate this rule and are tracked as follow-up work — don't
add new violations.

---

## 2026-05-13 · MongoDB · Unique indexes on per-user fields must be Sparse

**Rule**: Any unique index on a per-user / optional field must be created with
`new CreateIndexOptions { Unique = true, Sparse = true }`.

**Why**: A non-sparse unique index treats missing/null fields as null, so the second-ever doc with
a missing field collides on the first. The `chatSessions.userId_1` incident on 2026-05-13 tripped
this when a legacy PascalCase-keyed index left every doc with `null` in the indexed slot.

**Defense**: `MongoIndexInitializer.DropOptionDriftedIndexesAsync` re-creates the chatSessions
index as sparse on every startup. New unique indexes should be sparse by default.

---

## 2026-05-13 · MongoDB · Always register `CamelCaseElementNameConvention` before any class map

**Rule**: The convention pack registration in `DatabaseExtensions.AddDatabaseServices` must run
before any code touches a document. Class maps are baked on first use.

**Why**: Indexes created via `Builders<T>.IndexKeys.Ascending(d => d.UserId)` compile to the
serialized field name. If the convention isn't applied at index-creation time, the index keys on
`UserId` (PascalCase) — but documents have `userId` (camelCase), so every doc indexes as `null`
and the unique constraint fires on the second insert.

**Defense**: `MongoIndexInitializer.DropLegacyPascalCaseIndexesAsync` drops any index whose name
starts with an uppercase letter, on every startup. Safe + idempotent.

---

## 2026-05-12 · Clean Architecture · Domain layer cannot reference Application or Infrastructure

**Rule**: `AI.MedicareAssistant.Domain` has no project references. Only `MongoDB.Bson` is allowed
as a NuGet for the `[BsonId]` etc. attributes.

**Why**: Inverting this couples persistence/service concerns into the model layer and breaks
testability. The dependency graph must stay one-way: Api → Infrastructure → Application → Domain.

---

## 2026-05-10 · AI · Never call OpenAI / Anthropic / Gemini SDKs from Application or Domain

**Rule**: All LLM calls go through `IChatClient` (Microsoft.Extensions.AI). Provider selection
happens in `Api/Extensions/AiExtensions.AddAiProvider()`.

**Why**: Lets us swap providers via the `AiProvider` config value without changing service code.
Also keeps the Application layer agnostic of provider response shapes.

---

## 2026-05-09 · Frontend · Standalone components + signals + inject() are mandatory in new code

**Rule**:
- `standalone: true` + `changeDetection: ChangeDetectionStrategy.OnPush` on every `@Component`.
- `inject()` for DI, never constructor parameters.
- `input()` / `output()` signal functions, never `@Input()` / `@Output()` decorators.
- No `@NgModule`.

**Why**: Angular 21 defaults. Older patterns are tolerated in untouched legacy files but every new
file and every refactor must conform.

---

## 2026-05-09 · Frontend · JWT in sessionStorage, not localStorage

**Rule**: Tokens go in `sessionStorage` with a timestamp; the auth service enforces a 1-hour
expiry. `auth.interceptor.ts` attaches `Authorization: Bearer` automatically — never set it by
hand in a service.

**Why**: Closing the browser tab signs the user out, which matches the impersonation safety model.
`localStorage` survives across sessions, which is wrong for a clinical tool that an FP uses on
shared workstations.

---

<!-- Append new entries above this line -->
