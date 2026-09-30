# The `chartula` CLI

This page is the reference for the `chartula` command: every command, every option with its default, the environment it reads, what it prints where, and its exit status.
`chartula --help` prints the commands and options from the binary itself, so it always matches the version you run.
[Getting started](getting-started.md) walks through a first run, and [Troubleshooting](troubleshooting.md) has the messages a run stops with.

Every run is one command that reads a release, prints or writes its result, and exits.
There is no daemon and no interactive shell.

## Commands

| Command | What it does | Model calls | Writes files | Publishes |
| --- | --- | --- | --- | --- |
| `chartula preview` | Establishes the facts of a release and shows what `generate` would send. | none | no | no |
| `chartula generate` | Establishes the same facts, has the model write each audience, checks the texts, and writes the outputs. | one per audience, and one check per rendering | yes | a draft of the release notes |
| `chartula generate --no-publish` | The same, without the release notes on GitHub. | as `generate` | yes | no |
| `chartula doctor` | Checks everything a run needs. | one short call per model | no | no |
| `chartula --version` | Prints the version and the commit it was built from. | none | no | no |
| `chartula --help` | Prints the commands and options. | none | no | no |

Run `preview`, `generate` and `doctor` from a checkout of the repository the release belongs to, because the range of the release is read with `git` from the current directory.

`-h`, `--help` and `help` print the help text as the first word, and so does `chartula` without arguments.
`-h` and `--help` print it after a command too, such as `chartula generate --help`, and start nothing.
An unknown first word prints `Unknown command '<word>'.` and the help text, and exits with 1.

## Options

| Option | Value | Default | Commands |
| --- | --- | --- | --- |
| `--tag` | a tag | The nearest tag reachable from `HEAD` (`git describe --tags --abbrev=0`). | `preview`, `generate`, `doctor` |
| `--repo` | `owner/name` | Owner and name from the `origin` remote, in the `https://`, `ssh://` or `git@host:` form. | `preview`, `generate`, `doctor` |
| `--since` | a tag or commit | After the previous tag, or from the first commit for a first tag. | `preview`, `generate` |
| `--yes` | none | Off: a first tag or a large range is asked about in a terminal, and stops a run without one. | `preview`, `generate` |
| `--audience` | `technical`, `customer`, `product` | `technical` and `customer` | `preview`, `generate` |
| `--no-publish` | none | Off: `generate` publishes the release notes. | `generate` |

Options follow the command in any order, and a value follows its option after a space.
`--audience` may be repeated; every other option may be given once.

Chartula checks every option before it reads configuration, git or GitHub, and stops on one it cannot read as meant:

```console
$ chartula generate --nopublish
Unknown option '--nopublish' for generate. Did you mean --no-publish?
chartula --help lists every command and its options.
```

| Command line | Message |
| --- | --- |
| an option of another command | `--no-publish is an option of generate, not of preview.` |
| an option without its value, at the end or before another option | `--tag needs a value: --tag <release-tag>.` |
| an option given twice | `--tag is given twice. Pass it once.` |
| `=` instead of a space | `Write --tag v1.3.0, with a space, not --tag=v1.3.0.` |
| a word that is no option | `Unexpected argument 'v1.3.0'. preview takes options only, such as --tag <release-tag>.` |

Each message is followed by `chartula --help lists every command and its options.`, and the run exits with 1.
So a typo never runs for another release, and never publishes by accident.

### `--tag` and `--repo`

A value you did not pass is announced on stderr before the run starts, so `generate` never publishes to a release you did not see:

```console
$ chartula preview
Using tag v1.3.0, the nearest tag reachable from HEAD. Pass --tag to choose another.
Using repository owner/name, from the 'origin' remote. Pass --repo to choose another.
```

When a default cannot be read, the run stops before any network call and names the option to pass:

```console
$ chartula preview
No --tag given, and no tag is reachable from HEAD in '/work/my-repo'. Pass --tag <release-tag>, or run from a checkout of the repository with its tags fetched (git fetch --tags).

$ chartula preview --repo name-only
Invalid option --repo 'name-only'. Expected <owner/name>.
```

