# Costs and checks

A `generate` run pays your model provider for two kinds of calls: one [rendering](glossary.md#rendering) per [audience](glossary.md#audience), and one [thorough check](glossary.md#thorough-check) per rendering.
This page shows how to see the size of a run before you pay for it, how to read what it cost afterwards, and whether the thorough check earns its share.
It ends with what a measured comparison of models and settings found.

For a quick view of what using Chartula can cost and how long it can take, [What to expect from Chartula](what-to-expect.md) shows measured examples across four release scenarios, with charts and completion counts.

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

So a release larger than you expected, or a [range](glossary.md#range) that reaches too far back, shows before a single token is spent.
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
| `Release` | How much release the run worked on: commits in the range, the merged pull requests behind them, the [facts](glossary.md#fact) the changelog was written from after filtering, and the characters of description the model read beyond the titles. When some commits belong to no pull request, it names them too: `3 commits without one (1 merge commit, skipped)`. Each became a change of its own, except the merge commits ([What goes into a release](what-goes-into-a-release.md#2-pull-requests-or-commits)). So a release with long descriptions costs more than one with the same number of pull requests and short ones. |
| `runs` | How often the check was asked to run: once per rendered audience. |
| `with findings` | How many of those runs flagged at least one [claim](glossary.md#claim). |
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
A [flag](glossary.md#flag) does not fail the run.
So read the flags before you publish, because no output file marks a flagged entry.

**The [rule-based check](glossary.md#rule-based-check)** always runs and makes no model call.
It flags a number in the text that no fact contains, and a quoted or backticked name that appears in no fact.
A number is the same with and without its thousands separators, so `8000` is backed by a pull request that says `8,000`.
So it is free, and it catches only what a lookup can decide.

**The thorough check** asks a model whether each claim of the rendering is backed by the facts, one call per rendering.
It catches what a lookup cannot: a claim of degree, a distorted meaning, a change described as delivered that a later pull request removed.
It is on by default, and `faithfulness.thorough: false` turns it off ([Configuration](configuration.md#faithfulness)).
It can run on another model than the rendering, with `faithfulness.model`.
So you can render with one model and check with another, at a different cost.

## Does the thorough check earn its tokens?

The line under the thorough check answers it for each run:

```text
    caught 4 claims the rule-based check missed, for 22,765 tokens in 2 calls
```

It pairs the claims **only** the thorough check caught with the tokens the check spent to catch them.
Claims both checks found are not counted, because the thorough check added nothing on those.
In this run the check spent 22,765 of the run's 45,452 tokens, about half, and found four claims the free check did not.

Read the line across several of your releases, not one:

- **Its flags identify changes you would make:** compare that review value with the check's cost and keep it on when it helps.
- **It adds no useful findings across your releases:** consider using the rule-based check alone, with your own reading of the output.

Then read what it flagged, because a count says nothing about whether a flag is justified.
A check that flags a lot of supported passages costs you reading time on top of its tokens.
In the [comparison of 2026-09-25](#the-model-comparison-of-2026-09-25), the rule-based check reported no flags in 42 complete runs, and every flag came from the thorough check.
That observation alone does not establish the thorough check's accuracy or value.

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

## The provider comparison of 2026-10-09

The October comparison in [chartula-evals](https://github.com/goldbarth/chartula-evals) ran OpenAI and Anthropic against the same four real releases.
The [captured run measurements](assets/expectations/october-runs.csv), [summaries](assets/expectations/october-summary.csv) and [source provenance](assets/expectations/provenance.json) retain its figures, model settings, source paths and checksums.
The original comparison report is `sweeps/cmp-2026-10-stats/report.md` in that repository.
[What to expect from Chartula](what-to-expect.md) presents the charts and a generated entry for each audience.

All 84 attempts used Chartula `0.1.0-preview.5+541842d`, the same prompt, `title-and-description`, technical and customer audiences, and the thorough check on.
Within each scenario, both providers used identical facts and release commits.
Every configuration ran three times and used each model's lowest accepted thinking setting: disabled for Luna and Haiku, low for Sol, Astra, Sonnet and Opus.
Premium writers were measured only on the typical and sparse-description releases.

### Typical release: writing and checking together

This was ServiceDeskLite `v1.9.0`, with 19 pull requests, 18 facts and 17 entries per audience.
The first model writes and the second checks.
Cost is USD at the historical list prices without cache; cost and duration are medians of complete runs, with their observed min-max range.
Completion counts include every attempt, including those in which Chartula refused an audience's answer.

| Provider | Writing / checking | Complete / attempts | Cost $ (range) | Duration s (range) |
| --- | --- | ---: | ---: | ---: |
| OpenAI | Luna / Luna | 3/3 | 0.0055 (0.0053-0.0057) | 29.5 (23.0-30.9) |
| OpenAI | Luna / Sol | 3/3 | 0.0511 (0.0511-0.0534) | 33.2 (31.1-33.5) |
| OpenAI | Sol / Sol | 3/3 | 0.1071 (0.1065-0.1092) | 54.0 (50.3-54.6) |
| OpenAI | Astra / Sol | 3/3 | 0.3586 (0.3559-0.3594) | 52.5 (50.2-54.3) |
| Anthropic | Haiku / Haiku | 2/3 | 0.0103 (0.0102-0.0104) | 36.6 (36.6-36.7) |
| Anthropic | Haiku / Sonnet | 3/3 | 0.0857 (0.0854-0.0865) | 29.8 (28.7-31.0) |
| Anthropic | Sonnet / Sonnet | 3/3 | 0.1824 (0.1817-0.1851) | 36.5 (36.0-38.6) |
| Anthropic | Opus / Sonnet | 2/3 | 0.2694 (0.2679-0.2708) | 42.1 (41.8-42.4) |

Sol here is `gpt-6.1-sol`, and the three Anthropic ids end in `5-5`.
[Providers](providers.md#choosing-a-model) lists the full model ids.
The two 2/3 rows use two observations for their medians.
With Luna / Sol and Haiku / Sonnet, checking accounted for about 94% of the mean cost.
Changing only the writer's price tier can therefore leave most of the bill intact.

### Completion across the releases

| Writing / checking | Small | Typical | Large | Sparse descriptions |
| --- | ---: | ---: | ---: | ---: |
| Luna / Luna | 3/3 | 3/3 | 3/3 | 0/3 |
| Luna / Sol | 3/3 | 3/3 | 3/3 | 0/3 |
| Sol / Sol | 3/3 | 3/3 | 3/3 | 3/3 |
| Astra / Sol | not measured | 3/3 | not measured | 3/3 |
| Haiku / Haiku | 3/3 | 2/3 | 3/3 | 2/3 |
| Haiku / Sonnet | 2/3 | 3/3 | 3/3 | 3/3 |
| Sonnet / Sonnet | 3/3 | 3/3 | 3/3 | 3/3 |
| Opus / Sonnet | not measured | 2/3 | not measured | 3/3 |

The sparse-description release was Ingestor `v3.2.0`: 15 pull requests, ten facts, only two with descriptions.
The two Luna writer configurations produced no complete run there; Haiku / Haiku produced two.
Sol / Sol and Sonnet / Sonnet completed every measured scenario, twelve runs each.
The ten incomplete attempts still made model calls and spent tokens; their smaller bills are not prices for a complete release.

### What these figures support

The cheap writer/checker pairs had lower complete-run costs than the middle-model pairs in every scenario in which both completed.
For the typical release, Haiku / Sonnet cost less than Sonnet / Sonnet and completed all three attempts; on the small release it lost an audience in one attempt.
Opus / Sonnet cost more than Sonnet / Sonnet on the typical release, completed fewer attempts there, and has no measured small or large release.
Those observations do not establish that paying more buys better text or a more accurate check.

Start by checking completion on a release like yours, then compare the cost of writing and checking together.
If completion across these scenarios is your priority, Sol / Sol and Sonnet / Sonnet are measured starting points, with no guarantee for another release.
Keep descriptions when they contain useful source information, and use `preview` to inspect what your release will send before choosing a model.
Use flags to guide your reading; choose whether the thorough check earns its cost from the findings you confirm in your own workflow.

Three repeats per configuration describe these releases, not reliable failure probabilities or confidence intervals.
The texts were not scored and flags were not judged.
Each configuration generated new text, so changing the checker did not isolate the checker alone.
The models use different accepted thinking settings and tokenizers; this compares complete configurations, not equally capable models.
The September comparison below used an earlier version and prompt and cannot serve as an extra October repetition.
Neither historical price calculation is a current quote or an invoice verification.

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
- This September comparison did not include Claude models.
  [What to expect from Chartula](what-to-expect.md) adds the October measurements for OpenAI and Anthropic across four release scenarios.

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

The flag ranges overlap, so these three repetitions do not establish an effect of thinking on flag counts or text quality.
Thinking raised duration and cost.
With `gpt-6-sol`, `high` cost 62% more than `disabled` and took 4.3 times as long.
A run rendered by `gpt-6-luna` cost about half of one rendered by `gpt-6-sol`, and of its $0.051 the check on `gpt-6-sol` took $0.048.
For these September models, `thinking: disabled` is a cost-conscious starting point; with a cheap rendering model the thorough check is most of the bill.

### Which model checks

Rendering and checking on `gpt-6-luna` and `gpt-6-sol` in every combination, `thinking: disabled`:

| Rendering model | Checked by `gpt-6-luna` | Checked by `gpt-6-sol` |
| --- | --- | --- |
| `gpt-6-luna` | 6 flags per run | 4 (3-4) |
| `gpt-6-sol` | 5 (4-8) | 3 (3-4) |

The configurations using `gpt-6-luna` to check reported more flags.
`gpt-6-luna` as the checker flagged more, but at least 7 of its 35 flags said in their own reasoning that the passage was supported; none of `gpt-6-sol`'s 88 flags did.
`gpt-6-luna` checked for $0.0025 per run, against $0.048 for `gpt-6-sol`.
The contradictory reasons are observations from the earlier evaluation's reading of those flags, not a measured false-alarm rate.
Every configuration wrote new text, so these runs do not isolate the checker or establish which checker is more accurate.

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
Fewer flags here can reflect less source material; they do not establish better text.
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

These observations concern the two local configurations measured in September, not all local models.
Read a local run's output line by line before publishing it; these runs establish no local text-quality ranking.

### Recommendations

The September figures support choices about cost and input, with the October completion results above adding more release scenarios:

- **Assess the thorough check by useful findings.** It was the only source of flags in September, but flag counts alone do not establish its value.
- **Start with the lowest accepted thinking setting**, for writing and checking, when controlling cost and time matters.
  Additional thinking cost more and took longer in September; a text-quality benefit was not measured.
- **Compare whole configurations.** A cheaper writer can leave an expensive checker as most of the bill.
  Fewer flags do not identify the more accurate checker.
- **Try a cheaper writer on your own release and review the result.** Luna cost about $0.003 to write the September release, but both Luna writer configurations lost audiences on the October sparse-description case.
- **Keep the [fact base](glossary.md#fact-base) at `title-and-description`** unless your descriptions add nothing to the titles.

[Providers](providers.md#choosing-a-model) lists the model ids Chartula has been run with.
