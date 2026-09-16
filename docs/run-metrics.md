# Run metrics

Every `preview` and `generate` run ends with a summary of what it did and what it cost.
It is printed unconditionally: measuring a run should never be something you have to remember to turn on.

```text
Run metrics
  Rule-based check: 3 runs, 1 with findings, 1 claim, no tokens
  Thorough check:   3 runs, 2 with findings, 2 claims, 6,230 in / 130 out
    caught 1 claim the rule-based check missed, for 6,360 tokens in 3 calls
  Rephrasing:       3 calls, 5,437 in / 859 out
  Total:            12,656 tokens
```

## What the numbers mean

| Line | Reading |
| --- | --- |
| `runs` | How often the check was asked to run - once per rendered audience. |
| `with findings` | How many of those runs flagged at least one claim, the check's hit rate. |
| `claims` | How many claims the check flagged in total. |
| `in / out` | Tokens sent to and produced by the model, attributed to that operation. |

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

Chartula catches the cases where usage is reported: the characters sent bound the token count from below, so a reported `prompt_tokens` far under that bound is proof the prompt was cut, not suspicion.
The run stops there with an error naming the endpoint's context window as the cause.
See ["The context window is the first thing to get right"](configuration.md#the-context-window-is-the-first-thing-to-get-right) for the fix.

An endpoint that reports the untruncated length regardless of what it actually processed gives nothing to detect this way - that gap is real, not closed by this check.

## Judging whether the thorough check earns its cost

The indented line under the thorough check is the whole point of this summary.

It pairs the claims **only** the thorough check caught - the ones the free rule-based check missed - with the tokens the check spent to catch them.
Claims both checks find are not counted there: the thorough check adds nothing on those.

Read across several real releases:

- Consistently caught claims for the tokens spent: the check earns its cost, keep it on.
- Consistently 0 claims caught: the rule-based check is already finding everything, and the tokens buy nothing.

That is the judgement the defaults should be decided by, rather than guesswork.
