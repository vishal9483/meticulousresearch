# Phase — App Branding & Icon

**SPEC:** §3.7 (app identity: application icon, product name, installer branding). **Milestone:** M6.
**Depends on:** design-system-theming

## Goal
Give the app a single, coherent brand identity in the **packaged** product: one application
icon everywhere it appears (window, taskbar, Start Menu, installer), the product name
**"MeticulousResearch Desktop"** shown consistently, and a look that matches the navy design
system. Owns the **brand assets** (icon, product name string, logo) that `installer` and the
About screen consume.

## Deliverables
1. **Application icon** — a multi-resolution `.ico` (16/32/48/256) matching the navy brand,
   referenced by the executable so Windows uses it for the window, taskbar, and Start Menu.
2. **Product-name source** — the display name "MeticulousResearch Desktop" defined once in
   branding metadata (assembly/product attributes) and read by the window title, About screen,
   and package manifest — no duplicated string literals.
3. **Window branding** — main window title shows the product name and the app icon in the title
   bar, reusing design-system-theming tokens (navy palette), no default WPF chrome.
4. **Onboarding branding** — the first-run welcome step shows the product name and brand
   identity (icon/logo + navy palette).
5. **Installer branding assets** — product name + icon exposed for the `installer` feature to
   surface in the setup UI and Start Menu entry.

## Suggested design
- Keep the icon and logo as design-system-adjacent assets so they track the navy palette
  (§3.7); the export "brand logo/accent" (Settings, §3.4.2) is a separate, user-configurable
  concern — do not conflate the app icon with the export logo.
- Store the product-name string in one place (e.g. `Directory.Build.props` / assembly product
  attribute) and expose an accessor the `@unit` test can read alongside the window title source.
- The About screen (from `about-screen`, M5) already displays version; here ensure it also reads
  the shared product name and icon rather than hard-coding them.

## Test-first order
1. `@unit` icon-sizes + single-source product-name tests → add the `.ico` and the shared name.
2. `@ui` window title/icon + branded-onboarding tests → wire the window and welcome step.
3. `@manual` taskbar/Start Menu/installer + coherence checklist → visual pass in the PR and on
   the packaged app.

## Definition of done
- One multi-resolution icon is used for the window, taskbar, Start Menu, and installer.
- Product name "MeticulousResearch Desktop" comes from a single source and is consistent across
  window title, About, onboarding, and package metadata.
- Onboarding and the packaged app read as one coherent navy-branded product; no default icon or
  placeholder name anywhere.

## Notes for later features
- `installer` consumes the icon and product name for the setup UI and Start Menu entry.
- `v1-acceptance` §9.1(1)/(10) checks the branded launch and absence of placeholders on a clean
  machine — the coherence checklist here feeds that gate.
