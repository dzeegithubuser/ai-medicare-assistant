# Application Audit Findings

> Generated: April 12, 2026
> Status: Tracking implementation progress

---

## CRITICAL (Fix Immediately)

- [ ] **1. Secrets in source control** — `appsettings.json` and `appsettings.Development.json` contain plaintext OpenAI key, Anthropic key, MongoDB credentials, JWT secret, Financial Planner auth token. `.gitignore` does not exclude them.
  - Files: `api-ai-medicare-assistant/AI.MedicareAssistant.Api/appsettings.json`, `appsettings.Development.json`
  - Fix: Move to User Secrets / env vars / Key Vault. Add to `.gitignore`. Rotate all keys.

- [ ] **2. IDOR — PrescriptionController.GetById** — Fetches any user's prescription by document ID without verifying ownership. Any authenticated user can read another user's drug data.
  - File: `AI.MedicareAssistant.Api/Controllers/PrescriptionController.cs`
  - Fix: Add `userId` filter to `GetByIdAsync` in service and repository layers.

- [ ] **3. XSS via bypassSecurityTrustHtml** — AI-generated markdown rendered with `marked` is passed through `bypassSecurityTrustHtml`, completely disabling Angular's XSS sanitizer.
  - File: `ui-ai-medicare-assistant/src/app/pipes/markdown.pipe.ts`
  - Fix: Add DOMPurify sanitization before bypassing Angular's sanitizer.

---

## HIGH

- [ ] **4. Weak JWT secret** — `"YourSuperSecretKeyAtLeast32CharactersLong!@#2026"` is a guessable placeholder. Allows token forgery.
  - File: `appsettings.json` → `Jwt:Secret`
  - Fix: Use a cryptographically random 256-bit+ key from a secret store.

- [x] **5. ~~MigrationController is [AllowAnonymous]~~** — **RESOLVED:** `MigrationController` has been deleted. MySQL and EF Core have been fully removed; all data is now in MongoDB.
  - ~~File: `AI.MedicareAssistant.Api/Controllers/MigrationController.cs`~~
  - ~~Fix: Add `[Authorize]` with admin role/policy, or remove from non-dev environments.~~

- [ ] **6. Reset token returned in HTTP response** — Forgot-password returns the reset token in the response body instead of sending via email.
  - File: `AI.MedicareAssistant.Application/Services/AuthService.cs`
  - Fix: Send token via email only. Never return in API response.

- [x] **7. ~~AiAnalysisStep — no try/catch on AI JSON deserialization~~** — RESOLVED: Pipeline directory and files deleted. Analysis pipeline no longer exists.
  - File: `AI.MedicareAssistant.Application/Services/Pipeline/AiAnalysisStep.cs` (deleted)
  - Fix: Wrap deserialization in try/catch, return `false` on failure with error message.

- [x] **8. ~~CmsRxNormEnrichmentStep — one drug failure kills ALL enrichments~~** — RESOLVED: Pipeline directory and files deleted. Enrichment pipeline no longer exists.
  - File: `AI.MedicareAssistant.Application/Services/Pipeline/CmsRxNormEnrichmentStep.cs` (deleted)
  - Fix: Per-drug try/catch so other drugs are still enriched.

---

## MEDIUM

- [ ] **9. No rate limiting on auth endpoints** — Brute-force/credential stuffing attacks possible on signup/signin/forgot-password.
  - File: `AI.MedicareAssistant.Api/Controllers/AuthController.cs`, `Program.cs`
  - Fix: Add `builder.Services.AddRateLimiter(...)` or `AspNetCoreRateLimit`.

- [ ] **10. Reset token reuses login JWT audience** — Reset token can authenticate as the user for 30 minutes via the standard bearer middleware.
  - File: `AI.MedicareAssistant.Application/Services/AuthService.cs`
  - Fix: Use a distinct audience for reset tokens (e.g. `"AI.MedicareAssistant.PasswordReset"`).

