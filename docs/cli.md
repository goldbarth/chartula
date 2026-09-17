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
  --no-publish   Write changelog.json and CHANGELOG.md, but publish no release notes.
  --audience <a> Render only this audience: technical, customer or product.
                 Repeat it, or separate them with commas. All three by
                 default. An output whose audience was not rendered is not
                 written.
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
- **GitHub release notes** - the technical rendering, published to the release for `<release-tag>`.

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

Renders only the named audiences instead of all three.
Valid values are `technical`, `customer` and `product`, case-insensitive; a misspelled name fails the run with `Unknown audience '<name>'.` rather than quietly producing nothing.
Repeat the flag or separate values with a comma - both forms are accepted and can be mixed.

Each audience is its own rephrasing call and its own faithfulness check, so a run that asks for one audience pays for one.
An output whose audience was not rendered is not written: no `CHANGELOG.md` without `technical`, no `release-<tag>.md` without `customer`.
The run's summary lists the skipped outputs next to the written ones.

With no `--audience` given, all three render - that is what a real release wants.

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
A token raises the limit to 5000 an hour:

```console
$ export GITHUB_TOKEN=$(gh auth token)
```

The variable names above are the defaults; both can be renamed in `chartula.yaml` - see [Configuration](configuration.md).

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
