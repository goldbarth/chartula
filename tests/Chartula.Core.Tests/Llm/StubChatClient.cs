using Microsoft.Extensions.AI;

namespace Chartula.Core.Tests.Llm;

/// <summary>
/// A stand-in <see cref="IChatClient"/> that records the messages it receives and
/// returns a fixed response. It lets the tests prove the seam works over an
/// arbitrary provider, with no live LLM call.
/// </summary>
internal sealed class StubChatClient(string responseText) : IChatClient
{
    public IReadOnlyList<ChatMessage>? LastMessages { get; private set; }

    public int CallCount { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        LastMessages = messages.ToList();
        CallCount++;
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Streaming is not exercised by these tests.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