- [ ] **11. Auth interceptor attaches JWT to ALL requests** — Token sent to any third-party URL.
  - File: `ui-ai-medicare-assistant/src/app/interceptors/auth.interceptor.ts`
  - Fix: Check that request URL starts with `environment.apiUrl` before attaching token.

- [ ] **12. Sliding token timestamp defeats server-side expiry** — `getToken()` resets the 1-hour client timer on every call, but actual JWT `exp` is fixed.
  - File: `ui-ai-medicare-assistant/src/app/services/auth.service.ts`
  - Fix: Use JWT `exp` claim as source of truth, or implement actual token refresh.

- [ ] **13. No input length validation on AI endpoints** — Attackers can send megabytes to burn token quotas.
  - Files: `DrugController.cs`, `ChatIntentController.cs` (DTOs)
  - Fix: Add `[MaxLength]` to all AI-bound DTO string properties.

- [ ] **14. ChatSessionService accepts arbitrary Role values** — Users can inject `"system"` role messages into chat history.
  - File: `AI.MedicareAssistant.Application/Services/ChatSessionService.cs`
  - Fix: Validate `Role` against an allowed list (`"user"`, `"assistant"`).

- [ ] **15. DrugAiService — no error handling around AI calls** — No retry, no null-check on `response.Text`.
  - File: `AI.MedicareAssistant.Infrastructure/AI/DrugAiService.cs`
  - Fix: Wrap in try/catch with retry (Polly), null-check response.

- [ ] **16. AnthropicMeaiChatClient leaks raw API error text** — Exception messages contain full API response (information disclosure).
  - File: `AI.MedicareAssistant.Infrastructure/Anthropic/AnthropicMeaiChatClient.cs`
  - Fix: Throw domain-specific exception with sanitized message.

- [ ] **17. SignalR errors silently swallowed** — `SyncMessages` failures are `.catch(() => {})`.
  - File: `ui-ai-medicare-assistant/src/app/services/chat-signal-r.service.ts`
  - Fix: Log errors, show user feedback on persistent failures.

- [ ] **18. GetUserId() across all controllers — unhandled exceptions** — `Guid.Parse` with `!` null-forgiving on potentially missing claim.
  - Files: All controllers
  - Fix: Use `Guid.TryParse`, return 400 on failure. Create a shared base controller method.

- [ ] **19. CancellationToken not forwarded to AI services** — Disconnected users still pay for AI tokens.
  - File: `AI.MedicareAssistant.Infrastructure/AI/DrugAiService.cs`
  - Fix: Add `CancellationToken ct = default` parameter, forward to `GetResponseAsync`.

- [ ] **20. Memory leak — unsubscribed router.events in ChatComponent** — No `takeUntilDestroyed` on `router.events.subscribe()`.
  - File: `ui-ai-medicare-assistant/src/app/chat/chat.component.ts`
  - Fix: Add `takeUntilDestroyed(this.destroyRef)`.

- [x] **21. ~~Generic Repository.UpdateAsync doesn't call Update()~~** — **RESOLVED:** `Repository<T>` (generic EF Core base) has been deleted. All repositories are now MongoDB-based.
  - ~~File: `AI.MedicareAssistant.Infrastructure/Repositories/Repository.cs`~~
  - ~~Fix: Add `DbSet.Update(entity)` before `SaveChangesAsync`.~~

- [ ] **22. profileCompleteGuard treats API errors as "profile incomplete"** — Network failures redirect user to profile page.
  - File: `ui-ai-medicare-assistant/src/app/guards/profile-complete.guard.ts`
  - Fix: Differentiate API errors from incomplete profile state.

---

## LOW

- [x] **23. Dead code: BuildMedicareRequestAsync** — Private method never called.
  - File: `AI.MedicareAssistant.Application/Services/CostProjectionService.cs`
  - Fix: Delete it.

