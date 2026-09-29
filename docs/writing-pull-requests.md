# Writing pull requests

Chartula writes its release notes from your pull requests: the title, the description and the labels.
It adds nothing a pull request does not say, so what your authors write decides what the notes can say.
This page sorts that input into levels, from the one thing worth asking of every author to what is optional, and shows what each level changes in the output.

The rendered entry quoted below comes from Chartula's own `v0.1.0-preview.3`.
Another run words it differently, but where an entry stands and what it may say stay the same.

## Without any of it

Chartula works on pull requests that follow no convention at all.
It still reads every merged pull request of the release, links each entry to its pull request, keeps the facts out of the model's hands, and checks each text against them.
A title such as `wip` or `update` is replaced by the first informative line of the description, so an entry never reads as a placeholder.

What it cannot do without input is tell a feature from a fix, or internal work from a change a reader meets.
Every change lands in one group, and a dependency bump stands next to a new feature in the customer notes.

## The minimum: a prefix in the title

Start every pull request title with a [Conventional Commits](https://www.conventionalcommits.org/) type:

```text
feat(observability): record the range a run read in its run record
fix(cli): refuse review mode until an interactive reviewer exists
docs: rewrite comments for readability
chore(deps): bump the Anthropic SDK
```

The type decides the category, and the category decides where an entry stands and who sees it.
[What goes into a release](what-goes-into-a-release.md#4-the-category) lists every type.

| Title | Category | Technical notes | Customer notes |
| --- | --- | --- | --- |
| `feat: ...` | `Feature` | under "Added" | under "What's New" |
| `fix: ...` | `Fix` | under "Fixed" | under "Bug Fixes" |
| `docs: ...` | `Documentation` | not shown | not shown |
| `chore: ...` | `Internal` | dropped | dropped |
| no prefix | `Other` | under "Changed" | under "What's Changed" |

A pull request titled `Rewrite comments for readability` is an `Other` change, so your customers read about your code comments.
Titled `docs: rewrite comments for readability`, it stays a fact in `changelog.json` and reaches neither rendering.

This is the one rule worth enforcing, because it costs an author a few characters and decides more of the output than anything else.

### Enforce it in CI

A check on the pull request title catches a missing prefix before the merge, when fixing it is still a title edit.
Save this as `.github/workflows/pull-request-title.yml`:

```yaml
name: Pull request title

on:
  pull_request:
    types: [opened, edited, synchronize, reopened]

permissions: {}

jobs:
  title:
    runs-on: ubuntu-latest
    steps:
      - name: Check the Conventional Commits prefix
        env:
          TITLE: ${{ github.event.pull_request.title }}
        run: |
          if ! printf '%s' "$TITLE" | grep -Eiq '^(feat|feature|fix|bugfix|perf|docs|doc|refactor|build|ci|chore|test|tests|style|revert|breaking)(\([^)]*\))?!?: '; then
            echo "::error::The title needs a Conventional Commits prefix, such as 'feat: ...' or 'fix(cli): ...'."
            exit 1
          fi
```

The check accepts the types Chartula knows, so a typo in the type fails the check instead of turning the change into `Other`.
It reads the title through an environment variable, so a title cannot inject shell code into the job.
Make it a required status check in the branch protection of your main branch, so a pull request cannot be merged without it.

## What pays off

### Mark breaking changes

Add a `!` after the type, or a `BREAKING CHANGE:` line to the description:

```text
feat(api)!: drop the v1 endpoint
```

A breaking change stands first in its group of the technical notes, marked `**Breaking:**`, and at the top of the customer notes under "What needs action".
No category filter drops it, and every audience sees it.
Without the marker, Chartula has no way to know, and the change reads like any other.

### Two sentences of description

Write what changed and what it means for someone who uses the project, for example:

```text
Every run record now names the range it read: where it started, the ref it started from,
and the commits at both ends. A tag or branch that moves later no longer hides which
commits a run saw.
```

The model rephrases from the title and the description, and from nothing else.
With a title only, an entry can only restate the title.
With a description, it can say what the change does for the reader; the technical entry of `v0.1.0-preview.3` for `feat(observability): record the range a run read in its run record` reads:

```markdown
- Record the range read for each run, including its start, source ref, and resolved commit hashes. ([#251](https://github.com/goldbarth/chartula/pull/251))
```

The start, the ref and the hashes come from the description of #251, not its title.
Two sentences are enough; the rest of a long description adds tokens to every run and gives the model more to overstate.

The description is copied in full into `changelog.json`, so notes meant only for reviewers become public when that file is published ([`changelog.json`](changelog-json.md)).

### Link the issue

`closes #12`, `fixes #12` or `resolves #12` in the title or description links the issue.
With `factBase.depth: title-description-and-issues`, its number appears under `linkedIssues` in `changelog.json`, where a tool that reads the file can follow it.
The renderings do not link issues, and Chartula reads nothing from the issue itself.

## Optional: labels

Labels answer what a prefix cannot, and each one is configured in `chartula.yaml` under names you choose.

A visibility label decides whether a reader can meet a change when the category would guess wrong:

```yaml
labels:
  internal: [visibility:internal]
  userFacing: [visibility:user-facing]
  actionRequired: [action-required]
```

- `visibility:user-facing` on a `refactor:` that changes an output format puts it in front of customers, which its category alone would not.
- `visibility:internal` on a `feat:` that only other developers meet keeps it out of the customer notes, and leaves it in the technical notes.
- `action-required` on a change that is not breaking but still asks something of the reader puts it under "What needs action".

[What goes into a release](what-goes-into-a-release.md#7-who-can-meet-a-change) has the full rules, including the labels that drop a change or force its category.
A repository that labels nothing loses nothing, because the category decides wherever no label does.

## What to ask of your authors

| Level | Ask | What it changes |
| --- | --- | --- |
| Minimum | A Conventional Commits prefix in the title | The group of each entry, and whether internal work reaches your readers |
| Pays off | `!` for a breaking change | A breaking change stands first and asks the reader to act |
| Pays off | Two sentences of description | Entries that say what a change does, not only what it is called |
| Pays off | `closes #N` | Issue numbers in `changelog.json`, at the matching depth |
| Optional | Visibility and action labels | Who sees a change, where the category would guess wrong |

A team that asks only for the prefix, and enforces it in CI, already gets release notes grouped the way a reader expects.
