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
The [bug report form](https://github.com/goldbarth/chartula/issues/new?template=bug_report.yml) asks for what a maintainer needs to run your case again.
A security vulnerability is the exception: please report it privately, as described in [SECURITY.md](SECURITY.md).

💡 **Suggest a feature.**
The [feature request form](https://github.com/goldbarth/chartula/issues/new?template=feature_request.yml) asks for the problem you are trying to solve, which matters more than a specific solution.

📖 **Improve the docs.**
Typos, unclear passages, and missing examples are all fair game and genuinely appreciated.
[Writing documentation](#writing-documentation) has the rules the pages follow.

💻 **Contribute code.**
The rest of this page takes you from a fork to a merged pull request.

---

## Before you start

If you are planning anything beyond a small fix, please **open an issue first** to talk it through.
This saves you from duplicated effort and helps make sure a change fits where the project is heading.
Small fixes (typos, obvious bugs) can go straight to a pull request.

Worth reading before a first code change:

- [Architecture](docs/development/architecture.md): how a run moves through the code, and where each concern lives.
- [Architecture decision records](docs/development/adr/README.md): the decisions that already stand, and why. A change that goes against one of them starts as an issue, not a pull request.
- [Recipes](docs/development/recipes/), step-by-step guides for common extensions, such as [adding a field to the fact base](docs/development/recipes/new-fact-field.md).

---

## From a fork to a merged pull request

1. **Fork** the repository on GitHub, clone your fork, and add this repository as `upstream`:

   ```bash
   git clone https://github.com/<you>/chartula.git
   cd chartula
   git remote add upstream https://github.com/goldbarth/chartula.git
   ```

2. **Branch** from an up-to-date `main`, named after the type of change and its topic, such as `fix/strict-arguments` or `docs/glossary`:

   ```bash
   git fetch upstream
   git switch -c fix/my-topic upstream/main
   ```

3. **Commit** with [Conventional Commits](#titles-conventional-commits), build and test (see below), and push the branch to your fork.
4. **Open a pull request** against `main`.
   Its title and description follow [The pull request](#the-pull-request) below, and the template starts you off.
5. **Review:** CI builds, tests and checks the formatting; a maintainer reads the change and may ask for more.
6. **Merge:** the maintainer squash-merges the pull request into one commit.
   Its title is the pull request's title, and its message is the description.
   So a `Closes #123` line in the description closes the issue on merge and stays in the history next to the change.

### Your pull request is Chartula's input

Chartula writes its own release notes from the merged pull requests, the way it writes yours.
The title is the line a release names the change by, and its type decides the category and whether the change appears at all ([types below](#types)).
The description is what the model reads to word the entry, so what it says can end up in a published release note.
[Writing pull requests](docs/writing-pull-requests.md) shows what each part changes in the output.

---

## Project setup

You need git and the [.NET 10 SDK](https://dotnet.microsoft.com/download), in exactly the version `global.json` names (`10.0.111` today).
The pin is exact because the SDK decides the version of a package the release build needs (`Microsoft.NET.ILLink.Tasks`), and a locked restore fails when that drifts from `packages.lock.json`.
With the official install script, for example: `./dotnet-install.sh --version 10.0.111`.

```bash
dotnet restore Chartula.slnx
```

Every project has a `packages.lock.json`, and CI restores with `--locked-mode`, so a build uses exactly the package versions recorded there.
When you add or update a package, the next `dotnet restore Chartula.slnx` rewrites the lock files of the projects it changes; commit them with the change.
Check before you push that a locked restore still passes, as CI runs it:

```bash
dotnet restore Chartula.slnx --locked-mode
```

---

## Build, test and format

The same commands CI runs:

```bash
dotnet build Chartula.slnx -c Release -warnaserror   # a warning fails the build
dotnet test Chartula.slnx -c Release                  # no key, no network, no tokens
dotnet format Chartula.slnx --verify-no-changes       # formatting as .editorconfig sets it
```

One class or one test, in the project that holds it:

```bash
dotnet test tests/Chartula.Core.Tests -c Release --filter "FullyQualifiedName~FactBaseBuilderTests"
```

`dotnet format Chartula.slnx` applies the formatting rules; most IDEs apply them as you type.
To get the format check before a commit instead of after a push, turn on the pre-commit hook once:

```bash
git config core.hooksPath .githooks
```

It only checks and never changes or stages a file, and it adds about ten seconds to a commit, because the check loads the whole solution.
To commit once without it, use `git commit --no-verify`.

[Testing](docs/development/testing.md) explains how each project is tested, how to add a fixture, and how a prompt change updates its snapshot.

---

## Trying the CLI on a repository

The built CLI is `src/Chartula.Cli/bin/Release/net10.0/chartula`.
Run it from a checkout of any repository with tags, as a user would:

```bash
cd ~/some-repo
~/chartula/src/Chartula.Cli/bin/Release/net10.0/chartula preview --tag v1.2.0
```

- **`preview`** makes no model call: it shows the facts and what `generate` would send, and costs GitHub requests only.
- **`generate --no-publish`** makes the model calls and pays for them in tokens, about one rendering and one check per audience, and writes the files into the checkout you run it in. It publishes nothing.
- **`generate`** without `--no-publish` creates or changes a GitHub release; use it on a repository of your own only.

To exercise the model path without tokens, point `Chartula__Llm__BaseUrl` and `Chartula__GitHub__ApiBaseUrl` at a local stub: `http` is allowed for an address on your machine ([Providers](docs/providers.md)).
For most changes the test suite is the cheaper proof, and a run on a real repository is the evidence a pull request shows when behavior a user meets has changed.

---

## Titles: Conventional Commits

Commit messages and pull request titles follow [Conventional Commits](https://www.conventionalcommits.org/), in the imperative mood:

```text
type(optional-scope): short description
```

```text
feat(cli): check the setup a run needs with chartula doctor
fix(config): refuse chartula.yaml and chartula.yml side by side
docs: add a getting-started guide built around chartula doctor
```

A breaking change adds `!` after the type, as in `feat!: keep pull request descriptions out of changelog.json`.

### Types

The type decides the category Chartula gives the change, and with this repository's configuration whether it appears in the release notes:

| Type | Meaning | In the release notes |
| --- | --- | --- |
| `feat` | New functionality | yes, as added |
| `fix` | A bug fix | yes, as fixed |
| `perf` | A performance improvement | yes, as changed |
| `refactor` | A structural change with no change in behavior | only with the `visibility:user-facing` label |
| `docs` | Documentation only | only with the `visibility:user-facing` label |
| `test`, `build`, `ci`, `chore`, `style` | Tests, build, CI, setup and dependencies | no, dropped as internal |
| `revert` | Reverts an earlier change | yes, unless the reverted change is in the same release |

A change marked breaking always appears, whatever its type.
[What goes into a release](docs/what-goes-into-a-release.md) has the full rules.

### Scopes

A scope is optional, and names the folder the change lives in: `facts`, `curation`, `filtering`, `labels`, `categorization`, `generation`, `rendering`, `prompting`, `formatting`, `faithfulness`, `review`, `serialization`, `releases`, `history`, `llm`, `observability`.
Outside the domain, `cli`, `config`, `output`, `ci` and `repo` are in use.

Use one when it helps a reader find the change; when a change spans several folders, drop the scope or split the change.

---

## The pull request

The description is read twice: by the reviewer now, and by the model when the release notes are written.
The merged pull requests follow one shape, and [the template](.github/PULL_REQUEST_TEMPLATE.md) starts you off with it:

1. **One line on what it follows:** `Closes #123` when it resolves an issue, or `Follows #120, which ...` when it continues earlier work.
2. **Bullets, one per decision, each with its reason.** The first says why the change is needed, as a user or contributor meets the problem. The others name what was decided and why, including what was left out on purpose.
3. **A bold verification section,** such as `**Verified:**` or `**Reproduced before the fix:**`, with how you checked it: a reproduction before the change, the run or test after it, and output where it helps.
4. **A last line `Tests:`** naming the tests added or changed, or `Tests: none, documentation only.`

```markdown
Closes #300

- **Why:** the CLI checked only the first word, so `generate --help` started a run and `--nopublish` published.
- **Checked before anything starts:** `CommandLineArguments.Check` runs before configuration, git or GitHub, so a typo costs nothing.

**Reproduced and verified end to end** with the built binary in a scratch clone, GitHub pointed at a local stub:

    $ chartula generate --nopublish
    Unknown option '--nopublish' for generate. Did you mean --no-publish?

Tests: `CommandLineArgumentsTests` (each refusal with its exact message), `ArgumentCheckTests` through the built CLI.
```

[#301](https://github.com/goldbarth/chartula/pull/301) is that pull request in full.

Before you open it:

- The solution builds without warnings, the tests pass, and the formatting check is clean.
- The change is focused: one concern per pull request where possible.
- No debug code, commented-out code, unresolved `TODO`, secret or API key is left in it.
- A change to behavior a user meets updates the page that describes it.

Do not worry about getting everything perfect.
Open the pull request, and we will work through the rest together in review.

---

## Writing documentation

The documentation follows these rules, so its pages read alike and stay correct:

- English, one sentence per line, one thought per sentence.
- Name who or what acts; no passive voice that hides the actor.
- Every decision carries its reason, and every instruction carries an example.
- A technical rule ends with one plain sentence on what it means for the reader ("so a release merged with merge commits costs more requests than the same release squashed").
- Describe the current behavior, not how it came to be.
- A term from the [glossary](docs/glossary.md) links to it on its first use in a user page.
- Every fact has one home; other pages link to it instead of restating it.
- No link to a private repository or unpublished material.
- A new page joins the documentation index in the README, and a renamed or moved section updates every link to it, in code, messages and scripts too.
- Messages, examples and output blocks come from a real run, not from memory.

---

## Questions

Unsure about anything?
[Ask in Discussions](https://github.com/goldbarth/chartula/discussions/categories/q-a) or open an issue - no question is too small, and asking early is always welcome.
