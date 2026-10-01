# Costs and checks

A `generate` run pays your model provider for two kinds of calls: one rendering per audience, and one thorough check per rendering.
This page shows how to see the size of a run before you pay for it, how to read what it cost afterwards, and whether the thorough check earns its share.
It ends with what a measured comparison of models and settings found.

## Before a run: the size `preview` shows

`chartula preview` makes no model call and costs no tokens.
It ends with the calls `generate` would make, and how many characters each rendering call would send:

```console
$ chartula preview --tag v1.9.0 --since 675555a7
...
Release: 19 commits, 19 pull requests, 18 facts (18 with a description, 40,459 characters)
...
generate would make 4 model calls:
  technical 1 rephrasing call, 43,212 characters of prompt, then 1 thorough check
  customer  1 rephrasing call, 47,277 characters of prompt, then 1 thorough check
  A thorough check sends the rendering along with the facts, so its size is known only once the rendering is.
```

So a release larger than you expected, or a range that reaches too far back, shows before a single token is spent.
[Estimating the cost of a run](#estimating-the-cost-of-a-run) turns these characters into tokens.

## Reading the run summary

Every `generate` run ends with a summary of what it did and what it cost.
It is printed on every run, so measuring a run is never something you have to remember to turn on.
This summary is from a real run of the release above, rendered and checked with `gpt-6-sol`:

```text
Run metrics
  Release:          19 commits, 19 pull requests, 18 facts (18 with a description, 40,459 characters)
  Rule-based check: 2 runs, 0 with findings, 0 claims, no tokens
  Thorough check:   2 runs, 2 with findings, 4 claims, 22,420 in / 345 out, 9.4 s (longest 5.0 s)
    of which 0 in cached, 0 out reasoning
    caught 4 claims the rule-based check missed, for 22,765 tokens in 2 calls
  Rephrasing:       2 calls, 21,293 in / 1,394 out, 21.5 s (longest 13.5 s)
    of which 0 in cached, 0 out reasoning
  Total:            45,452 tokens in 40.3 s
  Retries:          none
  Recorded in /work/my-repo/chartula-runs/20260925T114903Z-v1.9.0.json
```

| Line | Reading |
| --- | --- |
| `Release` | How much release the run worked on: commits in the range, the merged pull requests behind them, the facts the changelog was written from after filtering, and the characters of description the model read beyond the titles. When some commits belong to no pull request, it names them too: `3 commits without one (1 merge commit, skipped)`. Each became a change of its own, except the merge commits ([What goes into a release](what-goes-into-a-release.md#2-pull-requests-or-commits)). So a release with long descriptions costs more than one with the same number of pull requests and short ones. |
| `runs` | How often the check was asked to run: once per rendered audience. |
| `with findings` | How many of those runs flagged at least one claim. |
| `claims` | How many claims the check flagged in total. |
| `in / out` | Tokens sent to and produced by the model, attributed to that operation. |
| `of which ... cached` | The part of the input the provider served from its prompt cache, which most providers bill at a lower rate. |
| `of which ... reasoning` | The part of the output the model spent reasoning rather than writing. It is billed as output and never appears in the changelog. |
| time, `longest` | How long that operation's calls took together, retries included, and the longest single call. |
| `caught ... missed` | The claims only the thorough check found, and what the check spent to find them ([Does the thorough check earn its tokens?](#does-the-thorough-check-earn-its-tokens)). |
| `Total` | All tokens of the run, and how long the whole run took, from reading history to the last model call. |
| `lower bound` | Printed under `Total` as `lower bound, <n> of <m> calls unreported` when the provider returned no usage for some calls. So the real token count is higher than the total says. |
| `Retries` | Requests sent again after the first, per operation ([A slow run](troubleshooting.md#a-slow-run)). |
| `Recorded in` | The [run record](run-record.md) that keeps these figures after the terminal is gone. |

Providers break the tokens out differently.
OpenAI and compatible endpoints that follow it report both cached input and reasoning; Anthropic reports cache reads but folds thinking into the output without a separate count.
What a provider does not report is shown as `not reported`, never as zero, because zero reasoning would claim the model did not think.
The `out` figure is complete either way: reasoning is inside it, not on top of it.
So you can compare the `out` figures of two providers even when only one of them reports reasoning.

### Lines that point to a problem

Some lines appear only when something went wrong, and [Troubleshooting](troubleshooting.md) has the cause and the fix for each:

- **`failed without an answer`** under an operation: calls that ended in an error. Their time is in the totals, their tokens are not, because there were none to report ([A model call fails](troubleshooting.md#a-model-call-fails)).
- **`came back unreadable and verified nothing`** under the thorough check: runs whose verdict could not be read ([A thorough check that verified nothing](troubleshooting.md#a-thorough-check-that-verified-nothing)).
- **`Retries`** above zero, or a `longest` call that is most of an operation's time ([A slow run](troubleshooting.md#a-slow-run)).

A thorough check with runs but no calls was turned off, or had nothing to check:

```text
  Thorough check:   2 runs, 0 with findings, 0 claims, 0 in / 0 out
    caught 0 claims the rule-based check missed, for 0 tokens in 0 calls
```

A `lower bound` line under `Total` means the provider returned no token usage for some calls, so their tokens are missing from every count above it.
An endpoint that reports no usage at all, run here against a local stub, leaves every count at zero and every call unreported:

```text
  Rephrasing:       2 calls, 0 in / 0 out, 0.2 s (longest 0.2 s)
    of which cached input not reported, reasoning not reported
  Total:            0 tokens in 0.4 s
    lower bound, 4 of 4 calls unreported
```

So the summary is no measure of that run's cost; the provider's own usage page is, and the [run record](run-record.md#metrics) keeps the count as `callsWithoutUsage`.

## The two checks

Both checks read every rendering against the facts, and neither changes the text.
What they cannot back is listed under "Flagged for review" below the rendering, with the pull request it concerns where there is one.
A flag does not fail the run.
So read the flags before you publish, because no output file marks a flagged entry.

**The rule-based check** always runs and makes no model call.
It flags a number in the text that no fact contains, and a quoted or backticked name that appears in no fact.
So it is free, and it catches only what a lookup can decide.

**The thorough check** asks a model whether each claim of the rendering is backed by the facts, one call per rendering.
It catches what a lookup cannot: a claim of degree, a distorted meaning, a change described as delivered that a later pull request removed.
It is on by default, and `faithfulness.thorough: false` turns it off ([Configuration](configuration.md#faithfulness)).
It can run on another model than the rendering, with `faithfulness.model`.
So you can render with one model and check with a cheaper or a steadier one.

## Does the thorough check earn its tokens?

The line under the thorough check answers it for each run:

```text
    caught 4 claims the rule-based check missed, for 22,765 tokens in 2 calls
```

It pairs the claims **only** the thorough check caught with the tokens the check spent to catch them.
Claims both checks found are not counted, because the thorough check added nothing on those.
In this run the check spent 22,765 of the run's 45,452 tokens, about half, and found four claims the free check did not.

Read the line across several of your releases, not one:

- **It keeps catching claims:** the check earns its tokens, so keep it on.
- **It catches nothing, release after release:** the rule-based check already finds everything the model finds, and the tokens buy nothing.

Then read what it flagged, because a count says nothing about whether a flag is justified.
A check that flags a lot of supported passages costs you reading time on top of its tokens.
In the [comparison of 2026-09-25](#the-model-comparison-of-2026-09-25), the rule-based check found nothing in 42 runs, and the thorough check was the only one that caught anything.

## Estimating the cost of a run

A provider bills input tokens and output tokens at different rates, and cached input at a lower one.
The cost of a run is each count from the summary times its rate from the provider's price list: [Anthropic](https://www.anthropic.com/pricing#api), [OpenAI](https://openai.com/api/pricing/).
Chartula does not keep prices, because they change without a release of Chartula.

Before a run, estimate the input tokens from the characters `preview` shows:

1. Divide the characters of prompt by four.
   No tokenizer packs prose, code and JSON into fewer than roughly four characters per token, so the estimate errs on the high side.
2. With the thorough check on, double it.
   A check sends the facts again, with the rendering.
3. Add a few thousand output tokens, more with `thinking` on.

For the release above, `preview` counts 90,489 characters of prompt (on 2026-09-30, from the same range).
Divided by four, that is about 22,600 tokens; the real run of 2026-09-25 sent 21,293 tokens in its two rendering calls and 22,420 in its two checks.
So the estimate from `preview` lands close to what the run sends, for your release and before you pay for it.

What a run cost in money is in the [comparison of 2026-09-25](#the-model-comparison-of-2026-09-25), at the list prices of that day.

## Comparing runs

Each `generate` run keeps its figures in a [run record](run-record.md) in `chartula-runs/`.
So comparing a prompt change, another model, or the thorough check on and off means comparing files, not copying numbers out of a terminal.

One line per run, with the model, whether the thorough check ran, the input and output tokens, the claims only the thorough check caught, and the duration:

```console
$ jq -r '[input_filename, .provenance.model, .provenance.thoroughCheck,
    (.metrics.rephrase.inputTokens + .metrics.faithfulnessCheck.inputTokens),
    (.metrics.rephrase.outputTokens + .metrics.faithfulnessCheck.outputTokens),
    .metrics.thoroughCheck.onlyThoroughFlags, .metrics.durationSeconds] | @tsv' chartula-runs/*.json
chartula-runs/20260925T114903Z-v1.9.0.json	gpt-6-sol	true	43713	1739	4	40.27
chartula-runs/20260925T120749Z-v1.9.0.json	gpt-6-sol	false	21293	1508	0	32.969
chartula-runs/20260925T132051Z-v1.9.0.json	gpt-6-luna	true	43843	1893	4	34.576
```

Which pull requests each run flagged:

```console
$ jq -r '[input_filename, ([.audiences[].flags[]?.pullRequest // empty] | unique | map("#\(.)") | join(" "))] | @tsv' chartula-runs/*.json
chartula-runs/20260925T114903Z-v1.9.0.json	#223 #233 #237
chartula-runs/20260925T120749Z-v1.9.0.json
chartula-runs/20260925T132051Z-v1.9.0.json	#219 #223 #233
```

A flag that repeats across runs is found by the pull request it concerns, not by how alike its wording is.
So a flag that every run raises, like `#233` here, is the first one to read.
The fields are described in the [run record](run-record.md#schema).

## The model comparison of 2026-09-25

The measurements behind this section live in [chartula-evals](https://github.com/goldbarth/chartula-evals): the [evaluation](https://github.com/goldbarth/chartula-evals/blob/main/sweeps/servicedesklite-v1.9.0-evaluation.md), the [statistics report](https://github.com/goldbarth/chartula-evals/blob/main/sweeps/servicedesklite-v1.9.0-stats/report.md), and the charts [axis A](https://github.com/goldbarth/chartula-evals/blob/main/sweeps/servicedesklite-v1.9.0-stats/axis-a.svg) and [axis B](https://github.com/goldbarth/chartula-evals/blob/main/sweeps/servicedesklite-v1.9.0-stats/axis-b.svg).
What follows summarizes them; the figures are from there.

### What was measured

- **The release:** `goldbarth/ServiceDeskLite` `v1.9.0`, 19 commits, 18 facts, run with `--no-publish`.
- **The version:** Chartula `0.1.0-preview.3`, in all 43 attempts.
- **The runs:** three per setting, 14 settings, one failed attempt on a local model.
- **The figures:** the median of the three runs, the range in brackets where they differ.
- **The cost:** OpenAI list price, standard tier, on 2026-09-25, with every input token at the full rate. So the order of the runs, which decided what the cache served, does not shift the comparison.

### What it cannot tell

- Three runs per setting cannot tell a difference of one flag per run from noise.
- A flag count says nothing about whether a flag is justified, or about how well a text reads. The texts were not scored.
- One release of one repository was measured. How cost grows with the size of a release was not.
- The Claude models were not measured yet. Their comparison runs follow, and this section adds them when they exist.

So read the findings below as a starting point for your own runs, not as a verdict on a model.

### Model and thinking

Both models checked by `gpt-6-sol` with `thinking: disabled`:

| Rendering model | `thinking` | Cost $ | Duration s | Flags per run |
| --- | --- | --- | --- | --- |
| `gpt-6-luna` | `disabled` | 0.051 | 32 (30-35) | 4 (3-4) |
| `gpt-6-luna` | `low` | 0.052 | 43 (39-45) | 4 (3-5) |
| `gpt-6-luna` | `medium` | 0.055 | 72 (55-83) | 4 (2-6) |
| `gpt-6-luna` | `high` | 0.055 | 128 (124-137) | 3 (3-4) |
| `gpt-6-sol` | `disabled` | 0.105 | 40 (39-40) | 3 (3-4) |
| `gpt-6-sol` | `low` | 0.105 | 48 (47-51) | 4 (2-5) |
| `gpt-6-sol` | `medium` | 0.118 | 76 (69-94) | 3 |
| `gpt-6-sol` | `high` | 0.170 | 170 (161-208) | 3 |

Thinking lowered the flags at no level, and raised duration and cost.
With `gpt-6-sol`, `high` cost 62% more than `disabled` and took 4.3 times as long.
A run rendered by `gpt-6-luna` cost about half of one rendered by `gpt-6-sol`, and of its $0.051 the check on `gpt-6-sol` took $0.048.
So `thinking: disabled` is the setting to start from, and with a cheap rendering model the thorough check is most of the bill.

### Which model checks

Rendering and checking on `gpt-6-luna` and `gpt-6-sol` in every combination, `thinking: disabled`:

| Rendering model | Checked by `gpt-6-luna` | Checked by `gpt-6-sol` |
| --- | --- | --- |
| `gpt-6-luna` | 6 flags per run | 4 (3-4) |
| `gpt-6-sol` | 5 (4-8) | 3 (3-4) |

The flags depended on the checker more than on the renderer.
`gpt-6-luna` as the checker flagged more, but at least 7 of its 35 flags said in their own reasoning that the passage was supported; none of `gpt-6-sol`'s 88 flags did.
`gpt-6-luna` checked for $0.0025 per run, against $0.048 for `gpt-6-sol`.
So a cheaper checker costs less in tokens and more in reading time, since you sort out its false alarms by hand.

### The thorough check, on and off

Against `gpt-6-sol` with `thinking: disabled` and the check on ($0.105, 40 s, 3 (3-4) flags):

| Setting | Input tokens | Cost $ | Duration s | Flags per run |
| --- | --- | --- | --- | --- |
| The check on | 43,751 | 0.105 | 40 | 3 (3-4) |
| `faithfulness.thorough: false` | 21,293 | 0.057 | 34 | 0 |
| `factBase.depth: title-only` | 5,876 | 0.023 | 29 | 1 (1-2) |

With the check off, no run reported a flag: the rule-based check found nothing in any of the 42 runs.
With it on, it added 3 (3-4) flags per run for 83% more cost and 6 s more.
One of them, on `#233`, came up in every run of 7 of the 8 settings in the model table, and in 2 of 3 runs of the eighth.
According to the flag, the rendering listed `#233` as added although a later pull request of the same release had removed it again.
Whether the other flags were justified was not assessed.
So turning the check off saves close to half the cost and leaves the rendering unchecked by anything but a lookup.

`title-only` sent 87% fewer input tokens and cost 78% less, and the flags fell from 3 to 1.
Fewer flags here means less material, not better text: without a description, a rendering has less to stretch.
What the texts lost in content was not measured.
So `title-only` suits a repository whose descriptions add nothing to the title ([Configuration](configuration.md#factbase)).

### Local models

Rendered with `qwen3:14b` on Ollama (0.33.3, RTX 4080 SUPER, a context window of 24,576 tokens), cost $0 in tokens:

| Checked by | Attempts | Duration s | Flags per run |
| --- | --- | --- | --- |
| `qwen3:14b` | 3 of 3 | 90 (71-97) | 17 (0-28) |
| `granite4.1-guardian:8b` | 3 of 4 | 219 (48-677) | 3 (0-4) |

`qwen3:14b` failed as the rendering model.
In seven attempts, Chartula once rejected its text for an entry about a fact it was not sent, and once it copied an example from Chartula's instructions into the customer page.
None of the 36 runs on `gpt-6` models copied an example.

`qwen3:14b` failed as the checker.
It flagged 0, 28 and 17 claims in three runs, and 13 of the 17 said in their own reasoning that the facts supported the claim.

`granite4.1-guardian:8b` failed as the checker too.
It kept to the response format, but 2 of its 7 flags held its thinking log where the reason belongs, and one check call took 618 s with a retry.

So no local model has passed yet, and a local run's output needs a line-by-line reading before anything from it is published.

### Recommendations

As of 2026-09-25, for a release like the one measured:

- **Keep the thorough check on.** It was the only check that caught anything, and it caught the same wrong claim in every run.
- **Start with `thinking: disabled`**, for rendering and checking. Thinking raised cost and duration and lowered no flag.
- **Check with the steadier model.** Among the `gpt-6` models, that is `gpt-6-sol`, whatever model renders.
- **Render with the cheaper model**, if your own reading of its output holds up. `gpt-6-luna` rendered for $0.003 of a $0.051 run.
- **Keep the fact base at `title-and-description`** unless your descriptions add nothing to the titles.

[Providers](providers.md#choosing-a-model) lists the model ids Chartula has been run with.
