# The `chartula` CLI

Chartula is controlled entirely through the `chartula` command.
There is no daemon, no interactive shell and no menu - every run is one command that reads pull requests and produces output, then exits.

This page lists every command and option the CLI accepts today.
`chartula --help` prints a short version of the same thing from the binary itself.

## Command overview

```console
$ chartula --help
Chartula - multi-audience, grounded changelog generator.

Usage:
  chartula preview  [options]   Show what would be produced (dry run).
  chartula generate [options]   Produce and write the outputs.

Run it from a checkout of the repository the release belongs to.

Options:
  --tag <tag>    The release tag. Default: the nearest tag reachable from HEAD.
  --repo <o/n>   The GitHub repository, as owner/name. Default: read from
                 the 'origin' remote.
  --since <ref>  Start the release after this tag or commit instead of the
                 previous tag. A first tag needs this or --whole-history.
  --whole-history
                 Render a first tag's whole history, e.g. for a project
                 whose history is the release.
  --no-publish   Write changelog.json and CHANGELOG.md, but publish no release notes.
  --audience <a> Render only this audience: technical, customer or product.
                 Repeat it, or separate them with commas. Default:
                 technical and customer; product renders only when named.
                 An output whose audience was not rendered is not written.
```

`-h`, `--help` and `help` all print this text, and so does running `chartula` with no arguments at all.
An unrecognized first word is not silently ignored - it prints `Unknown command '<word>'.`, the same usage text, and exits with status 1.

Two commands exist: `preview` and `generate`.
Both run the identical pipeline against the same facts; they differ only in what happens at the very end.

## `chartula preview`

```console
$ chartula preview [--tag <release-tag>] [--repo <owner/name>]
```

Runs the full pipeline - curation, filtering, labeling, categorization, then one rephrasing and one faithfulness check per audience - and prints the result to stdout.
Nothing is written to disk and nothing is published.
Use it to see what a release would look like, to check token cost before committing to a real run, or to try a configuration change without touching `CHANGELOG.md`.

## `chartula generate`

```console
$ chartula generate [--tag <release-tag>] [--repo <owner/name>]
```

Runs the same pipeline as `preview` and then writes the outputs:

- **`CHANGELOG.md`** - the technical rendering, prepended to whatever is already there.
- **`release-<tag>.md`** - the customer rendering as a standalone page, with YAML front matter (title, date, one-sentence description) ahead of the entries.
- **`changelog.json`** - every audience's text plus the fact base behind it, in the [documented, stable format](changelog-json.md).
- **GitHub release notes** - the technical rendering, as a **draft** release for `<release-tag>` that you publish on GitHub after reading it. A release that already exists for the tag keeps its state: a draft stays a draft, a published release stays published, and only its notes are replaced. The output marks a draft with `(draft)` after its link.

A field with no source behind it is left out rather than filled in - no description when the facts do not support one, no date when the tag has none.

### `--no-publish`

```console
$ chartula generate --tag v1.2.0 --repo owner/name --no-publish
```

Writes `CHANGELOG.md`, `release-<tag>.md` and `changelog.json`, and leaves the GitHub release notes alone.
Use it to keep the record of a run without announcing a release - measuring a prompt change, say, or generating a changelog for a tag that was never shipped.
The run's summary lists this under "Skipped (--no-publish)" so a deliberately unpublished run still reads as complete rather than as a partial failure.

### `--audience <a>`

```console
$ chartula generate --tag v1.2.0 --repo owner/name --audience customer
$ chartula generate --tag v1.2.0 --repo owner/name --audience technical,product
$ chartula generate --tag v1.2.0 --repo owner/name --audience technical --audience customer
```

Renders only the named audiences instead of the default two.
Valid values are `technical`, `customer` and `product`, case-insensitive; a misspelled name fails the run with `Unknown audience '<name>'.` rather than quietly producing nothing.
Repeat the flag or separate values with a comma - both forms are accepted and can be mixed.

Each audience is its own rephrasing call and its own faithfulness check, so a run that asks for one audience pays for one.
An output whose audience was not rendered is not written: no `CHANGELOG.md` without `technical`, no `release-<tag>.md` without `customer`.
The run's summary lists the skipped outputs next to the written ones.

With no `--audience` given, `technical` and `customer` render: the two with somewhere to go, `CHANGELOG.md` and the release notes, and `release-<tag>.md`.
`product` renders only when named, since it has no output file of its own and no evaluation yet, and would otherwise cost a third of every run.
All three are `--audience technical,customer,product`.

## Release and repository

A run starts from a checkout of the repository the release belongs to, because the commit range is read with `git` from the current directory.
That checkout already knows the release and the repository, so both options default from it and only need to be passed to choose something else.

| Option | Value | Default |
| --- | --- | --- |
| `--tag` | `<release-tag>` | The nearest tag reachable from `HEAD` (`git describe --tags --abbrev=0`). |
| `--repo` | `<owner/name>` | Owner and name from the `origin` remote, in the `https://`, `ssh://` or `git@host:` form. |

A value that was not passed is announced on stderr before the run starts, so `generate` never publishes to a release you did not see:

