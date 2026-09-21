using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Prompting;
using Chartula.Core.Serialization;

namespace Chartula.Cli.Tests.Composition;

public sealed class ProvenanceWiringTests
{
    [Fact]
    public void A_run_records_the_tool_version_the_configured_model_and_the_prompt_hash()
    {
        RunProvenance provenance = OutputServiceCollectionExtensions.Provenance(
            new LlmOptions { Provider = "openai-compatible", Model = "qwen3:8b", BaseUrl = "http://localhost:11434/v1" });

        Assert.False(string.IsNullOrWhiteSpace(provenance.ToolVersion));
        Assert.Equal("openai-compatible", provenance.Provider);
        Assert.Equal("qwen3:8b", provenance.Model);
        Assert.Equal(ChangelogPromptBuilder.PromptHash, provenance.PromptHash);
    }
}
