# Chapter 2 — Frontend Architecture

> Component tree, components, services, interceptors, guards, models, configuration, styling, and UI flow.

This chapter has been split into focused sub-files for easier navigation. Each file covers a specific area of the frontend architecture.

---

## Sub-Files

| # | File | Description |
|---|------|-------------|
| 2.1 | [Component Tree](ch02-01-component-tree.md) | Visual hierarchy of all routed components |
| 2.2 | [Auth, Dashboard & Profile](ch02-02-components-auth-dashboard.md) | App root, authenticated shell, sign-in/up, password flows, profile |
| 2.3 | [Chat & Markdown](ch02-03-components-chat.md) | Right-panel chat, intent routing, guided wizard, drug analysis flow |
| 2.4 | [Medicare Analysis](ch02-04-components-medicare.md) | Wizard shell, drugs, pharmacy, plans, cost projections |
| 2.5 | [Saved Data, Compare & LTC](ch02-05-components-saved-ltc.md) | Saved list, detail, compare (Medicare/LTC/cross), LTC wizard |
| 2.6 | [Services](ch02-06-services.md) | HTTP clients, state management, SignalR, chat AI extraction, snapshots |
| 2.7 | [Guards & Models](ch02-07-guards-models.md) | Interceptor, route guards, all TypeScript interfaces |
| 2.8 | [Config, Styling & UI Flow](ch02-08-config-styling-flow.md) | App config, routes, environments, styling, 16-step user flow |

---

## Route Flow

```
/signin → /signup → /forgot-password → /reset-password → /verify-email
                          ↓
                    / (Dashboard — authGuard)
                          ↓
              /profile (UserProfileComponent)
              ├── /medicare-analysis (profileCompleteGuard)
              │     ├── /profile → /drugs → /pharmacies → /plans
              │     └── /cost-projections
              ├── /long-term-care (profileCompleteGuard)
              │     ├── /care-type → /projection
              │     └── /profile
              ├── /saved
              │     ├── /saved/compare
              │     └── /saved/:id
              └── /change-password
```

---

← [Chapter 1 — Overview](../ch01-overview/ch01-overview.md) | [Table of Contents](../APPLICATION_BLUEPRINT.md) | [Chapter 3 → Prompt Architecture](../ch03-prompt-architecture/ch03-prompt-architecture.md)