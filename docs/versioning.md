# Versioning Policy

Chartula follows [Semantic Versioning 2.0.0](https://semver.org/) for the NuGet package, with NuGet's own pre-release conventions layered on top.
This document is the binding rule for every version number Chartula publishes, from the current pre-release phase through 1.0.0 and beyond.

## 1. Basis

- **SemVer 2.0** governs the three-part version number and what each part means.
- **NuGet pre-release conventions** govern the suffix: a hyphen followed by a label and number (`-preview.N`), which NuGet treats as a pre-release identifier and excludes from default package listings.
- SemVer build metadata (`+build`) is not used. It carries no meaning for how the package is resolved or installed, and NuGet does not surface it.

## 2. Version scheme

```
MAJOR.MINOR.PATCH[-preview.N]
```

| Segment | Meaning | Increases when |
| --- | --- | --- |
| `MAJOR` | Breaking change | Public API, CLI surface, config schema, or output contract (`changelog.json`, file layout) changes in a way that breaks existing users |
| `MINOR` | New functionality | A feature is added in a backward-compatible way |
| `PATCH` | Fix, no new functionality | A bug is fixed without changing behaviour anyone correctly relies on |
| `-preview.N` | Pre-release iteration | A pre-1.0 build that has not yet been validated as stable |

Each segment resets the ones to its right: a MINOR bump resets PATCH to 0, a MAJOR bump resets MINOR and PATCH to 0.

## 3. Pre-release phase (current)

Chartula starts at `1.0.0-preview.1`, not `0.1.0`.
The target shape of the 1.0 API and CLI surface is already known; what is still being validated is whether the implementation holds up, not what the product is.

- **Label:** `preview` only. No `alpha` or `beta` labels are used, to keep the signal to users simple: "not yet stable" is the only distinction that matters before 1.0.
- **Incrementing:** each new pre-release build increments `N` by one (`preview.1` -> `preview.2` -> `preview.3`, …). `N` never resets while still pre-1.0.0, and no meaning is attached to the number itself beyond ordering.
- **What triggers a new preview:** any change worth shipping, feature or fix, ships as the next `preview.N`. There is no separate PATCH/MINOR tracking underneath the preview label.
- **Exit criteria - when preview ends and `1.0.0` ships:**
  - The known bugs blocking day-to-day use are fixed.
  - The public surface (CLI commands, config schema, `changelog.json` schema) has had at least one preview cycle without a breaking change.
  - The tool has been used on real, external repositories, not just its own.

Until all three hold, the next release is another `preview.N`, never `1.0.0`.

## 4. Stable release and beyond

`1.0.0` is published the moment the exit criteria in section 3 are met, not on a fixed calendar date.
From `1.0.0` onward, normal SemVer rules apply without qualification:

| Change type | Example | Version bump |
| --- | --- | --- |
| Bug fix, no surface change | Wrong label applied to a change category | `PATCH` |
| Backward-compatible feature | New audience type, new config option with a default | `MINOR` |
| Breaking change | Renamed CLI flag, changed `changelog.json` schema, dropped config key | `MAJOR` |

A `MAJOR` release after `1.0.0` is a deliberate, infrequent event.
It is announced ahead of time, and a migration note ships with the release (see section 5).

## 5. Practical rules

**When a new version ships**

- A version is published when there is something worth giving to users, not on a schedule. A pending fix or feature is reason enough; an empty release is not.
- Every published version, pre-release or stable, gets a Chartula-generated changelog entry. The tool that writes changelogs uses itself.

**Breaking changes**

- Any breaking change is called out explicitly in the release notes under its own heading, not buried in a bullet list.
- Before `1.0.0`: a breaking change is allowed between preview builds, but a preview that breaks something working in the previous preview should say so plainly in its release notes.
- After `1.0.0`: a breaking change requires a `MAJOR` bump, a documented migration path, and, where feasible, a deprecation warning in the release before it.

**NuGet specifics**

- Pre-release packages (any version with a `-preview.N` suffix) are excluded from `dotnet add package` and the NuGet.org listing by default; installing one requires `--prerelease` or an explicit version.
- A stable package must never depend on a pre-release package. A pre-release package may depend on either.
- Once `1.0.0` ships, dependency ranges published in downstream tooling or documentation should follow SemVer-compatible ranges (e.g. `[1.0.0, 2.0.0)`), so a `MAJOR` bump cannot silently reach consumers.

**Version string formatting**

- Always write the full three-part version, even when the patch is zero (`1.0.0`, not `1.0`).
- Pre-release versions are always written with the label and number separated by a dot (`1.0.0-preview.1`), matching NuGet's own sort order for pre-release identifiers.
- Git tags mirror the package version exactly, prefixed with `v` (`v1.0.0-preview.1`, `v1.0.0`).

## Quick reference

- Format: `MAJOR.MINOR.PATCH[-preview.N]`
- Pre-release label: `preview` only, no `alpha`/`beta`
- Pre-1.0: every shippable change bumps `preview.N` by one; `N` never resets
- `1.0.0` ships when known bugs are fixed, the surface has held stable for one preview cycle, and the tool has run against external repos
- After `1.0.0`: PATCH = fix, MINOR = compatible feature, MAJOR = breaking change
- Breaking changes always get their own heading in the release notes; after `1.0.0` they always require a MAJOR bump
- Stable packages never depend on pre-release packages
- Git tags mirror the package version: `v1.0.0-preview.1`, `v1.0.0`
