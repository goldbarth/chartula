# Run metrics

Every `preview` and `generate` run ends with a summary of what it did and what it cost.
It is printed unconditionally: measuring a run should never be something you have to remember to turn on.
A `generate` run also keeps it in a local file, so runs can be compared after their output is gone - see [Run record](run-record.md).

```text
Run metrics
  Rule-based check: 3 runs, 1 with findings, 1 claim, no tokens
  Thorough check:   3 runs, 2 with findings, 2 claims, 6,230 in / 130 out, 14.2 s (longest 6.1 s)
    of which 4,096 in cached, 40 out reasoning
    caught 1 claim the rule-based check missed, for 6,360 tokens in 3 calls
  Rephrasing:       3 calls, 5,437 in / 859 out, 41.0 s (longest 22.1 s)
    of which 0 in cached, 212 out reasoning
  Total:            12,656 tokens in 1 min 4 s
  Retries:          1 (rephrasing 1, thorough check 0)
```

## What the numbers mean

| Line | Reading |
| --- | --- |
| `runs` | How often the check was asked to run - once per rendered audience. |
| `with findings` | How many of those runs flagged at least one claim, the check's hit rate. |
| `claims` | How many claims the check flagged in total. |
| `in / out` | Tokens sent to and produced by the model, attributed to that operation. |
| `of which ... cached` | The part of the input the provider served from its prompt cache, which most providers bill at a lower rate. |
| `of which ... reasoning` | The part of the output the model spent reasoning rather than writing - billed as output, never seen in the changelog. |
| time, `longest` | How long that operation's calls took together, retries included, and the longest single call. |
| `in <time>` | How long the whole run took, from reading history to the last model call. |
| `Retries` | Requests sent again after the first, per operation - see below. |

Providers break these out differently.
OpenAI and compatible endpoints that follow it report both; Anthropic reports cache reads but folds thinking into the output without a separate count.
What a provider does not report is shown as `not reported`, never as zero: zero reasoning would claim the model did not think.
Either way the `out` figure is complete - reasoning is inside it, not on top of it.

## When a run was slow

A slow run has three usual causes, and the summary tells them apart:

- **One slow call** - the `longest` time is most of the operation's time. A long release, or a model thinking at length.
- **Uniformly slow calls** - the `longest` time is close to the average. A slow model or endpoint.
- **Retried calls** - `Retries` above zero. The provider was overloaded, rate-limited the key, or a request timed out and was sent again. Every provider client does this on its own, and without the count a retried call looks like one slow call.

Retries are counted from the requests the transport actually sends, the same way for every provider.
`not observed` means they could not be counted, which is not the same as none: it appears only for a model client Chartula did not build itself.

A call that ended in an error rather than an answer is listed under its operation as `failed without an answer`.
Its time is in the totals, its tokens are not - there were none to report.

The rule-based check makes no LLM call, so it always costs no tokens.

A thorough check with runs but no calls was toggled off, or had nothing to check.
See [`configuration.md`](configuration.md) for the `faithfulness.thorough` toggle.

## When a check verified nothing

A thorough check reaches the model and can still come back with an answer that cannot be read.
Those runs get a line of their own:

```text
  Thorough check:   3 runs, 0 with findings, 0 claims, 6,230 in / 130 out
    3 of 3 runs came back unreadable and verified nothing
```

Read it as tokens spent for no verification, not as a clean bill of health.
The same runs are flagged on each affected audience text, so review mode shows them too.

The likeliest cause is a model that does not hold to the requested response format, which is worth knowing before trusting a provider or a smaller model with the check.
Without this line the run would report `0 with findings`, which is what a genuinely clean check looks like.

## When the prompt never arrived

A different failure does not produce a line here at all: it fails the run.
An endpoint can silently cut a prompt to fit its context window and answer from what is left, and a provider that enforces its response schema by constrained decoding still returns a well-formed, often clean, verdict from a model that never saw the facts.
That verdict is not a check that passed - it is a check that never happened, and it looks exactly like a clean one.

Rendering is exposed to it the same way: a cut prompt still yields entries, written from the facts and rules the model kept, and a changelog built from a third of a release reads as if it were all of it.

Chartula catches the cases where usage is reported, for every model call - rendering and thorough check alike: the characters sent bound the token count from below, so a reported `prompt_tokens` far under that bound is proof the prompt was cut, not suspicion.
A cut rendering fails its audience, and a cut check fails the run, each with an error naming the endpoint's context window as the cause.
See ["The context window is the first thing to get right"](configuration.md#the-context-window-is-the-first-thing-to-get-right) for the fix.

An endpoint that reports the untruncated length regardless of what it actually processed, or reports no usage at all, gives nothing to detect this way - that gap is real, not closed by this check.
Neither is a model that saw every fact and judged badly: no property of the call shows that, which is why the rule-based check always runs.

## Judging whether the thorough check earns its cost

The indented line under the thorough check is the whole point of this summary.

It pairs the claims **only** the thorough check caught - the ones the free rule-based check missed - with the tokens the check spent to catch them.
Claims both checks find are not counted there: the thorough check adds nothing on those.

Read across several real releases:

- Consistently caught claims for the tokens spent: the check earns its cost, keep it on.
- Consistently 0 claims caught: the rule-based check is already finding everything, and the tokens buy nothing.

That is the judgement the defaults should be decided by, rather than guesswork.
