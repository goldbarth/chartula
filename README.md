<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/assets/chartula-wordmark-dark.svg">
  <img src="docs/assets/chartula-wordmark-light.svg" alt="Chartula" width="380">
</picture>

**Turn your merged pull requests into audience-ready release notes**
technical, customer-facing, and product, from a single source of truth, without the hallucinations.

[![Status](https://img.shields.io/badge/status-phase%201%20complete-blue?style=flat-square)](#status)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](CONTRIBUTING.md)

</div>

**Chartula** is a cross-platform .NET CLI that reads the pull requests behind a release and produces multiple, audience-tailored changelogs at once.
It grounds every generated line against the actual facts of your PRs, so the output reads well *and* stays true to what really changed.

> *Chartula* (Latin) - "a small document, a little note". Which is exactly what a changelog entry is.

---

## Status

**Phase 1 is complete.** The pipeline runs end to end on a real repository, from reading pull requests to writing `CHANGELOG.md`, `changelog.json` and GitHub release notes.

It is not published yet, so there is no `dotnet tool install` and no prebuilt binary - both land in phase 3.
To try it today, [build it from source](#installation).

See the [Roadmap](#roadmap) for what ships when.

---

## Why Chartula?

Release communication usually forces a trade-off between three imperfect options.

**Developer-oriented changelogs** read cleanly for engineers, but leave customers guessing what actually changed for them.

**Polished customer-facing release notes** look great, but tend to live outside your repository, behind hosting and subscriptions.

**Automated summaries** can turn thin commit messages into confident-sounding claims nobody can fully trust.

Chartula aims at the gap in the middle: one pipeline, run from your own repo, that turns the same set of pull requests into several audience-specific outputs **and** checks each one against the facts before it ships.

---

## Core ideas

**🎯 PR-level, not commit-spam.**
Changes are summarized per merged pull request, not per raw commit.

**👥 Multi-audience from one source.**
Technical, customer, and product-manager versions are rendered from a single structured fact base, so they can never contradict each other.

**🔒 Grounded, not guessed.**
An LLM only *rephrases* facts that are already established; it never decides what happened.
A faithfulness check flags anything in the output that isn't backed by the facts.

**💸 Runs in your repo, costs you nothing.**
Ships as a `dotnet tool` / standalone binary. No hosting, no subscriptions - you bring your own model key.

**⚙️ Configuration as code.**
Everything is driven by a small `chartula.yaml` in your repository. Sensible defaults; grows only when you want it to.

---

## Installation

> Not yet published. `dotnet tool install -g Chartula` lands with the first release.

Until then, build it from source. You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/goldbarth/chartula.git
cd chartula
dotnet build Chartula.slnx -c Release
```

The CLI is then at `src/Chartula.Cli/bin/Release/net10.0/chartula`.

---

## Usage

Chartula needs a release tag that exists in your repository and the repository to read pull requests from.

```bash
# Show what would be produced, without writing anything
chartula preview --tag v1.2.0 --repo owner/name

# Produce the outputs and write them
chartula generate --tag v1.2.0 --repo owner/name
```

Two environment variables carry the credentials, and neither is ever read from a config file:

| Variable | Used for |
| --- | --- |
| `ANTHROPIC_API_KEY` | The model that rephrases the facts. |
| `GITHUB_TOKEN` | Reading pull requests and writing release notes. |

`generate` writes three outputs:

- **`CHANGELOG.md`** - the technical rendering, prepended to your existing file.
- **`changelog.json`** - every audience text plus the fact base behind them, in a [documented, stable format](docs/changelog-json.md).
- **GitHub release notes** - the technical rendering, attached to the release for the tag.

Every run ends with a summary of what it did and what it cost in tokens.
See [Run metrics](docs/run-metrics.md) for how to read it.

---

## Configuration

Chartula works out of the box.
A `chartula.yaml` in your repository root only exists to refine that default behavior, and every setting can also be given as an environment variable.

[`chartula.example.yaml`](chartula.example.yaml) is a commented starting point - copy it and uncomment only what you need.
Full options are documented (not pre-filled) in [Configuration](docs/configuration.md), so beginners aren't overwhelmed and advanced users can go deep.

---

## Documentation

| Document | What it covers |
| --- | --- |
| [Architecture](docs/architecture.md) | The layering, the pipeline, and the choices behind them. |
| [Configuration](docs/configuration.md) | Every `chartula.yaml` section and its defaults. |
| [`changelog.json` format](docs/changelog-json.md) | The stable output schema other tools build on. |
| [Run metrics](docs/run-metrics.md) | Reading a run's cost, and judging whether the thorough check earns it. |
| [Test fixtures](docs/test-fixtures.md) | How the pipeline is tested without spending tokens. |
| [Contributing](CONTRIBUTING.md) | Working on Chartula. |

---

## Roadmap

Development is staged so that **each phase is useful on its own**, not a fragment waiting on the next.

### Phase 1 - The usable core ✅

The CLI running locally on a repo: PR-level collection, deterministic curation with label rules, the grounded fact base, audience-specific rendering, the faithfulness check with a lightweight review step, and stable repo outputs (`CHANGELOG.md`, `changelog.json`, GitHub release notes).
Configuration and observability from day one.

### Phase 2 - Distribution & reach

Webhook output for Discord/Slack/Teams, a JavaScript widget over `changelog.json`, and an RSS feed as a low-cost static extra.

### Phase 3 - Ecosystem breadth

A GitHub Action as a thin wrapper around the CLI, publication as a `dotnet tool` on NuGet, and multi-OS binaries via CI.

See the [project board](https://github.com/goldbarth/chartula/projects) for detailed tasks.

---

## Out of scope (for now)

These have been considered deliberately and set aside - not forgotten.
They may be revisited if there's real demand, so there's no need to open an issue proposing them from scratch.

- **Email / SMS / WhatsApp broadcast.**
  Requires a subscriber list and a paid delivery service, which breaks Chartula's "runs in your repo, costs you nothing" model.
  A "bring your own service" hook is the likely path instead.
- **Read analytics & feedback buttons** (👍/👎, read counts).
  These need a server to collect and store events - again, hosted infrastructure Chartula intentionally avoids.
- **Scripting-based configuration** (e.g. Lua).
  Chartula's config is declarative by nature; embedding a scripting runtime would add weight for little gain - unless config ever needs real logic, at which point this becomes worth revisiting.

---

## Contributing

Contributions are welcome - bug reports, ideas, documentation fixes, and code.
Please read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md) before opening an issue or pull request.

---

## License

Licensed under the [MIT License](LICENSE). © 2026 Felix Wahl.
