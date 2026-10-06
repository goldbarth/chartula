# Getting started

This page takes you from nothing installed to a published release [draft](glossary.md#draft), one step after the other.
Each step says what to run, what you should see, and where to look when you see something else.

You need a GitHub repository with at least one tag and some merged pull requests, and a checkout of it on your machine.

## 1. Install Chartula

On Linux or macOS:

```bash
curl -fsSL https://raw.githubusercontent.com/goldbarth/chartula/main/install.sh | sh
```

On Windows, in PowerShell:

```powershell
irm https://raw.githubusercontent.com/goldbarth/chartula/main/install.ps1 | iex
```

The script downloads the binary for your platform, checks it against the release's checksums, and starts it once to make sure it runs.
[Install](install.md) covers a pinned version, a download by hand, and building from source.

## 2. Set a model key and a GitHub token

Chartula reads both from the environment of the terminal it runs in, never from a file:

```bash
export ANTHROPIC_API_KEY=<your key>
export GITHUB_TOKEN=<your fine-grained token>
```

Anthropic is the default provider; create a key at [console.anthropic.com](https://console.anthropic.com/settings/keys).
For OpenAI, another hosted endpoint or a model on your own machine, follow the recipe in [Providers](providers.md) instead.

Create the GitHub token at [github.com/settings/personal-access-tokens/new](https://github.com/settings/personal-access-tokens/new), scoped to your repository, with the permissions [GitHub](github.md#the-token) lists for `generate`.
`generate` needs write access to create the release draft.
A read-only token takes you through step 5, and through `generate --no-publish`, which writes the files and publishes nothing.

## 3. Check the setup with `chartula doctor`

Run it from the checkout of your repository:

```bash
cd my-repo
chartula doctor
```

`doctor` checks everything a run needs, one line per check, and ends with whether a run would start.
It writes nothing and publishes nothing.
It costs three GitHub requests and a model call of a few tokens.
The call is there because only an answer proves that the key, the model and the endpoint fit together.

A first run often looks like this, here with the model key not yet set:

```console
$ chartula doctor
Checking the setup for a run in /work/my-repo

  ok    git           /usr/bin/git
  ok    checkout      /work/my-repo, with its full history
  ok    tag           v1.3.0, the nearest tag reachable from HEAD. Pass --tag to choose another.
  ok    repository    owner/name, from the 'origin' remote. Pass --repo to choose another.
  ok    config        no chartula.yaml in /work/my-repo, so the defaults apply
                      To change them, copy https://github.com/goldbarth/chartula/blob/v0.1.0-preview.3/chartula.example.yaml to chartula.yaml.
  fail  model         anthropic at its default endpoint, model claude-sonnet-5
                      No Anthropic API key found in ANTHROPIC_API_KEY. Set one with: export ANTHROPIC_API_KEY=<your key> (create one at https://console.anthropic.com/settings/keys). For an endpoint that needs no key, set llm.provider to openai-compatible.
  skip  endpoint      not checked: needs the model settings and a key
  ok    GitHub read   owner/name at https://api.github.com/, token from GITHUB_TOKEN
  ok    GitHub write  the token can publish release notes to owner/name, which generate needs
  ok    PR titles     all of the last 30 merged pull requests carry a Conventional Commits prefix

A run would stop. Fix what failed above, then run chartula doctor again.
```

The tag is the release Chartula works on: the nearest tag reachable from `HEAD`, unless you pass `--tag`.
The repository comes from the `origin` remote, unless you pass `--repo`.
Check both before you go on, because `generate` publishes to exactly this release.

## 4. Act on what it reports

Every `fail` and `warn` line names its cause and the fix.
Fix each `fail`, then run `chartula doctor` again, until it ends with `A run would start.`

- **`fail`** stops a run, so it has to go.
- **`warn`** lets a run start, but limits it: no GitHub token, a token that may not publish, or pull request titles without a Conventional Commits prefix.
- **`skip`** is a check that waits for an earlier one; it runs once that one passes.

When a message is not enough, these pages go further:

| Check | Where to look |
| --- | --- |
| `git`, `checkout`, `tag` | [What goes into a release](what-goes-into-a-release.md#1-the-range), including [a shallow clone](what-goes-into-a-release.md#a-shallow-clone) |
| `config` | [Configuration](configuration.md#a-file-that-cannot-be-read-as-meant) |
| `model`, `endpoint` | [Providers](providers.md#what-each-provider-needs) |
| `GitHub read`, `GitHub write` | [GitHub](github.md#the-token) |
| `PR titles` | [Writing pull requests](writing-pull-requests.md#the-minimum-a-prefix-in-the-title) |

A warning about pull request titles deserves a look before the first `generate`.
Chartula takes the category of each change from the prefix of its title, `feat:` or `fix:` for example, without asking the model, so a release without prefixes turns into one long list under `Other`.

## 5. See the facts with `chartula preview`

```bash
chartula preview
```

`preview` reads the release and establishes its [facts](glossary.md#fact) the way `generate` does, then stops before the model.
It makes no model call and writes nothing, so you can run it as often as you like.
On Chartula's own `v0.1.0-preview.3`:

```console
$ chartula preview
Preview of v0.1.0-preview.3 - no model call was made, and nothing was written or published.

Release: 7 commits, 7 pull requests, 6 facts (6 with a description, 15,083 characters)

Facts (6):
  #252     Fix: fix: keep the reason in each flag, and bound the customer outcome by the facts
           technical, customer
  #251     Feature: feat(observability): record the range a run read in its run record
           technical, customer
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

Read it as the list of what your release notes will say.
Each fact names its category and the [renderings](glossary.md#rendering) it will appear in; each dropped change names the setting that dropped it.
A change missing from the list, or under the wrong category, is fixed in the pull request title or in `chartula.yaml`, not in the output ([What goes into a release](what-goes-into-a-release.md) explains each decision).

On a repository's first tag, `preview` asks before it reads every commit up to it, because that [range](glossary.md#range) can be long ([A first tag](what-goes-into-a-release.md#a-first-tag)).

## 6. Write the release notes with `chartula generate`

```bash
chartula generate
```

`generate` establishes the same facts, has the model write each [audience](glossary.md#audience) from them, checks what the model wrote, and writes the results.
It is the step that costs tokens: the calls `preview` listed.
Its report, shortened:

```console
$ chartula generate
Generated changelog for v1.3.0

--- Technical ---
### Added

- ...

--- Customer ---
  description: ...

### What's New

- ...

Flagged for review:
  ! "..." - The facts say nothing about ...

Wrote:
  - /work/my-repo/changelog.json
  - /work/my-repo/release-v1.3.0.md
  - /work/my-repo/CHANGELOG.md
  - https://github.com/owner/name/releases/tag/untagged-4f2a91c0 (draft)

Run metrics
  ...
  Recorded in /work/my-repo/chartula-runs/20260930T122047Z-v1.3.0.json
```

Read the entries under **Flagged for review** first.
Each [flag](glossary.md#flag) is a [claim](glossary.md#claim) the checks could not find in the facts, and the rendering it belongs to is where the model most likely wrote more than your pull requests say.
Nothing in `CHANGELOG.md`, the customer page or the draft marks a flagged entry, so read the flags before you close the terminal.
The [run record](run-record.md) of step 8 keeps them, if you need them later.

`generate` wrote these files into your checkout:

- **`CHANGELOG.md`** - the technical notes, added at the top of the file.
- **`release-v1.3.0.md`** - the customer notes, as a page of its own.
- **`changelog.json`** - the same release as data, for tools that build on it.

It also created the release notes on GitHub as a draft, marked `(draft)` in the report.
[Outputs](outputs.md) describes each output, and [Costs and checks](costs-and-checks.md#reading-the-run-summary) the `Run metrics` at the end of the report.

## 7. Read and publish the draft

Open the draft from the link in the report, or from the Releases page of your repository.
A draft is visible only to people with write access, so nobody else sees it yet.

Read it, and edit it on GitHub where the flags or your own reading call for it.
Then publish it with the button on that page, or with the GitHub CLI:

```bash
gh release edit v1.3.0 --draft=false
```

Commit `CHANGELOG.md` the way you commit any other change, and edit it the same way as the draft first.

## 8. Keep the run records out of git

`generate` keeps a record of every run in `chartula-runs/`: what it cost, what it was made with, and the complete facts, pull request descriptions included.
The record is for comparing runs on your machine, and it can hold text from private pull requests, so keep it out of your repository:

```bash
echo 'chartula-runs/' >> .gitignore
```

[Run record](run-record.md) describes what a record holds.

## Next

- [CI](ci.md) runs `generate` on every tag, with a complete GitHub Actions job.
- [Writing pull requests](writing-pull-requests.md) shows what your authors can do for better release notes, level by level.
- [Configuration](configuration.md) lists every setting, from the model to which categories are left out.
- [Troubleshooting](troubleshooting.md) has the cause and the fix for a run that stops.
