# Versioning Policy

Chartula follows [Semantic Versioning 2.0.0](https://semver.org/) for its releases.
A release is a GitHub release with one self-contained binary per platform, so using Chartula needs no .NET.
This document is the binding rule for every version number Chartula publishes, from the current pre-release phase through 1.0.0 and beyond.

## 1. Basis

- **SemVer 2.0** governs the three-part version number and what each part means.
- **The pre-release suffix** is a hyphen followed by a label and number (`-preview.N`), a SemVer pre-release identifier.
- SemVer build metadata (`+build`) is never part of a published version.
  The SDK appends the commit a binary was built from (`0.1.0-preview.1+<sha>`), and `changelog.json` records that form in `toolVersion`, so a file names the build that wrote it; the tag, the release and the User-Agent carry the version without it.

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

Chartula starts at `0.1.0-preview.1`, not `1.0.0-preview.1`.
SemVer reserves `0.y.z` for initial development, where anything may change, and that is where Chartula is: the CLI flags, the shape of each rendering and what `generate` publishes are still moving, and the alpha exists to collect feedback that may move them further.
A `1.0.0` pre-release would promise that the shape of 1.0 is already known, and every correction the feedback asks for would then cost a `2.0.0`.

- **Version core:** fixed at `0.1.0` for the whole pre-release phase.
  The core carries no meaning yet, so bumping it would suggest a distinction that is not there.
- **Label:** `preview` only.
  No `alpha` or `beta` labels are used in version numbers, to keep the signal to users simple: "not yet stable" is the only distinction that matters before 1.0.
  The project calls this phase "alpha" in prose; the version number does not repeat it.
- **Why a suffix on a `0.y.z` version:** the suffix says "not yet stable" in the version itself, wherever the version appears.
  The GitHub release itself is not marked as a pre-release: the suffix says it already, and only a release GitHub counts as latest can be installed through the stable `releases/latest/download` link the install script uses.
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

**Release assets**

- Every release carries one self-contained single-file binary per platform: `chartula-linux-x64`, `chartula-linux-arm64`, `chartula-linux-musl-x64`, `chartula-linux-musl-arm64`, `chartula-osx-x64`, `chartula-osx-arm64`, `chartula-win-x64.exe`, `chartula-win-arm64.exe`.
  The `linux-musl` binaries are for Alpine and other musl systems, and need `libstdc++` there.
- `SHA256SUMS` lists the checksum of every binary, and every binary has a GitHub build provenance attestation (`gh attestation verify <file> --repo goldbarth/chartula`).
- The `Version` in `src/Chartula.Cli/Chartula.Cli.csproj` is the only place the version is written.
  The binary, the User-Agent it sends and `changelog.json` all read it from there.

**How a release is made**

1. Set `Version` in the csproj to the next version, in a pull request like any other change.
2. After it is merged, tag the merge commit on `main` and push the tag (`v0.1.0-preview.2`).
3. `.github/workflows/release.yml` refuses a tag that does not match the csproj or is not on `main`, runs CI on the tagged commit, builds and smoke-tests each binary on its own platform (the Linux ones in a Debian slim or an Alpine container, which have none of the runner's extra libraries), and creates a draft release with the binaries, `SHA256SUMS` and the attestations.
4. `chartula generate` for the tag writes the notes into that draft; a person reads them and publishes the release.
5. Publishing runs `.github/workflows/install.yml` against the release: the install scripts, then a real `chartula preview`, in fresh containers of every mainstream Linux (glibc and musl, x64 and arm64), on macOS and on Windows.

**Version string formatting**

- Always write the full three-part version, even when the patch is zero (`1.0.0`, not `1.0`).
- Pre-release versions are always written with the label and number separated by a dot (`0.1.0-preview.1`), so SemVer compares the number numerically and `preview.10` sorts after `preview.9`.
- Git tags mirror the version exactly, prefixed with `v` (`v0.1.0-preview.1`, `v1.0.0`).

## Quick reference

- Format: `MAJOR.MINOR.PATCH[-preview.N]`
- Pre-release phase: `0.1.0-preview.N`, label `preview` only, no `alpha`/`beta`
- Pre-1.0: every shippable change bumps `preview.N` by one; `N` never resets
- `1.0.0` ships when known bugs are fixed, the surface has held stable for one preview cycle, and the tool has run against external repos
- After `1.0.0`: PATCH = fix, MINOR = compatible feature, MAJOR = breaking change
- Breaking changes always get their own heading in the release notes; after `1.0.0` they always require a MAJOR bump
- The csproj `Version` is the single source; a tag must match it
- Git tags mirror the version: `v0.1.0-preview.1`, `v1.0.0`