```console
$ cd my-repo
$ chartula preview
Using tag v1.3.0, the nearest tag reachable from HEAD. Pass --tag to choose another.
Using repository owner/name, from the 'origin' remote. Pass --repo to choose another.
```

When a default cannot be read, the run fails before any network call and names the option to pass:

```console
$ chartula preview
No --tag given, and no tag is reachable from HEAD in '/work/my-repo'. Pass --tag <release-tag>, or run from a checkout of the repository with its tags fetched (git fetch --tags).

$ chartula preview --repo name-only
Invalid option --repo 'name-only'. Expected <owner/name>.
```

### Where a release starts

A release is everything after the previous tag up to the release tag.
A first tag has no previous tag, so its range is the whole history - and rendered as it is, that reads as a development log: intermediate states stand next to the changes that replaced them.
Where a release starts is a decision about facts, so Chartula does not guess it.
A first tag stops the run before any GitHub request or model call, and asks:

```console
$ chartula preview --tag v0.1.0
Error: v0.1.0 is the first tag, so its range is the whole history (69 commits).
  Rendered as it is, that reads as a development log rather than a release.
  --since <ref>     start the release after a tag or commit (e.g. the last state you shipped)
  --whole-history   render all of it, e.g. for a project whose history is the release
```

| Option | Value | Effect |
| --- | --- | --- |
| `--since` | `<tag-or-commit>` | The release starts after this ref instead of the previous tag. It has to be an ancestor of the release tag. Works on any tag, not only the first. |
| `--whole-history` | - | Render a range that spans all history. For a project whose history is its first release. |

Passing both is refused, since they answer the same question two ways.
Adopting Chartula on a project with a long history usually means `--since` on the first run, pointing at the last state that was already shipped; every later tag starts after its predecessor on its own.

Within any range, a change that a later change in the same range reverted or replaced is still rendered as an entry of its own.
Chartula cannot tell that one pull request undoes another without reading their meaning, which would put a fact decision into the model.

## Environment

Two environment variables carry credentials, and neither is ever read from `chartula.yaml`:

| Variable | Used for |
| --- | --- |
| `ANTHROPIC_API_KEY` | The model that rephrases the facts. |
| `GITHUB_TOKEN` | Reading pull requests and writing release notes. |

A run without `ANTHROPIC_API_KEY` is refused before it reads anything, naming the variable, because every audience would fail on it.
The `openai-compatible` provider is exempt: a local server needs no key.

```console
$ chartula preview
Configuration error: No Anthropic API key found in ANTHROPIC_API_KEY. Set one with: export ANTHROPIC_API_KEY=<your key> (create one at https://console.anthropic.com/settings/keys). For an endpoint that needs no key, set llm.provider to openai-compatible.
```

A run starts without `GITHUB_TOKEN` and prints a warning to stderr rather than refusing - a small release fits inside GitHub's unauthenticated budget of 60 requests an hour per IP address, and a run spends roughly one request per pull request.
Beyond that the run fails partway through with a 403 that names a commit rather than the cause.
A token raises the limit to 5000 an hour.

### A GitHub token

Use a [fine-grained personal access token](https://github.com/settings/personal-access-tokens/new) scoped to the one repository you generate changelogs for.
Chartula makes two kinds of request, and the token needs exactly the permissions they take:

| Permission | Access | Used for |
| --- | --- | --- |
| Contents | Read-only | `preview` and `generate --no-publish`: finding the pull requests behind each commit. |
| Contents | Read and write | `generate`: creating and updating the draft release. |
| Pull requests | Read-only | Reading the pull requests themselves. |
| Metadata | Read-only | Required by GitHub for every fine-grained token; selected automatically. |

A token for `preview` only never needs write access, so a read-only token is the safer default until you publish.

```console
$ export GITHUB_TOKEN=<your fine-grained token>
```

`export GITHUB_TOKEN=$(gh auth token)` works too, but hands Chartula the GitHub CLI's own OAuth token: typically `repo` scope, which is read and write access to every repository you can reach.
It is a shortcut for a quick local try, not for a machine that runs Chartula regularly.

The variable names above are the defaults; both can be renamed, in the environment only - see [Environment-only settings](configuration.md#environment-only-settings).

## Configuration file

`chartula.yaml` in the repository root refines the default behavior of both commands - which model is used, which labels affect curation, which categories are excluded, and more.
It is never required; every command above works with no file present.
See [Configuration](configuration.md) for every key and its default.

## Exit status

`0` when every audience the run asked for rendered.
`1` on a usage error (missing option, unknown command, unknown audience), a run-time failure (bad configuration, a pipeline error), or a run in which any requested audience failed.

An audience that failed does not hold back the ones that rendered: `generate` still writes their outputs and says `<n> of <m> audiences failed.`
When no audience rendered, nothing is written at all, so an earlier run's `changelog.json` is not replaced by one without renderings.
Errors are written to stderr when they are about the invocation itself, and to stdout as `Error: <message>` when the pipeline started running and then failed.

## After a run

Every run - `preview` or `generate` - ends with a report of what it did and what it cost in tokens.
See [Run metrics](run-metrics.md) for how to read it.
