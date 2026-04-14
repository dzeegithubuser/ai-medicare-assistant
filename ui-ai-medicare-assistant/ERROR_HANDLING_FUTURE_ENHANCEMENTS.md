# Error Handling Future Enhancements

This note captures current error-handling gaps found during review and recommended improvements for future iterations.

## Why this matters

- Several non-blocking network/storage failures are currently swallowed.
- At scale, silent failures make state drift and production diagnosis harder.
- We should keep UX smooth while improving observability and reliability.

## Current gaps

### 1) Chat message sync failures are silently ignored (High)

- `src/app/services/drug-state.service.ts`
  - `syncMessagesToServer()` calls `updateMessages(...)` with `error: () => {}`
- Impact:
  - UI continues, but server chat history can fall behind unnoticed.
  - Session restore may show stale or incomplete messages.

### 2) UI state sync (`editMode`) silently ignored (Medium)

- `src/app/services/profile.service.ts`
  - `updateUiState(...)` uses `error: () => {}`
- Impact:
  - Refresh/navigation can restore inconsistent edit/view mode.

### 3) Dashboard chat-session hydration hides failures (Medium)

- `src/app/dashboard/dashboard.component.ts`
  - `getSession()` error branch is empty.
- Impact:
  - No signal when session restore fails, difficult troubleshooting.

### 4) Nested profile refresh calls suppress errors (Low)

- `src/app/analysis/analysis-shell.component.ts`
  - `loadProfile().subscribe({ error: () => {} })` in save-and-navigate paths.
- Impact:
  - Main flow works, but profile refresh failures are invisible.

### 5) Storage errors are swallowed without telemetry (Medium)

- `src/app/services/drug-state.service.ts`
  - `persist()`, `restore()`, `hydrateFpConfirmedFromSessionStorage()`
- `src/app/analysis/fp-drugs-step/fp-drugs-step.component.ts`
  - local persistence/restore catch blocks
- Impact:
  - Quota/corruption issues are hard to detect in production.

## Recommended enhancements

## Phase 1 (quick wins)

- Add a shared non-blocking error reporter utility:
  - Example: `reportNonBlockingError(context, err)`
  - Minimal behavior: `console.warn` in dev, optional telemetry hook in prod.
- Replace empty handlers `error: () => {}` with context-aware reporting.
- Keep user flow non-blocking (no hard stops for background sync failures).

## Phase 2 (resilience)

- Add retry/backoff for chat/session sync endpoints:
  - message sync
  - ui-state sync
- Add in-flight guard / queue to avoid overlapping sync calls.
- Add a `syncPending`/`syncFailed` state for lightweight UI indicator.

## Phase 3 (scalability + observability)

- Move from full-history message patch to append/delta endpoint on backend.
- Track metrics:
  - sync request count
  - payload size
  - failure rate by endpoint
  - storage quota/corruption incidents

## Suggested acceptance criteria

- No critical path uses empty error handlers for network calls.
- Background sync failures are visible in logs/telemetry.
- Chat/session sync retries transient failures automatically.
- User navigation/edit flows remain non-blocking when sync fails.

## Candidate tasks

- [ ] Create `ErrorHandlingService` (non-blocking reporter + optional telemetry)
- [ ] Replace silent handlers in:
  - [ ] `drug-state.service.ts`
  - [ ] `profile.service.ts`
  - [ ] `dashboard.component.ts`
  - [ ] `analysis-shell.component.ts`
- [ ] Add retry/backoff wrapper for `ChatSessionService` writes
- [ ] Add sync status signal and optional subtle UI status indicator
- [ ] Define backend contract for delta message sync
