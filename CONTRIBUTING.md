# Contributing to Chartula

First off: thank you for being here. 💛

Chartula is being built in the open, and it only gets better with people like you.
Contributions of every size are welcome - a typo fix, a sharp bug report, a fresh idea, or a pull request.
You do not need to be a .NET expert or a changelog nerd to help.

> New to open source? This is a friendly place to start.
> Pick anything labelled [`good first issue`](https://github.com/goldbarth/chartula/labels/good%20first%20issue), or just open an issue and say hi.

---

## Code of Conduct

This project follows a [Code of Conduct](CODE_OF_CONDUCT.md).
By taking part, you agree to help keep this a welcoming and respectful space for everyone.

---

## Ways to contribute

There is more than one way to make Chartula better:

🐛 **Report a bug.**
Open an issue with what happened, what you expected, and how to reproduce it.
A security vulnerability is the exception: please report it privately, as described in [SECURITY.md](SECURITY.md).

💡 **Suggest a feature.**
Open an issue describing the problem you are trying to solve.
The problem matters more than a specific solution.

📖 **Improve the docs.**
Typos, unclear passages, and missing examples are all fair game and genuinely appreciated.

💻 **Contribute code.**
See [Project setup](#project-setup) and [Opening a pull request](#opening-a-pull-request) below.

---

## Before you start

Chartula is alpha in progress: the core architecture has settled, but the surface around it is still growing.

If you are planning anything beyond a small fix, please **open an issue first** to talk it through.
This saves you from duplicated effort and helps make sure a change fits where the project is heading.
Small fixes (typos, obvious bugs) can go straight to a pull request.

Worth reading before a first code change: [Architecture](docs/architecture.md).
It explains the layering, why facts are established before an LLM ever sees them, and which dependency choices exist to keep a native-AOT build reachable.

Decisions that already stand, and why, are recorded in [`docs/adr/`](docs/adr/):

- [Facts are established first, then rephrased](docs/adr/0001-facts-first-then-rephrase.md)
- [The git CLI and `HttpClient`, not LibGit2Sharp and Octokit](docs/adr/0002-git-cli-and-httpclient.md)
- [Endpoints and credential names come from the environment only](docs/adr/0003-endpoints-and-credentials-from-the-environment.md)

A change that goes against one of them starts as an issue, not a pull request.

Step-by-step guides for common extensions are in [`docs/recipes/`](docs/recipes/):

- [Adding a field to the fact base](docs/recipes/new-fact-field.md)

---

## Project setup

You will need the [.NET 10 SDK](https://dotnet.microsoft.com/download) installed, in exactly the version `global.json` names.
The pin is exact because the SDK decides the version of a package the release build needs (`Microsoft.NET.ILLink.Tasks`), and a locked restore fails when that drifts from `packages.lock.json`.

```bash
# Clone your fork
git clone https://github.com/goldbarth/chartula.git
cd chartula

# Restore dependencies
dotnet restore Chartula.slnx
```

Every project has a `packages.lock.json`, and CI restores with `--locked-mode`, so a build uses exactly the package versions recorded there.
If you add or update a package, commit the updated lock files with it; a pull request whose lock files do not match its project files fails CI.

---

## Build and test

```bash
# Build the solution
dotnet build Chartula.slnx -c Release

# Run the tests
dotnet test Chartula.slnx -c Release
```

Please make sure the project builds and the tests pass before opening a pull request.
Formatting rules live in `.editorconfig` and are applied automatically by most IDEs.
CI checks them with `dotnet format Chartula.slnx --verify-no-changes`; `dotnet format Chartula.slnx` applies them.

To get that check before a commit instead of after a push, turn on the pre-commit hook once:

```bash
git config core.hooksPath .githooks
```

It only checks and never changes or stages a file, and it adds about ten seconds to a commit, because the check loads the whole solution.
To commit once without it, use `git commit --no-verify`.

The suite needs no API key, no network and no tokens: the pipeline is tested by replaying stored fact bases, so you can run it as often as you like.
[Test fixtures](docs/test-fixtures.md) explains how that works and how to add a case.
A change to the prompt text also fails the suite until its snapshot is updated; [Prompt snapshots](docs/test-fixtures.md#prompt-snapshots) says how.

---

## Commit convention

Chartula uses [Conventional Commits](https://www.conventionalcommits.org/).
There is a nice bonus here: Chartula reads pull requests to generate changelogs, so clear commit and PR titles help the project *and* let the tool eat its own dog food.
A good title already reads like a changelog line.

**Pattern:** `type(optional-scope): short description` in imperative mood.

```
feat: add faithfulness check for rendered output
fix(render): correct heading level in customer changelog
docs: describe chartula.yaml configuration
chore: bump target framework
```

### Types

| Type | Meaning |
| --- | --- |
| `feat` | New functionality |
| `fix` | Bug fix |
| `refactor` | Structural change, no behavior change |
| `perf` | Performance improvement |
| `docs` | Documentation only |
| `test` | Tests only |
| `chore` | Setup, config, dependencies |
| `build` | Build system changes |
| `ci` | CI/CD configuration |
| `revert` | Revert a previous commit |

### Scopes (optional)

Scopes are **optional**.
Use one only when it will genuinely help later filtering or release notes; otherwise leave it off.

A scope names the area a change touches, which in practice is the folder it lives in:
`facts`, `curation`, `filtering`, `labels`, `categorization`, `generation`, `rendering`, `prompting`, `formatting`, `faithfulness`, `review`, `serialization`, `releases`, `history`, `llm`, `observability`.

Outside the domain, `cli`, `config`, `output`, `ci` and `repo` are in use.

If a change spans several scopes, drop the scope or split it into separate commits.

---

## Opening a pull request

A quick checklist before you hit "Create pull request":

- [ ] The solution builds without warnings (`dotnet build -c Release`)
- [ ] All tests pass (`dotnet test -c Release`)
- [ ] The change is focused - one concern per PR where possible
- [ ] No debug code, commented-out code, or unresolved `TODO` left behind
- [ ] The PR describes **what** changed and **why**, and links any related issue

Use imperative mood in your commit messages:

```
✔  add ticket search command
✘  added ticket search command
```

Do not worry about getting everything perfect.
Open the PR, and we will work through the rest together in review.

---

## Questions

Unsure about anything?
Open an issue or a discussion - no question is too small, and asking early is always welcome.
