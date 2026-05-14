---
name: ui-standards
description: Coding standards for the Angular 21 frontend at ui-ai-medicare-assistant/. Trigger when editing or creating any .ts/.html/.scss file under that folder, scaffolding components/services/guards, or reviewing UI PRs. Covers signals, OnPush, inject(), Tailwind, Material, SignalR, routing, testing.
---

# UI Standards — `ui-ai-medicare-assistant/`

Angular 21 + TypeScript 5.9 + Tailwind CSS 4 + Angular Material 21. **Match these conventions exactly** — they are enforced by code review and `tools/check-max-lines.mjs`.

## Project structure (type-based, not feature-folders)

```
src/app/
├── auth/            # Auth feature components
├── chat/            # Chat feature components
├── medicare-analysis/
├── plan-recommendation/
├── services/        # ALL services live here (centralized)
├── guards/          # Functional CanActivateFn guards
├── interceptors/    # HTTP interceptors
├── models/          # TypeScript interfaces/types
├── pipes/
├── data/            # Static data
└── utils/
```

Services are **centralized** in `services/`, never co-located with the component that uses them.

## Components

- **Standalone only.** Never create `NgModule`. Standalone is default in Angular 21; the schematic in `angular.json` enforces it.
- **`ChangeDetectionStrategy.OnPush` always.** Every `@Component` decorator includes `changeDetection: ChangeDetectionStrategy.OnPush`. See [chat.component.ts:44](ui-ai-medicare-assistant/src/app/chat/chat.component.ts#L44).
- **Signals over RxJS for component state.** Use `signal()`, `computed()`, `effect()`. Reach for RxJS only for HTTP streams or external event sources.
  - Example: [chat.component.ts:81-94](ui-ai-medicare-assistant/src/app/chat/chat.component.ts#L81-L94) (`computed()` for derived UI state)
  - Example: [drug-selection-panel.component.ts:36-62](ui-ai-medicare-assistant/src/app/medicare-analysis/drug-selection-panel.component.ts#L36-L62)
- **Modern input/output.** Use `input()` and `output()` functions, **not** `@Input()`/`@Output()` decorators. See [drug-selection-panel.component.ts:22-34](ui-ai-medicare-assistant/src/app/medicare-analysis/drug-selection-panel.component.ts#L22-L34).
- **`inject()` only.** Never use constructor injection in new code. See [auth.service.ts:26](ui-ai-medicare-assistant/src/app/services/auth.service.ts#L26).
- **Separate template/style files.** Pattern: `foo.component.ts` + `foo.component.html` + `foo.component.scss`. Inline templates are reserved for trivial root components like `app.ts`.

## Styling

- **Tailwind first.** Use Tailwind utility classes in templates for ~all layout/spacing/color. Component `.scss` is for what Tailwind can't express.
- **Theme tokens via CSS custom properties + `data-theme` attribute.** Defined globally in `styles.scss` (e.g. `--color-cyan-*`, `--app-bg`, `--app-text`). Do not hardcode hex colors in components — use the tokens.
- **Angular Material 21 (M3).** Import only the modules you use (e.g. `MatIconModule`, `MatButtonModule`, `MatFormFieldModule`). Don't import the umbrella module. See [chat.component.ts:9-13](ui-ai-medicare-assistant/src/app/chat/chat.component.ts#L9-L13).

## Services

- **`@Injectable({ providedIn: 'root' })`** — every service is a root singleton. See [auth-signal-r.service.ts:36](ui-ai-medicare-assistant/src/app/services/auth-signal-r.service.ts#L36).
- **`inject()` inside the class body**, not constructor params.
- **HTTP** returns `Observable<T>`. Auth header injection is handled by [auth.interceptor.ts:1-16](ui-ai-medicare-assistant/src/app/interceptors/auth.interceptor.ts#L1-L16); errors by `http-error.interceptor.ts`. Don't manually attach JWTs in services.
- **JWT storage**: `sessionStorage` with a timestamp; 1-hour expiry enforced. See [auth.service.ts:18-21,72-85](ui-ai-medicare-assistant/src/app/services/auth.service.ts#L18-L21). Never store the token in `localStorage`.

## Routing

- **Functional guards** (`CanActivateFn`), not class-based. See [auth.guard.ts:5-14](ui-ai-medicare-assistant/src/app/guards/auth.guard.ts#L5-L14).
- **Route paths as constants** in `app-routes.const.ts` — no magic strings in components or templates.

## Real-time (SignalR)

- Wrap `@microsoft/signalr` in a dedicated service ([chat-signal-r.service.ts](ui-ai-medicare-assistant/src/app/services/chat-signal-r.service.ts)).
- Pattern: single persistent connection per session; `connect(token)` returns Observable; `session$` is a `ReplaySubject` for late subscribers; connection state exposed as a signal (`isConnected`).
- Auth: token passed as **query param** to match the API hub config — not as a header.

## TypeScript / imports

- **Strict mode is on**, with `noImplicitOverride`, `noPropertyAccessFromIndexSignature`, `noImplicitReturns`, `noFallthroughCasesInSwitch`, plus Angular `strictTemplates`. Don't disable these locally.
- **No path aliases.** All intra-app imports are relative (`../services/...`). Don't add `@app/*` paths to `tsconfig.json`.

## Linting / formatting

- **Prettier** with `printWidth: 100`, `singleQuote: true`, `arrowParens: 'avoid'`, angular parser for `.html`. Configured in `package.json`.
- No project ESLint config — Angular defaults apply.
- **File-size limit**: `tools/check-max-lines.mjs` is run via `npm run check:maintainability`. Keep files small; split large components.

## Testing

- **Vitest** (not Karma/Jasmine), with Jasmine-compatible syntax. Use `vi.fn()` for mocks. See [drug-state.service.spec.ts](ui-ai-medicare-assistant/src/app/services/drug-state.service.spec.ts).
- `TestBed.configureTestingModule()` for component/service setup.
- `npm test` runs the suite.

## Scripts

```powershell
npm --prefix ui-ai-medicare-assistant run start:dev    # ng serve, dev profile
npm --prefix ui-ai-medicare-assistant run build:prod   # production build
npm --prefix ui-ai-medicare-assistant test             # Vitest
npm --prefix ui-ai-medicare-assistant run check:maintainability
```

## Don't

- Don't create `NgModule`s.
- Don't use `@Input()`/`@Output()` decorators in new code — use `input()`/`output()`.
- Don't use constructor injection — use `inject()`.
- Don't omit `OnPush` change detection.
- Don't put JWTs in `localStorage`.
- Don't add path aliases to `tsconfig.json`.
- Don't co-locate services next to components — they go in `src/app/services/`.
- Don't import the umbrella `MaterialModule` — import only the specific Material modules used.
- Don't manually attach `Authorization` headers — that's the interceptor's job.
