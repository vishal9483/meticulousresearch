# Phase — URL Resource

**SPEC:** §3.2. **Milestone:** M1. **Depends on:** text-paste-resource

## Goal
Add the **URL resource type**: at add-time, fetch the page, convert it to clean text/markdown,
store the extracted text, and **retain the original URL** for provenance. Conversion happens once
at add-time so preview and grounding work offline afterward.

## Deliverables
1. **`IResourceService.AddUrl(projectId, url)`** (extends the shared contract) — validate →
   fetch → convert → store extracted text → populate fields → save.
2. **`IUrlFetcher`** abstraction — performs the HTTP fetch; injectable so tests use a fake with
   scripted responses (bodies, titles, 404/500/timeout/connection-error) with no real network.
3. **HTML→markdown conversion** — extract the main readable content (strip nav/ads/boilerplate),
   convert to markdown, capture the page title for the default resource title.
4. **Storage** — write converted text to the resource's `extracted.txt`; set `source_uri` to the
   exact original URL; `blob_path` optional (may store the raw fetched HTML for re-extract).
5. **Errors** — malformed URL, fetch failure, and empty-content cases produce actionable errors
   and create no resource (SPEC §3.7).

## Suggested design
- Reuse the shared storage/sizing/estimate flow; only fetch+convert is new.
- `IUrlFetcher` returns status + content-type + body so the service decides convert vs. error;
  keep all network behind it so unit tests stay deterministic and offline.
- Readability-style main-content extraction before markdown conversion for clean grounding text.
- Since conversion is at add-time, preview reads the stored `extracted.txt` — no re-fetch
  (proves the offline-preview scenario).
- Estimation via shared `ITokenEstimator`; timestamps via `IClock`.

## Test-first order
1. Add-URL fetch+convert + provenance `@unit` tests → `AddUrl` + fake fetcher.
2. Boilerplate-strip + title-default `@unit` tests → conversion.
3. Storage + offline-preview `@unit @integration` tests → extracted.txt + no re-fetch.
4. Malformed / fetch-failure / empty `@unit` tests → validation + error paths.
5. Fetch-progress preview `@ui` test → Resources view wiring.

## Definition of done
- All add/convert/provenance/failure `@unit` (+ `@integration`) scenarios green; `@ui` green.
- Extracted markdown stored at the §5 path; original URL retained exactly in `source_uri`.
- All network is behind `IUrlFetcher`; no scenario touches the real internet.

## Notes for later features
- `resource-management` "re-extract" can re-fetch (or re-convert stored HTML) via the same path.
- `full-text-search` indexes the converted text; `context-budget`/`token-estimation` consume the
  token estimate exactly as for other resource types.
