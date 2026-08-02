# Phase — Model Selector

**SPEC:** §6, §6.1, §6.2, §6.3, §3.3. **Milestone:** M2. **Depends on:** conversations

## Goal
Expose Claude models as friendly tiers backed by a **config-driven catalog JSON** (§6.3), with
per-conversation and per-message selection, and record the model used on every turn. Owns the
**model catalog JSON** consumed by `ai-gateway` (which model id to send) and `cost-tracking`
(prices).

## Deliverables
1. **Model catalog JSON** — the shipped default at a known path (overridable in Settings)
   matching §6.3: `defaultModel`, `tiers[]` (tier, name, id, contextTokens, maxOutputTokens,
   priceInputMTok, priceOutputMTok, vision), and `additional[]`.
2. **`IModelCatalog`** in Core: `Tiers`, `AdditionalModels`, `DefaultModelId`,
   `Resolve(tierOrId)`, `TryGet(id)`, `IsVisionCapable(id)`, price lookup by id. Loads the JSON,
   validates it, and **falls back to the shipped default with a warning** on malformed input.
3. **Selection state** — a conversation carries a selected model (defaulting to the project
   default); a single turn may override it without mutating the conversation default.
4. **Model picker view/VM** — tiered picker with an "All models" expander; shows current model;
   surfaces the vision warning + "switch model" action when an image is in scope and the chosen
   model has `vision=false`.
5. **Recording** — the model id used is written to the assistant `Message.model` at completion
   (the persistence lives in `conversations`; this feature supplies the value).

## Suggested design
- Treat the JSON as source of truth; store **model ids** (not tier names) on turns so historical
  turns stay valid if tiers are re-pointed later.
- `Resolve` accepts either a tier name (→ its id) or a raw id (pass-through if present in catalog).
- Prices live only in the catalog; `cost-tracking` computes cost from persisted tokens ×
  current catalog prices (§3.6), so this feature must expose price-by-id cleanly.
- Vision check reads the `vision` flag; the warning/switch is advisory (does not hard-block).
- Catalog path + override handled via `ISettingsService`; keep loading pure/deterministic for tests.

## Test-first order
1. Catalog loading `@unit` tests (tiers, tier→id map, additional list, override, malformed
   fallback, default model) → JSON + `IModelCatalog`.
2. Selection/override/recording `@unit` tests → selection state + turn plumbing.
3. Vision-capability `@unit` test → vision check + warning hook.
4. `@ui` picker tests → model picker view.

## Definition of done
- Default catalog loads the four tiers with correct ids/prices and the additional models.
- Custom catalog overrides without a rebuild; malformed JSON falls back with a clear warning.
- Conversation default vs. per-message override behave per §3.3; model id recorded per turn.
- Non-vision model with an image in scope warns and offers a switch.

## Notes for later features
- `cost-tracking` reads prices via `IModelCatalog` by id (tokens are the stored ground truth).
- `ai-gateway`/`streaming` send the resolved model id per turn.
- `turn-metadata-actions` "retry with other model" reuses the picker + per-message override.
- `image-attachments` triggers the vision warning path when the selected model lacks vision.
