<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/assets/chartula-wordmark-dark.svg">
  <img src="docs/assets/chartula-wordmark-light.svg" alt="Chartula" width="380">
</picture>

**Release notes written from your merged pull requests, and checked against them before you publish.**

[![Status](https://img.shields.io/badge/status-alpha-1BA897?style=flat-square&labelColor=26201A)](#try-it)
[![CI](https://img.shields.io/github/actions/workflow/status/goldbarth/chartula/ci.yml?branch=main&style=flat-square&label=CI&labelColor=26201A)](https://github.com/goldbarth/chartula/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-C68A35?style=flat-square&labelColor=26201A)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-C68A35?style=flat-square&labelColor=26201A)](CONTRIBUTING.md)

</div>

Chartula reads the pull requests merged into a release and writes its release notes: a changelog for developers, a page for the people who use your product, and on request a summary for product managers.
Before a model writes a word, Chartula decides which changes the release contains, how each one is categorized, and whether it breaks something.
After the model has written, Chartula checks the text against your pull requests and reports the statements it finds they do not back.
So you know which lines to read first before you publish.

Chartula is a single binary for Linux, macOS and Windows.
It runs in your checkout or your CI job with your own model key or a local model, without a server and without a subscription.

> *Chartula* (Latin) - "a small document, a little note". Which is exactly what a changelog entry is.

---

## What it looks like

Chartula's own release notes and [changelog](CHANGELOG.md) are written by Chartula.
This is from the run that wrote them for `v0.1.0-preview.3`, on 2026-09-24, with `gpt-6-sol`.

**What the pull request says.**
[#252](https://github.com/goldbarth/chartula/pull/252) tightened the instructions for customer entries and measured the effect: [flags](docs/glossary.md#flag) on outcomes that promised more than the [facts](docs/glossary.md#fact) dropped from 5.7 to 1.75 per run.
Fewer, not none.

**What Chartula wrote for customers:**

> **Review flag reasons and customer outcomes:** Review flags could quote a claim without explaining why it was flagged, and customer outcomes could promise more than the facts supported. Flags now carry their reasons, and you can count on customer outcomes to state limits named in the facts.

**What the check reported below it:**

```text
Flagged for review:
  ! #252: "you can count on customer outcomes to state limits named in the facts" - #252 changed the customer prompt to require fact-bounded outcomes, but its measured runs still had outcome-related flags. The facts do not establish that customer outcomes now consistently state the limits in the facts.
```

The pull request made overpromising entries rarer.
The entry tells your users they can count on outcomes staying within the facts.
Chartula leaves the sentence as the model wrote it and tells you it is the one to read before you publish.
The [thorough check](docs/glossary.md#thorough-check), which asks a model, found it; the free [rule-based check](docs/glossary.md#rule-based-check), which looks for numbers and names the pull requests do not contain, had nothing to find here.

<details>
<summary>The same finding in the run's record</summary>

The local [run record](docs/run-record.md) keeps every flag with the pull request it concerns, and what the run was made with.
Shortened to those parts:

```json
{
  "tag": "v0.1.0-preview.3",
  "repository": "goldbarth/chartula",
  "mode": "generate",
  "provenance": {
    "toolVersion": "0.1.0-preview.3+7620e6d95b62190d12f57ba345479b524674fea1",
    "provider": "openai-compatible",
    "model": "gpt-6-sol",
    "promptHash": "sha256:18467240be1e8a49ca134d8d94ebaf281104fff70ad715fbc0c6b71dc048b5c1",
    "thinking": "disabled",
    "thoroughCheck": true,
    "factBaseDepth": "title-and-description",
    "checkModel": "gpt-6-sol",
    "checkThinking": "disabled"
  },
  "audiences": [
    {
      "audience": "technical",
      "rendered": true,
      "flags": []
    },
    {
      "audience": "customer",
      "rendered": true,
      "flags": [
        {
          "text": "\"you can count on customer outcomes to state limits named in the facts\" - #252 changed the customer prompt to require fact-bounded outcomes, but its measured runs still had outcome-related flags. The facts do not establish that customer outcomes now consistently state the limits in the facts.",
          "pullRequest": 252
        }
      ]
    }
  ]
}
```

</details>

---

## Try it

**Alpha.** Chartula runs end to end on real repositories, and its output is for a person to read before it is published.
If you try it, [tell us how it went](https://github.com/goldbarth/chartula/issues/new?template=alpha-feedback.yml).

You need git, a checkout of your repository with its tags, a model key or a local model, and a GitHub token to publish.
Install Chartula on Linux or macOS:

```bash
curl -fsSL https://raw.githubusercontent.com/goldbarth/chartula/main/install.sh | sh
```

On Windows, in PowerShell:

```powershell
irm https://raw.githubusercontent.com/goldbarth/chartula/main/install.ps1 | iex
```

Set the model key and the GitHub token, and run Chartula from your checkout:

```bash
export ANTHROPIC_API_KEY=<your key>
export GITHUB_TOKEN=<your fine-grained token>

cd my-repo
chartula doctor      # check the setup, and what to fix, before a run spends anything
chartula preview     # show the facts of the nearest tag and what generate would send, for free
chartula generate    # write the release notes, check them, and create a draft release on GitHub
```

`generate` writes `CHANGELOG.md`, `release-<tag>.md` and `changelog.json` into your checkout, and creates a [draft](docs/glossary.md#draft) release on GitHub that only people with write access see.
On a tag whose release is already published, it stops before the first model call, and `--replace-published` replaces the published notes.
`generate --no-publish` writes the files and leaves GitHub alone.
[Outputs](docs/outputs.md) shows each file, and [GitHub](docs/github.md#the-token) the permissions the token needs.

[Getting started](docs/getting-started.md) walks through the first run step by step, up to a published draft.
To run Chartula in a GitHub Actions job instead, start with [CI](docs/ci.md).

---

## How the check works

**The model cannot add or drop a change.**
Which pull requests belong to the release, how each is categorized, whether it is breaking, and which group, marker and link its entry gets are all decided before the model is called.
The model writes one entry per change it is given; an answer that leaves a change out, writes one twice, or adds one it was not given fails instead of being written.

**Two checks read every text, and say why they flag.**
The rule-based check costs nothing: it flags numbers and quoted names the pull requests do not contain.
The thorough check asks a model whether each statement is backed by the pull requests, and can use another model than the one that wrote the text.
Each flag names its reason and, where it can, the pull request it concerns.
Neither check catches every unsupported statement, so a run without flags still needs a reader.

**A flag is advice.**
It does not change the text, and it does not stop the run or the publication.
Read the flags in the terminal before you publish; no written file marks a flagged entry.

**The checks compare with your pull requests, not with your code.**
A wrong pull request description produces a well-worded wrong entry, and both checks measure against that same text.

**You pay only your model provider.**
Chartula itself needs no hosting and no subscription, and against a model on your own machine a run costs nothing.
`preview` shows how much a run would send before it spends anything, and every run ends with what it cost ([Costs and checks](docs/costs-and-checks.md)).

---

## Providers

- **Anthropic**, the default: `ANTHROPIC_API_KEY`, and `claude-sonnet-5` unless you choose another model.
- **OpenAI**: `llm.provider: openai-compatible`, a model id, `https://api.openai.com/v1` as the endpoint, and `OPENAI_API_KEY`.
- **A hosted endpoint** that speaks OpenAI's dialect: the same, with its own URL, model id and key variable.
- **A local server** such as Ollama or LM Studio: the same, with no key, and no release data leaves your machine.

[Providers](docs/providers.md) has a recipe for each, and the models Chartula has been compared on.

---

## Known limitations

These are known, and a person reads the output before it is published, so each one is left for after the launch.
The [Roadmap](ROADMAP.md#after-the-launch-in-this-order) says which point removes which limitation, and in which order.

**Source data**

- **Facts are author text.** A wrong pull request title or description stays wrong, and the checks verify against that same text, not against the code.
- **Pull request text goes to the model as it is.** It is neither delimited nor size-limited, and edits made to a pull request after its merge are not detected.
- **Breaking status comes from the text.** A `BREAKING CHANGE:` footer or a `!` sets it. Links in pull request text are passed through unchecked.
- **Two pull requests with the same change become two entries.**
- **GitHub is the only source.** Chartula reads pull requests from GitHub or GitHub Enterprise, and from no other host.

**Checking**

- **Flags appear in the terminal only.** Nothing in the written files marks a flagged entry, and a run with flags still exits with `0`.

**Publishing**

- **Only the release notes are a draft.** The customer page and `CHANGELOG.md` are written directly.

**Installation and support**

- **No GitHub Action yet.**
- **The binaries are not code-signed.** Downloaded through a browser, macOS Gatekeeper and Windows SmartScreen warn on first start; the install scripts avoid that.
- **No package manager.** No Homebrew, Scoop or winget; the install scripts or a download.

---

## Documentation

**Start**

| Document | What it covers |
| --- | --- |
| [Getting started](docs/getting-started.md) | The first run, step by step: install, `doctor`, `preview`, `generate`, and publishing the draft. |
| [Install](docs/install.md) | Platforms, the install scripts and their settings, a download by hand, building from source, updating and uninstalling. |
| [Providers](docs/providers.md) | Setting up Anthropic, OpenAI, a hosted endpoint or a local server, choosing a model, and `thinking`. |
| [GitHub](docs/github.md) | The token and its permissions, the rate limit, GitHub Enterprise, and what a run writes to GitHub. |
| [Troubleshooting](docs/troubleshooting.md) | The messages a run most often stops with, what causes each, and the fix. |

**Understand the output**

| Document | What it covers |
| --- | --- |
| [What goes into a release](docs/what-goes-into-a-release.md) | The [range](docs/glossary.md#range), pull requests and commits, reverts, categories, breaking changes, filters, and which [audience](docs/glossary.md#audience) a change reaches. |
| [Outputs](docs/outputs.md) | Every file a run writes, and the shape of the technical, customer and product texts, with real examples. |
| [Writing pull requests](docs/writing-pull-requests.md) | What to ask of your authors, level by level, what each level changes in the output, and a CI check for the title. |
| [Costs and checks](docs/costs-and-checks.md) | A run's size before it runs, reading its cost, whether the thorough check earns it, and what the model comparison found. |
| [Glossary](docs/glossary.md) | The terms the other pages use, such as fact, rendering and flag, each with one meaning. |

**Automate**

| Document | What it covers |
| --- | --- |
| [CI](docs/ci.md) | A complete GitHub Actions job, what happens to its draft, and an Alpine variant. |
| [`changelog.json` format](docs/changelog-json.md) | The release as data for other tools, its schema, and reading it with `jq`. |
| [Versioning](docs/versioning.md) | The version scheme, preview and alpha, when 1.0 ships, and what you can build on. |

**Look up**

| Document | What it covers |
| --- | --- |
| [CLI](docs/cli.md) | Every command and option with its default, the environment, what goes to stdout and stderr, and the exit status. |
| [Configuration](docs/configuration.md) | Every `chartula.yaml` key with its default and valid values, and settings as environment variables. |
| [Run record](docs/run-record.md) | The local file each `generate` run keeps, for comparing runs. |

**Contribute**

| Document | What it covers |
| --- | --- |
| [Contributing](CONTRIBUTING.md) | The workflow, the setup and the conventions. |
| [Architecture](docs/development/architecture.md) | The layering, the pipeline, and the choices behind them. |
| [Decision records](docs/development/adr/README.md) | The decisions that constrain a change, with their reasons, and how to record a new one. |
| [Testing](docs/development/testing.md) | How each project is tested without a network or tokens, running a single test, and adding a fixture. |
| [Releasing](docs/development/releasing.md) | Making a release, from the version bump to the committed changelog. |
| [Roadmap](ROADMAP.md) | What comes next, in order. |

A prompt change moves the output in ways no unit test catches.
Everything that compares runs against each other lives in a separate repository, [chartula-evals](https://github.com/goldbarth/chartula-evals).

---

## Feedback and contributing

Feedback on the alpha helps most right now.
[The feedback form](https://github.com/goldbarth/chartula/issues/new?template=alpha-feedback.yml) asks for what we can act on: which work Chartula took off your hands, which it added, a flag that confused you, and where the setup got in your way.
Bug reports, documentation fixes and code are welcome too; please read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md) first.

---

## License

Licensed under the [MIT License](LICENSE). © 2026 Felix Wahl.
