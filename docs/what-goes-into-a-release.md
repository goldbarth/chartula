# What goes into a release

Chartula decides what a release contains before any model sees it, with fixed rules and no guessing.
So you can predict from this page which changes of a release appear, under which category, and for which audience.
The steps below run in this order.

## 1. The range

A release is every commit after the previous tag, up to the release tag.
Chartula reads that range with `git` from the checkout the run starts in.
For `v1.3.0` with `v1.2.0` before it, the release is `v1.2.0..v1.3.0`.

### A first tag

A first tag has no previous tag, so its release is every commit up to it, from the first commit on.
For a new project, that is its first release, and it renders without any option.
For a project that tags late, it is a development log: intermediate states stand next to the changes that replaced them.

`--since <ref>` starts the release after a tag or commit you name instead, which has to be an ancestor of the release tag.
It works on any tag, not only the first.
Adopting Chartula on a project with a long history usually means `--since` on the first run, pointing at the last state that was already shipped; every later tag starts after its predecessor on its own.
So `--since` is only needed when a release starts later than the previous tag or the first commit.

### A large range is confirmed

Before any GitHub request or model call, the run header names the range with both of its ends.
A first tag, or a range with more commits than [`range.confirmAboveCommits`](configuration.md#range) (200 by default), is confirmed twice before it is read:

```console
$ chartula preview --tag v0.1.0
...
Range:  every commit up to v0.1.0 (69 commits), the first tag
        It costs 69 GitHub requests, and every pull request in it goes to the model once per audience.
        To start later, pass --since <ref>: the commits after <ref>, up to v0.1.0.
Render all 69 commits up to v0.1.0? [y/N] y
This sends every pull request in the range to the model. Continue? [y/N] y
```

Anything but `y` stops the run with nothing spent.
Without a terminal, as in CI, nobody can answer, so the run stops with the same text, and `--yes` confirms the range up front.
So a job stops once on its first tag, and every later tag runs on its own unless it is larger than the threshold.

### A shallow clone

A shallow clone ends its history at the fetch depth, which looks the same as a first tag's history ending at the first commit.
`actions/checkout` makes one by default (`fetch-depth: 1`).
So a run in a shallow clone without `--since` stops before any GitHub request or model call, `--yes` included, since its history ends at the fetch depth, not at the first commit:

```console
$ chartula preview --tag v0.2.0
Error: The checkout is a shallow clone: its history ends at the fetch depth, not where 'v0.2.0' starts, so neither the previous tag nor the first commit can be read from it.
  Fetch the full history and tags: git fetch --unshallow --tags
  In GitHub Actions, check out with fetch-depth: 0; in GitLab CI, set GIT_DEPTH: 0.
```

`--since <ref>` still runs in a shallow clone when the ref was fetched along with the tag and everything between them was too, so a deliberately limited fetch is not blocked.
A ref outside the fetched history, one the fetched history does not connect to the tag, or a range the fetch depth cuts off inside - a merged branch older than the depth - is refused with the same way out.

## 2. Pull requests, or commits

For every commit in the range, Chartula asks GitHub which pull request it belongs to.
That costs one request per commit, which [GitHub](github.md#the-rate-limit) counts against its rate limit.

Merged pull requests are the source of the release, because their title and description say what changed in words meant for a reader.
Each pull request becomes one change, however many commits it has.

A commit that belongs to no merged pull request, such as a direct push to the main branch, becomes a change of its own, next to the pull requests.
It has its subject line as the title, no description and no labels, and the steps below treat it like any other change.
So a fix pushed straight to `main` is in the release, and a `chore:` or `ci:` push is dropped as `Internal`, as a pull request with that title would be.
A release with no merged pull request at all is the same case: every commit becomes a change.

A merge commit without a pull request, such as a branch merged locally and pushed, becomes no change.
What it brings in is the merged commits, which are in the range themselves.
The changes keep the order of the range, so a direct push stands between the pull requests merged before and after it.

The [run summary](run-metrics.md) counts the commits without a pull request, and the merge commits among them that were skipped, so no commit leaves the release without the run saying so.
Asking GitHub about each commit is what finds its pull request, so a direct push costs no request beyond the one per commit.

### The title and the description

The title of a change is the pull request title.
When that title says nothing - it is empty, starts with `Merge `, or is exactly `wip`, `update`, `updates`, `misc`, `changes`, `fix`, `fixes` or `cleanup` - the first informative line of the description takes its place, or `PR #<n>` when there is none.

The description is the pull request body without its HTML comments, because GitHub does not show them to a reader.
A body left with nothing but headings and checklist items is an unfilled template, and counts as no description.
So a pull request template's placeholders never become facts.

## 3. Reverts

A revert that names what it takes back is paired with it.
When both are in the same range, neither appears, since the change never shipped.

A pull request counts as a revert when its title starts with `revert:`, `revert(scope):` or GitHub's `Revert "..."`.
It names its target when its title or description has a commit hash of at least seven characters that matches exactly one commit in the range (`This reverts commit ...`, `Reverts d85a4b9, bc826ba`), or GitHub's `Reverts owner/repo#N` and `Reverts #N`.
A pull request number in prose (`the waterfall from #61`) does not count, because it is as often context as a target.

A revert that names anything outside the range, or nothing recognisable, takes back what it could match and stays as an entry of its own, so a removal a reader may have met is never hidden.
A revert that a later revert in the same range takes back removes nothing, so what it reverted stays.

A change that a later change in the same range replaced without reverting it still appears as an entry of its own.
Chartula cannot tell that one pull request supersedes another without reading their meaning, which would put a fact decision into the model.

A change from a commit without a pull request pairs no reverts, so a revert pushed straight to `main` stays as an entry of its own.

## 4. The category

The category comes from the [Conventional Commits](https://www.conventionalcommits.org/) prefix of the title: a type, an optional scope in parentheses, an optional `!`, and a colon, as in `feat(cli)!: add --since`.

| Prefix | Category |
| --- | --- |
| `feat`, `feature` | `Feature` |
| `fix`, `bugfix` | `Fix` |
| `perf` | `Performance` |
| `docs`, `doc` | `Documentation` |
| `refactor` | `Refactor` |
| `build`, `ci`, `chore`, `test`, `tests`, `style` | `Internal` |
| `revert` | `Other` |
| any other type, or no prefix | `Other` |

Case does not matter, so `Feat:` is a `Feature`.
A title without a prefix is `Other`, so a repository without Conventional Commits gets every change as `Other` and none of its internal work filtered out.

A revert that stays in the release is `Other`, not `Internal`, because it takes back something a reader may have met.

The `labels.category` setting overrides the prefix: the first of a pull request's labels that it maps forces that category.

```yaml
labels:
  category:
    security: Fix
```

## 5. Breaking changes

A change is breaking when any of these holds:

- a `!` before the colon of the prefix, as in `feat!:` or `feat(api)!:`;
- the type `breaking`, as in `breaking: drop the v1 endpoint`;
- a line in the description that starts with `BREAKING CHANGE:` or `BREAKING-CHANGE:`, in capitals.

The footer has to start its line and be written in capitals, so prose that only discusses breaking changes does not mark a change as breaking.
Chartula reads the description for this whatever `factBase.depth` says, so a breaking change stays breaking at `title-only` too.

## 6. What is dropped

Chartula drops a change in this order:

1. A label listed in `labels.exclude` drops it, whatever else applies.
2. With `labels.onlyIncludeLabeled: true`, a change without labels is dropped. A commit carries no labels, so a change from a commit without a pull request is dropped, and a release without pull requests keeps nothing.
3. A breaking change is kept from here on, whatever its category.
4. A change whose category is in `filter.excludeCategories` is dropped. The default list is `[Internal]`.

A forced category from `labels.category` counts in step 4, not the one from the prefix.
Everything that survives is a fact, and appears in `changelog.json`.

```yaml
labels:
  exclude: [no-changelog]
filter:
  excludeCategories: [Internal, Documentation]
```

## 7. Who can meet a change

Each fact says whether a reader can come into contact with the change (`userVisible` in `changelog.json`):

- A breaking change always can, whatever its labels say.
- Otherwise a visibility label decides, because whoever wrote the pull request knows whether users meet the change. A label in `labels.userFacing` says yes, one in `labels.internal` says no, and both together count as internal, because that reading never shows an internal change to a reader.
- With no visibility label, the category decides: `Feature`, `Fix`, `Performance` and `Other` count as something a reader can meet, and `Documentation`, `Refactor` and `Internal` do not.

That fallback is why labelling nothing costs nothing.
A category says what kind of change something is, not whether a reader can meet it - a feature can be entirely internal, such as a serialisation format - so a label decides where the category cannot.

The audiences read this differently:

| The change | technical | customer | product |
| --- | --- | --- | --- |
| a reader can meet it | yes | yes | yes |
| an `internal` label on a `Feature`, `Fix`, `Performance` or `Other` change | yes | no | yes |
| none of the above | no | no | no |

An `internal` label keeps a change away from customers, not from developers or product managers, so it narrows the customer rendering only.
A change that reaches no rendering is still a fact in `changelog.json`.

```yaml
labels:
  internal: [visibility:internal]
  userFacing: [visibility:user-facing]
```

## 8. How much of each change the model reads

`factBase.depth` decides how much of each change reaches the model:

| `factBase.depth` | The model reads | `linkedIssues` |
| --- | --- | --- |
| `title-only` | the title | the issue numbers the title closes |
| `title-and-description` (default) | the title and the description | the issue numbers they close |

`title-only` sends far less text, so a run costs less and the model has less to overstate, but it also has less to say.
The issue numbers are the numbers after `close`, `closes`, `closed`, `fix`, `fixes`, `fixed`, `resolve`, `resolves` or `resolved` and a `#`, as in `closes #12`.
They come from the text the model reads, so a number in a rendering that the text closes is not flagged as invented.
Chartula reads no issue, so the number is all a fact knows about it.

`title-description-and-issues`, a third value of earlier versions, is still accepted and means `title-and-description`: it read no issue either, and the model read the same text ([#258](https://github.com/goldbarth/chartula/issues/258)).

The title fallback and the breaking footer read the description at every depth, because they decide facts, not wording.
