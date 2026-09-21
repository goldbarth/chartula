using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Serialization;
using Chartula.Infrastructure.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// Where a run's files come from. The pipeline asks for ports; an output the
/// composition root forgets to bind is a run that fails on the last step, after
/// everything it costs has already been spent.
/// </summary>
public sealed class OutputWiringTests
{
    // The LLM options are registered by AddChartulaLlm in a real run; changelog.json
    // records the model from them.
    private static ServiceProvider Build()
        => new ServiceCollection().AddSingleton(new LlmOptions()).AddChartulaOutputs().BuildServiceProvider();

    [Fact]
    public void Every_local_output_resolves_to_a_writer_that_puts_files_on_disk()
    {
        ServiceProvider services = Build();

        Assert.IsType<FileChangelogJsonWriter>(services.GetRequiredService<IChangelogJsonWriter>());
        Assert.IsType<FileChangelogMarkdownWriter>(services.GetRequiredService<IChangelogMarkdownWriter>());
        Assert.IsType<FileCustomerPageWriter>(services.GetRequiredService<ICustomerPageWriter>());
    }
}