- [x] **24. AddMemoryCache() called twice** — Duplicate registration in Program.cs.
  - File: `AI.MedicareAssistant.Api/Program.cs`
  - Fix: Remove the duplicate.

- [x] **25. Hardcoded "OpenAI" in log messages** — Misleading when Anthropic is active.
  - File: `AI.MedicareAssistant.Infrastructure/AI/DrugAiService.cs`
  - Fix: Change to "AI provider" or inject provider name.

- [x] **26. PasswordHash cached in IMemoryCache** — Full `User` entity (including hash) held in memory cache.
  - File: `AI.MedicareAssistant.Application/Services/AuthService.cs`
  - Fix: ~~Cache a projection without the hash.~~ Removed caching entirely — auth operations always query DB for fresh data, preventing password hashes from lingering in memory.

- [x] **27. Faux-streaming in AnthropicMeaiChatClient** — `GetStreamingResponseAsync` fetches full response, yields once.
  - File: `AI.MedicareAssistant.Infrastructure/Anthropic/AnthropicMeaiChatClient.cs`
  - Fix: Implement SSE-based streaming or document as unsupported. *(Documented as unsupported with XML doc comment.)*

- [x] **28. authGuard uses imperative navigate** — Should return `UrlTree` instead of `router.navigate()`.
  - File: `ui-ai-medicare-assistant/src/app/guards/auth.guard.ts`
  - Fix: Return `router.parseUrl('/signin')`.

- [x] **29. DeleteByUserIdAsync uses DeleteOne instead of DeleteMany** — Orphaned documents if multiple active recommendations exist.
  - File: `AI.MedicareAssistant.Infrastructure/Repositories/RecommendationRepository.cs`
  - Fix: Use `DeleteManyAsync`.

- [x] **30. Non-atomic upserts in MongoDB repositories** — Delete-then-insert instead of `ReplaceOneAsync` with `IsUpsert`.
  - File: `AI.MedicareAssistant.Infrastructure/Repositories/MongoRepositories.cs`
  - Fix: Use `ReplaceOneAsync` with `IsUpsert = true`. *(Fixed in PrescriptionDocRepository, UserAnalysisSelectionsRepository, and LtcSelectionsRepository.)*

- [x] **31. MongoDbContext.EnsureIndexes() runs synchronously** — Blocking call in singleton constructor.
  - File: `AI.MedicareAssistant.Infrastructure/Data/MongoDbContext.cs`
  - Fix: Move to async startup initialization via `IHostedService`. *(Extracted to MongoIndexInitializer hosted service.)*

---

## PRODUCTION READINESS GAPS

- [ ] **32. No Dockerfile / docker-compose** — No container build support.
- [ ] **33. No health check endpoints** — No `/health`, no MongoDB liveness probes.
- [ ] **34. No HTTPS redirection** — `app.UseHttpsRedirection()` absent.
- [ ] **35. CORS hardcoded** — Origins not configurable per environment.
- [ ] **36. Production environment.ts identical to dev** — Points to `localhost:5024`.
- [ ] **37. Structured log sink missing** — Serilog writes to local files only (no Seq/AppInsights).

---

## TEST COVERAGE GAPS

- [ ] **38. Backend: 0/19 controllers tested** — No integration tests with `WebApplicationFactory`.
- [x] **39. Backend: ~13 application services untested** — AuthService, ProfileService, all Extract services, Pipeline steps, etc. *(Added 65+ unit tests: AuthService, ProfileService, DrugService, ChatSessionService, PrescriptionService — all passing)*
- [x] **40. Frontend: effectively zero test coverage** — Only 1 spec file exists. *(Added 34 Vitest tests: AuthService 12 tests, DrugStateService 22 tests — plus fixed existing 5 chat-send-guards matchers — 39 total passing)*
- [ ] **41. No E2E tests** — No Cypress/Playwright.
