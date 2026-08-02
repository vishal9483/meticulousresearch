# Phase — Settings & Secure API Key

**SPEC:** §3.5, §7.5. **Milestone:** M0. **Depends on:** data-store-migrations

## Goal
Provide secure API-key storage (never plaintext) and app-level settings with persistence and a
Settings screen. Owns the **secure key store** and **settings service** contracts.

## Deliverables
1. **`ISecureKeyStore`** backed by Windows Credential Manager / DPAPI.
   - `Save(key)`, `Get()`, `Clear()`, `HasKey`. Value never written to SQLite or any file.
   - Suggested impl: DPAPI (`ProtectedData`) writing an encrypted blob, or Credential Manager via
     `CredWrite`/`CredRead` P/Invoke or a maintained wrapper. Behind the interface for testability
     (a `FakeSecureKeyStore` in tests holds it in memory).
2. **`IApiCredentialProvider`** (or equivalent resolver) — the single place the rest of the app
   asks for the **effective API key** and **effective base URL**, applying the resolution order
   from §7.5. This is what `ai-gateway` consumes; nothing else reads the env var or the store
   directly.
   - **Key:** `ANTHROPIC_API_KEY` env var (if set & non-empty) **wins** → `ISecureKeyStore.Get()`
     → none. Reads the env var each time (via an injected `IEnvironment` so tests can set/clear
     it deterministically — never `Environment.GetEnvironmentVariable` inline).
   - **Base URL:** `ANTHROPIC_BASE_URL` env var (if set & non-empty) **wins** → persisted
     base-URL setting → default public Anthropic API constant. Trailing slash normalized.
   - Neither the env-supplied key nor URL is ever written back to SQLite, settings files, or a
     command line.
3. **`ISettingsService`** over the `Setting` table (from data-store-migrations) for non-secret
   settings: **API base URL**, default model, context budget, data directory, theme, telemetry
   (off default).
   - Typed getters/setters + change notification; sensible defaults per §3.5/§6.
   - Base URL default is the public Anthropic API; when the `ANTHROPIC_BASE_URL` env var is set,
     the Settings UI shows the effective (env) value as read-only/overridden so it's clear the
     environment is in control.
4. **`IKeyTester`** — calls the Models endpoint **at the resolved base URL with the resolved key**
   (mocked in tests) to validate the key and return the model list; maps 401 → "invalid key",
   network error → offline message (no stack traces).
5. **Settings view + view-model** — key entry (masked) with Test button, **API base URL field
   (with env-override indicator)**, default model, theme, context budget, data directory picker
   with write-validation, telemetry toggle.

## Suggested design
- Secrets and settings are separate stores: secret → `ISecureKeyStore`; preferences → `Setting` table.
- **Env wins, but is never persisted.** The resolver reads `ANTHROPIC_API_KEY` /
  `ANTHROPIC_BASE_URL` live; saving/clearing the stored key or base-URL setting must not touch
  the environment, and an env-supplied value must never be written into the store or a settings
  file (assert this in tests).
- Data-directory change validates writability before persisting; moving existing data is out of
  scope for M0 (validate + save path only) unless backup-restore covers migration later.
- Theme value is consumed by `design-system-theming`; default model by `model-selector`/`ai-gateway`;
  the effective key + base URL by `ai-gateway` (via `IApiCredentialProvider`).

## Test-first order
1. Secure-storage `@unit @integration` tests (including the "not in db/plaintext" assertions) →
   implement `ISecureKeyStore` + fake.
2. Credential-resolution tests (env-wins key + base-URL precedence, env never persisted) →
   implement `IApiCredentialProvider` over an injected `IEnvironment`.
3. Settings persistence/default tests (including base-URL default + env-override indicator) →
   implement `ISettingsService`.
4. Test-key success/invalid tests (mocked API at the resolved base URL) → implement `IKeyTester`.
5. `@ui` data-directory validation → implement Settings view.

## Definition of done
- Key is provably absent from db.sqlite and settings files (scanned in test).
- Key resolution follows env → store → none; base-URL resolution follows env → setting → default
  public API; an env-supplied key/URL is never written to the store or any settings file.
- Settings persist across restart with correct defaults; telemetry off; default model `claude-opus-5`;
  base URL defaults to the public Anthropic API.
- Test-key surfaces success + model list, and actionable errors for invalid/offline.

## Notes for later features
- `onboarding` reuses `ISecureKeyStore` + `IKeyTester` for the API-key step, and should skip/adapt
  the key step when `ANTHROPIC_API_KEY` is already supplied by the environment.
- `model-selector` reads/writes the default model via `ISettingsService`.
- `ai-gateway` obtains the effective key **and base URL** via `IApiCredentialProvider` (env wins,
  then secure store / settings) — never from plaintext, and never a hardcoded endpoint.
