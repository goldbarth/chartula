# 3. Endpoints and credential names come from the environment only

- **Status:** Accepted
- **Date:** 2026-09-21 (#158, #166)

## Context

Chartula reads its configuration from `chartula.yaml` in the repository it runs in.
That file is repository content: anyone whose pull request is merged can change it.

Four settings decide where release data and credentials go: the model endpoint, the GitHub endpoint, and the names of the environment variables whose values are sent to them.
When the file could set them, one merged change could point the GitHub endpoint at any host and name any variable of the operator's environment as the token, and the next run would send that value there.

## Decision

- `llm.baseUrl`, `llm.apiKeyEnvironmentVariable`, `github.apiBaseUrl` and `github.tokenEnvironmentVariable` are read from the environment only, as `Chartula__Llm__BaseUrl` and so on.
- A `chartula.yaml` that sets one is refused at startup, naming the variable to use instead, rather than silently ignored.
- The environment is not loaded whole: besides `Chartula__` settings, Chartula reads only the default credential variables (`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `GITHUB_TOKEN`) and the ones the two name settings point at.
- Endpoints must use `https`, except on this machine, where local model servers run.
- Every run prints the endpoints and credential variable names in force, never their values (`EndpointNotice`).
- Credentials themselves are never read from a file.

## Consequences

- No repository content can redirect a credential; only whoever controls the environment can.
- Pointing Chartula at another endpoint takes an environment variable, not a line in the config file.
  [Running against your own endpoint](../configuration.md#running-against-your-own-endpoint) shows how.
- A new setting that decides where data or credentials go belongs in the environment-only list, not in `chartula.yaml`.
- A new credential variable has to be added to what `ChartulaConfiguration` reads, or the run will not see it.
