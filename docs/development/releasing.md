# Releasing

How a maintainer makes a Chartula release, from the version bump to the committed changelog.
What the version number means is in [Versioning](../versioning.md).

A release ships when there is something worth giving to users, a feature or a fix, not on a schedule.
Chartula writes its own release notes and changelog, so every release is also a run of Chartula on its own repository.

## 1. Set the version

`<Version>` in `src/Chartula.Cli/Chartula.Cli.csproj` is the only place the version is written.
The binary, `chartula --version`, the name it sends to GitHub and `toolVersion` in every file it writes all read it from there.

Change it to the next version in a pull request of its own, like any other change:

```xml
<Version>0.1.0-preview.4</Version>
```

Title the pull request `build: bump version to 0.1.0-preview.4`.
The `build` type is filtered out as internal, so the bump never appears in the release notes it produces.

## 2. Tag the merge commit

After the pull request is merged, tag its merge commit on `main` and push the tag:

```console
$ git switch main && git pull
$ git tag v0.1.0-preview.4
$ git push origin v0.1.0-preview.4
```

The tag is the version with a `v` in front.

## 3. Let the release workflow build the draft

The pushed tag starts `.github/workflows/release.yml`, which:

1. refuses a tag that does not match `<Version>` in the csproj, or that points at a commit not on `main`, so only reviewed code ships with the version it reports,
2. runs CI on the tagged commit,
3. builds each binary on its own platform and smoke-tests it there, the Linux ones in a Debian slim or an Alpine container that has none of the runner's extra libraries,
4. creates a draft release for the tag with the binaries, `SHA256SUMS` and a build provenance attestation per binary.

The workflow starts only for tags that match `v*-preview*`.
So a `v1.0.0` tag builds nothing until the trigger in `release.yml` is widened, which the 1.0.0 release has to do first.

[Install](../install.md#platforms) lists the binaries a release carries.

## 4. Generate the release notes

Run Chartula on its own repository, from a checkout of `main` with its full history and tags.
Use a binary of the tagged commit, downloaded from the draft or [built from the tag](../install.md#build-from-source), so the run's `toolVersion` names the release.


```console
$ git fetch --tags
$ export Chartula__Llm__Provider=openai-compatible
$ export Chartula__Llm__BaseUrl=https://api.openai.com/v1
$ export Chartula__Llm__Model=gpt-6.1-sol
$ export Chartula__Llm__Thinking=low
$ export OPENAI_API_KEY=<key>
$ export GITHUB_TOKEN=<token with Contents read and write on goldbarth/chartula>
$ chartula preview --tag v0.1.0-preview.4
$ chartula generate --tag v0.1.0-preview.4
```

These are the settings `0.1.0-preview.4` was generated with.
`0.1.0-preview.2` and `0.1.0-preview.3` used `gpt-6-sol` with thinking `disabled`; `gpt-6.1-sol` refuses `disabled`, so `low` is the nearest setting it takes.
They override the model that `chartula.yaml` pins, which the evaluation's measurements refer to; the note at the top of `CHANGELOG.md` records which settings each release used.
Whatever settings you choose, write them into that note in step 6.

`preview` first shows the facts for free, so a wrong category or a missing change is fixed in the pull request title before any token is spent.
`generate` writes the notes into the draft the workflow created, and writes `CHANGELOG.md`, `release-<tag>.md`, `changelog.json` and a run record into the checkout.
Keep the run summary and the `Recorded in` line: the pull request of step 6 quotes them.

## 5. Read and publish the release

Read the flags `generate` printed first, because nothing in the draft marks a flagged entry.
Then read the draft on GitHub, and publish it there.

The notes are published as Chartula wrote them.
What a run gets wrong is opened as an issue against Chartula rather than edited away, so the release notes stay a record of what the tool does.

Publishing starts `.github/workflows/install.yml`, which installs the new release with the install scripts, in fresh containers of every mainstream Linux (glibc and musl, x64 and arm64), on macOS and on Windows, and runs `chartula generate --no-publish` with it.
So a release that cannot be installed or does not run shows up within minutes of publishing.

## 6. Commit the changelog

`generate` prepended a section for the release to `CHANGELOG.md`.
Commit it in a pull request, as written, with one edit by hand: a line for the release in the note at the top of the file, naming the settings of the run:

```markdown
> - `0.1.0-preview.4`: provider `openai-compatible`, model `gpt-6.1-sol`, thinking `low`.
```

Add an option to the line when the run used one that changes the output, such as `--audience technical`.
So a reader can tell which model and settings wrote each section, and compare them.

Commit only `CHANGELOG.md`.
`changelog.json`, `release-<tag>.md` and `chartula-runs/` are ignored in this repository, because `changelog.json` holds only the last release and the run record is a local log.

Title the pull request `docs: add the changelog Chartula generated for 0.1.0-preview.4`, and give its description the run's facts:

- the command and the settings,
- the binary's `toolVersion`, which is the tagged commit,
- the pull requests, facts, tokens and time from the run summary,
- the flags per audience.

[#280](https://github.com/goldbarth/chartula/pull/280) is the pull request for `0.1.0-preview.3`.
