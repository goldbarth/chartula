# Contributing to Chartula

First off: thank you for being here. 💛

Chartula is being built in the open, and it only gets better with people like you.
Contributions of every size are welcome - a typo fix, a sharp bug report, a fresh idea, or a pull request.
You do not need to be a .NET expert or a changelog nerd to help.

> New to open source? This is a friendly place to start.
> Pick anything labelled [`good first issue`](https://github.com/goldbarth/chartula/labels/good%20first%20issue), or just open an issue and say hi.

---

## Contents

- [Code of Conduct](#code-of-conduct)
- [Ways to contribute](#ways-to-contribute)
- [Before you start](#before-you-start)
- [Project setup](#project-setup)
- [Build and test](#build-and-test)
- [Commit convention](#commit-convention)
- [Opening a pull request](#opening-a-pull-request)
- [Questions](#questions)

---

## Code of Conduct

This project follows a [Code of Conduct](CODE_OF_CONDUCT.md).
By taking part, you agree to help keep this a welcoming and respectful space for everyone.

---

## Ways to contribute

There is more than one way to make Chartula better:

| | How |
| --- | --- |
| 🐛 **Report a bug** | Open an issue with what happened, what you expected, and how to reproduce it. |
| 💡 **Suggest a feature** | Open an issue describing the problem you are trying to solve. The problem matters more than a specific solution. |
| 📖 **Improve the docs** | Typos, unclear passages, and missing examples are all fair game and genuinely appreciated. |
| 💻 **Contribute code** | See [Project setup](#project-setup) and [Opening a pull request](#opening-a-pull-request) below. |

---

## Before you start

Chartula is in early development, so the architecture is still settling.

If you are planning anything beyond a small fix, please **open an issue first** to talk it through.
This saves you from duplicated effort and helps make sure a change fits where the project is heading.
Small fixes (typos, obvious bugs) can go straight to a pull request.

---

## Project setup

You will need the [.NET SDK](https://dotnet.microsoft.com/download) installed.

```bash
# Clone your fork
git clone https://github.com/goldbarth/chartula.git
cd chartula

# Restore dependencies
dotnet restore Chartula.slnx
```

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

Scopes are **optional**, especially this early while the structure is still moving.
Use one only when it will genuinely help later filtering or release notes; otherwise leave it off.
As the pipeline takes shape, these areas are emerging as natural scopes:

| Scope | Area |
| --- | --- |
| `cli` | Command surface and entry points |
| `collect` | Pull request collection |
| `curate` | Curation and label rules |
| `facts` | The grounded fact base |
| `render` | Audience-specific rendering |
| `check` | Faithfulness check and review step |
| `output` | Repo outputs (`CHANGELOG.md`, `changelog.json`, release notes) |
| `config` | `chartula.yaml` and configuration |
| `ci` | CI/CD workflows |
| `repo` | Repository-level changes (meta, config) |

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
