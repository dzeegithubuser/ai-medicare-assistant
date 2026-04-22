# AI Medicare Assistant — Enterprise Blueprint

> A living technical reference organized as a book. Each chapter covers a distinct architectural concern — read sequentially for a full system overview, or jump to any chapter independently.

---

## Preface

This application is a ChatGPT-style Medicare healthcare assistant where users paste prescription lists and receive structured drug metadata with clinical intelligence. It features a professional split-panel UI with a two-step AI-powered drug search engine (drug name verification followed by full analysis), drug-drug interaction detection, dosage validation, therapeutic alternative suggestions, RxNorm-verified normalization, on-demand nearby pharmacy search with AI-generated per-drug pricing comparison, on-demand Medicare plan recommendations (MA, Part D, Medigap via Financial Planner API), a Long Term Care (LTC) cost projection wizard, an AI chatbot orchestrator with FSM-based multi-turn conversation state, JWT-authenticated user sessions, first-time user profile onboarding (with name, personal details, tax filing, health, and address), and zipcode-aware drug cost estimation.

The codebase follows **Clean Architecture** on the backend (.NET 10) and **signal-based component architecture** on the frontend (Angular 21). Every layer has a single responsibility, and every integration degrades gracefully.

---

## Table of Contents

| #  | Chapter | Description | File |
|----|---------|-------------|------|
| 1  | [Overview](ch01-overview.md) | Purpose, tech stack, and high-level architecture diagram | `ch01-overview.md` |
| 2  | [Frontend Architecture](ch02-frontend-architecture.md) | Component tree, components, services, guards, models, styling, and UI flow | `ch02-frontend-architecture.md` |
| 3  | [Prompt Architecture](ch03-prompt-architecture.md) | File-based prompt system for AI interactions | `ch03-prompt-architecture.md` |
| 4  | [Backend Architecture](ch04-backend-architecture.md) | API, Domain, Application, and Infrastructure layers | `ch04-backend-architecture.md` |
| 5  | [Data & Authentication](ch05-data-and-auth.md) | MongoDB database schema and JWT authentication | `ch05-data-and-auth.md` |
| 6  | [API Contract](ch06-api-contract.md) | Endpoints, request/response schemas, and examples | `ch06-api-contract.md` |
| 7  | [Project Structure](ch07-project-structure.md) | Full directory tree for frontend and backend | `ch07-project-structure.md` |
| 8  | [Feature Catalog](ch08-feature-catalog.md) | Story of each implemented enhancement | `ch08-feature-catalog.md` |
| 9  | [Roadmap](ch09-roadmap.md) | Future enhancements and planned capabilities | `ch09-roadmap.md` |
| 10 | [Testing Scenarios](ch10-testing-scenarios.md) | Manual test matrix covering all features | `ch10-testing-scenarios.md` |
| 11 | [Drug Endpoint Test Scenarios](drug-endpoint-test-scenarios.md) | Quick QA checklist for `POST /api/drug/suggest-names` with real-world validation references | `drug-endpoint-test-scenarios.md` |


