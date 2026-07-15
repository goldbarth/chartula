<div align="center">

# Chartula

**Turn your merged pull requests into audience-ready release notes**
technical, customer-facing, and product, from a single source of truth, without the hallucinations.

[![Status](https://img.shields.io/badge/status-early%20development-orange)](#status)
[![.NET](https://img.shields.io/badge/.NET-cross--platform-512BD4?logo=dotnet&logoColor=white)](#)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)


</div>

**Chartula** is a cross-platform .NET CLI that reads the pull requests behind a release and produces multiple, audience-tailored changelogs at once.
It grounds every generated line against the actual facts of your PRs, so the output reads well *and* stays true to what really changed.

> *Chartula* (Latin) - "a small document, a little note". Which is exactly what a changelog entry is.

---

## Status

> 🚧 **Early development.** Chartula is being built in the open.
> The first usable release is on the way - see the [Roadmap](#roadmap) for what ships when.

---

## Why Chartula?

Release communication usually forces a trade-off:

| Approach | The trade-off |
| --- | --- |
| **Developer-oriented changelogs** | Read cleanly for engineers, but leave customers guessing what actually changed for them. |
| **Polished customer-facing release notes** | Look great, but tend to live outside your repository, behind hosting and subscriptions. |
| **Automated summaries** | Can turn thin commit messages into confident-sounding claims nobody can fully trust. |

Chartula aims at the gap in the middle: one pipeline, run from your own
repo, that turns the same set of pull requests into several audience-specific
outputs **and** checks each one against the facts before it ships.

---

## Core ideas

| Principle | What it means |
| --- | --- |
| 🎯 **PR-level, not commit-spam** | Changes are summarized per merged pull request, not per raw commit. |
| 👥 **Multi-audience from one source** | Technical, customer, and product-manager versions are rendered from a single structured fact base, so they can never contradict each other. |
| 🔒 **Grounded, not guessed** | An LLM only *rephrases* facts that are already established; it never decides what happened. A faithfulness check flags anything in the output that isn't backed by the facts. |
| 💸 **Runs in your repo, costs you nothing** | Ships as a `dotnet tool` / standalone binary. No hosting, no subscriptions - you bring your own model key. |
| ⚙️ **Configuration as code** | Everything is driven by a small `chartula.yaml` in your repository. Sensible defaults; grows only when you want it to. |

---

## Installation

> Not yet published. Installation instructions will land with the first release.

```bash
# Planned:
dotnet tool install -g Chartula
```

---

## Usage

> Command surface is being finalized. Planned entry points:

```bash
# Preview the next changelog locally, without publishing
chartula preview

# Generate changelogs for a release
chartula generate
```

---

## Configuration

Chartula is configured through a `chartula.yaml` file in your repository root.
The shipped file is intentionally minimal - Chartula works out of the box, and the config only exists to refine that default behavior.
Full options are documented (not pre-filled), so beginners aren't overwhelmed and advanced users can go deep.

---

## Roadmap

Development is staged so that **each phase is useful on its own**, not a fragment waiting on the next.

| Phase | Focus | What ships |
| --- | --- | --- |
| **1** | The usable core | The CLI running locally on a repo: PR-level collection, deterministic curation with label rules, the grounded fact base, audience-specific rendering, the faithfulness check with a lightweight review step, and stable repo outputs (`CHANGELOG.md`, `changelog.json`, GitHub release notes). Configuration and observability from day one. |
| **2** | Distribution & reach | Webhook output for Discord/Slack/Teams, a JavaScript widget over `changelog.json`, and an RSS feed as a low-cost static extra. |
| **3** | Ecosystem breadth | A GitHub Action as a thin wrapper around the CLI, publication as a `dotnet tool` on NuGet, and multi-OS binaries via CI. |

See the [project board](https://github.com/goldbarth/chartula/projects) for detailed tasks.

---

## Out of scope (for now)

These have been considered deliberately and set aside — not forgotten.
They may be revisited if there's real demand, so there's no need to open
an issue proposing them from scratch.

- **Email / SMS / WhatsApp broadcast.** Requires a subscriber list and a
  paid delivery service, which breaks Chartula's "runs in your repo, costs
  you nothing" model. A "bring your own service" hook is the likely path
  instead.
- **Read analytics & feedback buttons** (👍/👎, read counts). These need a
  server to collect and store events — again, hosted infrastructure Chartula
  intentionally avoids.
- **Scripting-based configuration** (e.g. Lua). Chartula's config is
  declarative by nature; embedding a scripting runtime would add weight for
  little gain — unless config ever needs real logic, at which point this
  becomes worth revisiting.

---

## Contributing

Contributions are welcome - bug reports, ideas, documentation fixes, and code.
Please read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md) before opening an issue or pull request.

---

## License

Licensed under the [MIT License](LICENSE). © 2026 Felix Wahl.
