# GitHub

Chartula reads the pull requests behind a release from GitHub or GitHub Enterprise, and writes the release notes back there.
It supports no other host.

## What a run reads and writes

| Command | Reads from GitHub | Writes to GitHub |
| --- | --- | --- |
| `chartula preview` | the pull request behind each commit | nothing |
| `chartula generate --no-publish` | the pull request behind each commit | nothing |
| `chartula generate` | the pull request behind each commit, and the release for the tag | the release notes for the tag |

Everything else a run produces, such as `CHANGELOG.md` and `changelog.json`, stays on your machine until you commit or upload it.

## The token

Chartula reads the token from `GITHUB_TOKEN`.
Use a [fine-grained personal access token](https://github.com/settings/personal-access-tokens/new) scoped to the one repository you write changelogs for, with these permissions:

| Permission | `preview`, `generate --no-publish` | `generate` |
| --- | --- | --- |
| Contents | Read-only | Read and write |
| Pull requests | Read-only | Read-only |
| Metadata | Read-only | Read-only |

Contents covers the commits Chartula asks about and the release it writes.
Pull requests covers the pull requests themselves.
GitHub selects Metadata for every fine-grained token on its own.

A read-only token cannot change anything in the repository, so it is the safer choice until you publish with `generate`.

```console
$ export GITHUB_TOKEN=<your fine-grained token>
```

`export GITHUB_TOKEN=$(gh auth token)` works too, but hands Chartula the GitHub CLI's own token.
That token typically has `repo` scope, which is read and write access to every repository you can reach, so keep it for a quick local try and use a fine-grained token on a machine that runs Chartula regularly.

When `GITHUB_TOKEN` already holds a token for another tool, name another variable, in the environment only:

```console
$ export Chartula__GitHub__TokenEnvironmentVariable=CHARTULA_GITHUB_TOKEN
$ export CHARTULA_GITHUB_TOKEN=<your fine-grained token>
```

### Without a token

A run starts without a token and prints a warning, because a small release of a public repository fits the unauthenticated rate limit.

A private repository cannot be read without one.
The run stops at its first request and says so:

```console
$ chartula preview
Error: GitHub cannot read owner/name (404 Not Found).
  The request carried no token: GITHUB_TOKEN is not set.
  Likely causes:
  - --repo is misspelled
  - owner/name is private, and reading it needs a token in GITHUB_TOKEN
```

GitHub answers `404` for a private repository it will not show you, so the message names both a typo and the missing token.
A token that cannot read the repository gets a similar message, naming the token and, when GitHub says which, the missing permission.

## The rate limit

GitHub allows 60 API requests an hour per IP address without a token, shared with every other unauthenticated request from that address.
A token raises the limit to 5000 an hour.

A run spends one request per commit in the range, not per pull request, because it asks GitHub which pull request each commit belongs to.
A pull request merged with a merge commit costs one request for the merge commit and one for each commit on its branch, so a release merged with merge commits costs more requests than the same release squashed.
Ten squash-merged pull requests take ten requests; ten pull requests of five commits each, merged with merge commits, take sixty.
`generate` adds a few requests when it writes the release notes.

When the limit runs out, the run stops partway through and names it:

```console
$ chartula preview
Error: GitHub's rate limit is spent (403 Forbidden). A token in GITHUB_TOKEN raises it; see the warning at the start of the run.
```

## GitHub Enterprise

Point Chartula at your server's API, in the environment only:

```console
$ export Chartula__GitHub__ApiBaseUrl=https://github.example.com/api/v3/
$ chartula preview
...
GitHub: https://github.example.com/api/v3/, token from GITHUB_TOKEN
```

Keep the trailing slash.
Chartula appends each request's path to this URL, and without the slash the last segment is replaced, so requests go to `/api/repos/...` instead of `/api/v3/repos/...` and fail with `404`.

The header line every run prints names the API URL and the token variable in effect, so a setting inherited from a shell profile or a CI runner shows before the first request.

## What `generate` writes to GitHub

`generate` writes the technical rendering as the release notes for the tag.

- **A tag without a release** gets a new draft release.
  A draft is visible only to people with write access, so nothing goes public until someone publishes it.
  The run marks its link with `(draft)`.
- **A tag with a release** keeps it as it is, and only the notes are replaced.
  A draft stays a draft, and a published release stays published, so its readers see the new notes at once ([#249](https://github.com/goldbarth/chartula/issues/249)).
- **A re-run** finds the draft of the earlier run and replaces its notes, instead of adding a second draft.

Publish a draft after reading it, on the release page on GitHub or with the GitHub CLI:

```console
$ gh release edit v1.2.0 --draft=false
```

`generate` needs Contents read and write only for this last step, and it checks that permission before the first model call.
The check asks GitHub to draft notes for the tag, a request that needs the same permission and stores nothing.
A read-only token, or no token, stops the run there and exits with `1`:

```text
Error: GitHub does not let this run publish the release notes for v1.2.0 to owner/name (403 Forbidden).
  The request carried the token from GITHUB_TOKEN.
  Publishing needs a token with Contents read and write on owner/name.
  The run stopped before any model call. --no-publish writes the files without publishing them.
```

So a token that cannot publish costs one GitHub request and no model call.
Run `generate --no-publish` with a read-only token, so the run writes the files and leaves the release alone.
