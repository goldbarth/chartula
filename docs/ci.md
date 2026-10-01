# CI

A GitHub Actions job can write the release notes whenever you push a release tag.
The job below does that with a pinned Chartula version, and keeps the files it writes as an artifact.

## The job

Save it as `.github/workflows/release-notes.yml` and add your model key as a repository secret named `ANTHROPIC_API_KEY`:

```yaml
name: Release notes

on:
  push:
    tags: ['v*']

# The built-in token gets what `chartula generate` needs, and nothing more.
permissions:
  contents: write        # read the commits, and write the draft release
  pull-requests: read    # read the pull requests behind the commits

jobs:
  release-notes:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
        with:
          fetch-depth: 0   # the full history and all tags; a shallow clone is refused

      - name: Install Chartula
        env:
          CHARTULA_VERSION: v0.1.0-preview.3
        run: |
          curl -fsSL "https://raw.githubusercontent.com/goldbarth/chartula/$CHARTULA_VERSION/install.sh" | sh
          echo "$HOME/.local/bin" >> "$GITHUB_PATH"

      - name: Write the release notes
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          TAG: ${{ github.ref_name }}
          REPO: ${{ github.repository }}
        run: chartula generate --tag "$TAG" --repo "$REPO"

      - name: Keep the files
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@v7
        with:
          name: chartula-${{ github.ref_name }}
          path: |
            CHANGELOG.md
            release-*.md
            changelog.json
            chartula-runs/
```

Each step is there for a reason:

- **`fetch-depth: 0`** fetches the whole history and every tag.
  `actions/checkout` fetches one commit by default, and Chartula refuses such a shallow clone, because it cannot tell from it where the release starts.
- **A pinned version** installs the release you tested, from the install script of that same release, so a new Chartula release cannot change your notes unannounced.
  Raise `CHARTULA_VERSION` on purpose when you want a newer one; [Install](install.md#settings) describes the setting.
- **Secrets as environment variables** keep the key out of the workflow file and out of the log.
  The built-in `GITHUB_TOKEN` needs no secret of its own.
- **`permissions`** give the built-in token exactly what [GitHub](github.md#the-token) lists for `generate`.
  Without `contents: write`, the run writes its files and pays for its model calls, and then GitHub refuses to publish.
- **`--tag` and `--repo`** name the release and the repository explicitly, so the run does not depend on what the checkout's `HEAD` and remote happen to be.
  They pass through environment variables rather than straight into the command, so a tag name cannot inject shell code.
- **The artifact** keeps `CHANGELOG.md`, `release-<tag>.md`, `changelog.json` and the run record, which otherwise disappear with the runner.

A failed [audience](glossary.md#audience) or a refused publication ends the run with exit code `1`, so the job fails and GitHub notifies you.

A first tag, or a [range](glossary.md#range) with more commits than [`range.confirmAboveCommits`](configuration.md#range), is confirmed before it is read, and a job has no terminal to answer on.
So the job stops there with nothing spent and exit code `1`, and names the two ways on: `--since <ref>` to start later, or `--yes` to render the range as it is.
Run that one release by hand or with the option added once, rather than adding `--yes` to the job for good, which would let every later range through unasked.

## What happens to the draft

For a tag without a release, the job creates a [draft](glossary.md#draft) release with the technical [rendering](glossary.md#rendering) as its notes.
A draft is visible only to people with write access, so nothing goes public until someone reads it and publishes it, on the release page or with `gh release edit <tag> --draft=false`.

When another workflow of yours creates the release for the same tag, let it create a draft and run this job before anyone publishes.
A release that is already published keeps its state, and Chartula replaces its notes in public ([#249](https://github.com/goldbarth/chartula/issues/249)); [What `generate` writes to GitHub](github.md#what-generate-writes-to-github) has the details.

`CHANGELOG.md` and the customer page are not committed by the job.
The tag's checkout is not a branch, so download them from the artifact and commit them through a pull request of your own.
To write the files and leave GitHub alone, run `chartula generate --no-publish` and drop `contents: write` to `contents: read`.

## Another provider

The job reads `chartula.yaml` from your repository, so the provider and model come from there as they do on your machine.
Only the endpoint and the key come from the job, as environment variables.
For OpenAI, for example, with `llm.provider: openai-compatible` and `llm.model` in `chartula.yaml` and a secret named `OPENAI_API_KEY`:

```yaml
      - name: Write the release notes
        env:
          Chartula__Llm__BaseUrl: https://api.openai.com/v1
          OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          TAG: ${{ github.ref_name }}
          REPO: ${{ github.repository }}
        run: chartula generate --tag "$TAG" --repo "$REPO"
```

[Providers](providers.md) has the settings for every provider.

## In an Alpine container

In an Alpine container, install git before the checkout, because `actions/checkout` downloads the files without their history when git is missing.
The musl binary also needs `libstdc++`, and the image lacks `curl` and the CA certificates:

```yaml
jobs:
  release-notes:
    runs-on: ubuntu-latest
    container: alpine:latest
    steps:
      - name: Install what Chartula needs
        run: apk add --no-cache git ca-certificates libstdc++ curl

      - uses: actions/checkout@v7
        with:
          fetch-depth: 0

      # The remaining steps are the same as in the job above.
```

The install script picks the musl binary on its own.