### `--since` and `--yes`

`--since` starts the release after the tag or commit you name, instead of after the previous tag.
It has to be an ancestor of the release tag.

A first tag, or a range with more commits than `range.confirmAboveCommits`, is confirmed before any GitHub request or model call.
In a terminal the run asks; `--yes` confirms up front, for a run without one, such as a CI job.
So a CI job stops on its first tag, with nothing spent, until you pass `--yes` or `--since`.
[What goes into a release](what-goes-into-a-release.md#1-the-range) explains the range, the first tag, the confirmation and a shallow clone.

### `--audience`

```console
$ chartula generate --audience customer
$ chartula generate --audience technical,product
$ chartula generate --audience technical --audience customer
```

Renders only the named audiences instead of the default two.
The names are case-insensitive, and both forms can be mixed.
A name that is not an audience stops the run before it reads anything:

```console
$ chartula generate --audience developers
Unknown audience 'developers'. There are three: technical, customer, product.
```

Each audience is one rendering call and one thorough check, so a run for one audience pays for one.
An output whose audience was not rendered is not written: no `CHANGELOG.md` without `technical`, no `release-<tag>.md` without `customer`.
`product` renders only when named, because it has no output file of its own and would otherwise cost a third of every run.

### `--no-publish`

`generate --no-publish` writes every file `generate` writes and leaves the GitHub release notes alone.
The summary lists the notes under `Skipped (--no-publish)`, so a run you kept local still reads as complete.
Use it to keep the record of a run without announcing a release, for example when you compare a prompt change or a model.
`preview` publishes nothing either way.

## `chartula preview`

Reads the range and its pull requests, decides every fact the way `generate` does, and stops before the model:

```console
$ chartula preview --tag v0.1.0-preview.3
Preview of v0.1.0-preview.3 - no model call was made, and nothing was written or published.

Release: 7 commits, 7 pull requests, 6 facts (6 with a description, 15,083 characters)

Facts (6):
  #252     Fix: fix: keep the reason in each flag, and bound the customer outcome by the facts
           technical, customer
  #251     Feature: feat(observability): record the range a run read in its run record
           technical, customer
  ...
  #248     Documentation: docs: rewrite comments for readability
           in no rendering
  ...
Dropped (1):
  #253     build: bump version to 0.1.0-preview.3
           Internal is in filter.excludeCategories

generate would make 4 model calls:
  technical 1 rephrasing call, 14,484 characters of prompt, then 1 thorough check
  customer  1 rephrasing call, 18,549 characters of prompt, then 1 thorough check
  A thorough check sends the rendering along with the facts, so its size is known only once the rendering is.
```

Each fact names the audiences whose rendering carries it, and each dropped change names the setting that dropped it.
The prompt is counted in characters, as it would be sent; [Estimating the cost of a run](costs-and-checks.md#estimating-the-cost-of-a-run) turns them into tokens.

`preview` makes no model call, so it costs no tokens and needs no model key.
It spends one GitHub request per commit in the range, writes nothing and publishes nothing.
So you can repeat it until the facts are right, and pay only for the `generate` that follows.

## `chartula generate`

Establishes the same facts as `preview`, renders each audience with the model, checks the renderings, and writes the outputs:

- **`CHANGELOG.md`** - the technical rendering, prepended to whatever is already there.
- **`release-<tag>.md`** - the customer rendering as a standalone page, with YAML front matter (title, date, one-sentence description) ahead of the entries.
- **`changelog.json`** - every audience's text plus the facts behind it, without the pull request descriptions, in the [documented, stable format](changelog-json.md). It is meant to be published.
- **GitHub release notes** - the technical rendering, as a **draft** release for the tag that you publish on GitHub after reading it. A release that already exists for the tag keeps its state: a draft stays a draft, a published release stays published, and only its notes are replaced. The output marks a draft with `(draft)` after its link.
- **`chartula-runs/<time>-<tag>.json`** - the [run record](run-record.md): what the run cost, what it was made with, and the complete facts, descriptions included. It stays on your machine.

A field with no source behind it is left out rather than filled in: no description when the facts do not support one, no date when the tag has none.

A token that cannot publish stops the run before the first model call ([What `generate` writes to GitHub](github.md#what-generate-writes-to-github)).
Publishing is still the last step, so when GitHub refuses it for another reason, the files are already written.
The summary lists them under `Wrote:`, names the refusal under `Not published:`, and the run exits with 1.
A re-run replaces this release's entries in `CHANGELOG.md` and its draft rather than adding to them, but pays for the model calls again.

## `chartula doctor`

Checks everything a run needs, one line per check, and says whether a run would start:

```console
$ chartula doctor
Checking the setup for a run in /work/my-repo

  ok    git           /usr/bin/git
  ok    checkout      /work/my-repo, with its full history
  ok    tag           v1.3.0, the nearest tag reachable from HEAD. Pass --tag to choose another.
  ok    repository    owner/name, from the 'origin' remote. Pass --repo to choose another.
  ok    config        /work/my-repo/chartula.yaml
  ok    model         openai-compatible at http://localhost:11434/v1, model qwen3:14b-ctx24k, no key in OPENAI_API_KEY: a local server needs none
  ok    endpoint      qwen3:14b-ctx24k answered, 98 tokens
  ok    GitHub read   owner/name at https://api.github.com/, token from GITHUB_TOKEN
  warn  GitHub write  GitHub does not let this run publish the release notes for v1.3.0 to owner/name (403 Forbidden).
                      The request carried the token from GITHUB_TOKEN.
                      Publishing needs a token with Contents read and write on owner/name.
                      generate stops before its first model call. preview and generate --no-publish publish nothing and still run.
  warn  PR titles     24 of the last 30 merged pull requests carry a Conventional Commits prefix, such as feat: or fix:.
                      Without one, a change is Other, and internal work is not filtered out. For example: 'Update readme', 'Bump deps'
                      docs/writing-pull-requests.md shows what a prefix changes in the output.

A run would start. The warnings above do not stop it, but read them before generate.
```

Each check asks what a run asks, through the same code, so a failed check shows the message the run would print.

| Status | Meaning |
| --- | --- |
| `ok` | The check passed. |
| `warn` | A run starts, but something limits it: no GitHub token, a token that cannot publish, or pull request titles without a prefix. |
| `fail` | A run would stop there. |
| `skip` | The check needs an earlier one that failed, so one missing piece does not bury the report under follow-on errors. |

`doctor` writes no file, publishes nothing, and names variables, never their values.
The endpoint check is the one that costs: it asks each model a run uses for an answer of at most 16 tokens, about a hundred with the prompt, because only an answer proves the key, the model id and the endpoint together.
The GitHub checks cost three requests: the pull requests of the tag's commit, the check that the token may publish (the same one `generate` makes, which stores nothing), and the last 30 merged pull requests for the title check.
So a CI job can run `doctor` before `generate` for the price of a few tokens.

## `chartula --version`

```console
$ chartula --version
chartula 0.1.0-preview.3+f2b8f02e37f7c6bcd6ea725c39c576b800865ee6
```

It prints the version and the commit it was built from, the same text a run records as `toolVersion` in `changelog.json` and the run record.
So a bug report and a run file name the same build.

## While a run works

After the header, a run shows each step as it starts, with its count where it has one and the time it has taken so far:

```console
$ chartula generate --tag v1.3.0
...
Range:  the commits after v1.2.0, up to v1.3.0 (100 commits)
Reading pull requests   100/100 commits  4 s
Rendering technical                      12 s
Rendering customer                       14 s
Checking technical                       3 s
Checking customer                        5 s
```

In a terminal the current line updates in place, and its time keeps counting while a model call runs.
So a slow step still shows it is working, and a run that is about to finish is not aborted and paid for again.
The steps are the ones the run takes: one GitHub request per commit, then one rendering per audience, then the checks of each rendering; a `preview` shows the first step only.
There is no estimate of the time remaining, because how long a model call takes is not known before it returns.

Without a terminal on stderr, as in CI, each step is one plain line as it starts, `Reading pull requests (100 commits)` or `Rendering technical`, with no control characters, so a job log stays readable.
When a step fails, it is the last line shown, and the error follows below it.
[A slow run](troubleshooting.md#a-slow-run) shows how to read the steps and the summary when a run takes long.

## What goes to stdout and stderr

| Stream | What it carries |
| --- | --- |
| stdout | The result: the help text, the version, the `doctor` report, the `preview` report, and the `generate` report with its renderings, flags, written files and run summary. An error after the run started, as `Error: <message>`, and a range that was not confirmed, as `Stopped: <message>`. |
| stderr | Everything about the run itself: the announced `--tag` and `--repo` defaults, the header naming the model and GitHub endpoints, the warning without a GitHub token, the `Range:` line and its question, the progress lines, a usage error, and a `Configuration error:`. |

So `chartula generate > report.txt` keeps the report free of progress lines, and a CI log still shows where a run stood.
The question about a first tag or a large range is asked only when stdin is a terminal, and the progress lines update in place only when stderr is one.

## Environment

Three variables carry credentials, and none is ever read from `chartula.yaml`:

| Variable | Used for | Home |
| --- | --- | --- |
| `ANTHROPIC_API_KEY` | The model, with `llm.provider: anthropic`, the default. `generate` refuses to start without it. | [Providers](providers.md#anthropic) |
| `OPENAI_API_KEY` | The model, with `llm.provider: openai-compatible`, when the endpoint needs a key. A local server needs none. | [Providers](providers.md#what-each-provider-needs) |
| `GITHUB_TOKEN` | Reading pull requests and publishing the release notes. A run starts without one and warns. | [GitHub](github.md#the-token) |

`preview` makes no model call, so it runs without a model key.

Every setting of `chartula.yaml` can also be set as a `Chartula__<Section>__<Key>` variable, which wins over the file ([Settings as environment variables](configuration.md#settings-as-environment-variables)).
Four settings exist only as variables: the two endpoints and the names of the two credential variables ([Environment-only settings](configuration.md#environment-only-settings)).
The install scripts read `CHARTULA_VERSION`, `CHARTULA_INSTALL_DIR` and `CHARTULA_DOWNLOAD_URL`; the CLI does not ([Install](install.md#settings)).

## Configuration file

`chartula.yaml` in the directory the run starts in refines what every command does: the model, the labels, the filters, the categories and more.
No command needs it, and every default applies without it.
[Configuration](configuration.md) lists every key and its default.
A file that cannot be read as written stops the run with a configuration error naming the file, the line and the column ([A file that cannot be read as meant](configuration.md#a-file-that-cannot-be-read-as-meant)).

## Exit status

| Status | When |
| --- | --- |
| `0` | `preview` showed the release. `generate` rendered every audience it asked for and published, or skipped publishing with `--no-publish`. `doctor` found nothing that fails. `--help` and `--version`. |
| `1` | A usage error: an unknown command or option, an option of another command or without its value, an unknown audience, an invalid `--repo`. A configuration error. A tag or repository that cannot be read. A range that was not confirmed. An error once the run started. A `generate` in which any requested audience failed, or GitHub refused to publish. A `doctor` with a `fail` line. |

An audience that failed does not hold back the ones that rendered: `generate` still writes their outputs and says `<n> of <m> audiences failed.`
When no audience rendered, no output is written, so an earlier run's `changelog.json` is not replaced by one without renderings; only the [run record](run-record.md) is kept, since the tokens were spent.
So a CI job reads the exit status, and a person reads the report for what did render.
[A model call fails](troubleshooting.md#a-model-call-fails) shows the messages of a failed audience.

## After a run

Every `generate` run ends with a summary of what it did and what it cost in tokens, and names its run record below it.
[Reading the run summary](costs-and-checks.md#reading-the-run-summary) explains each line.
A `preview` spends no tokens, so it ends with what `generate` would send instead.
