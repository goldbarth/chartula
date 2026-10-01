# 4. The domain talks to models through `IChatClient` alone

- **Status:** Accepted
- **Decided:** 2026-07-16 (#41, for #2)
- **Recorded:** 2026-10-01 (#319)

## Context

Chartula needs a model for two operations: writing the entries of each rendering, and the thorough check.
Both ask for structured output, a list of entries or a verdict.

Providers differ in their SDK, their endpoint and their dialect, and the product promises more than one: Anthropic, any endpoint that speaks OpenAI's dialect, and a local runner when that path proves too weak (#75).
A domain that referenced a provider SDK would let any change to the pipeline depend on that provider, and would let any test of the domain reach a real model by one wrong line.
A test that reaches a model costs tokens, needs a key, and passes or fails with the model's mood.

## Decision

- `Chartula.Core` references `Microsoft.Extensions.AI` and no provider package.
  The pipeline depends on `IChangelogModel`; its one implementation, `ChatModel`, sends its requests to an `IChatClient` and knows no provider.
- The provider packages, `Anthropic` and `Microsoft.Extensions.AI.OpenAI`, are referenced by `Chartula.Cli` alone.
  `Cli/Composition` builds the `IChatClient` for the configured provider (`LlmServiceCollectionExtensions.CreateChatClient`), and it is the only code that knows one.
- What the domain asks of a model is expressed in `Microsoft.Extensions.AI` terms: structured output through `GetResponseAsync<T>`, thinking through `ReasoningOptions`, usage through `UsageDetails`.
  Knowledge about one provider, such as which thinking modes a Claude model rejects (`ClaudeThinkingSupport`), lives in `Cli/Composition` too.

## Consequences

- No test in `Chartula.Core.Tests` can reach a real model: the project references no assembly that could make the call.
  That holds by construction, not by discipline, and the stand-in models in `FixtureModels.cs` fill the seam ([Testing](../testing.md#stand-in-models)).
- `Chartula.Cli.Tests` does reference the provider SDKs, on purpose: it tests what each SDK sends and puts into an error, with an `HttpMessageHandler` in place of the network.
  There, staying offline is a rule of the tests, not of the references ([Testing](../testing.md#chartulaclitests)).
- A provider joins in the composition root and the configuration.
  The OpenAI-compatible provider (#88) changed no file in `Chartula.Core`.
- The domain can use only what `IChatClient` expresses.
  A provider feature beyond it is set in the composition root, or not used.
- A provider package is judged on trimming as well as on whether it works, because a native-AOT build is a goal ([0002](0002-dependencies-that-keep-native-aot-reachable.md)).
- A provider package referenced from `Chartula.Core` or `Chartula.Infrastructure` starts as an issue, not a pull request.
