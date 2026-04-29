namespace LMLocal.Tests.E2E.AppTests;

[TestFixture]
public class StreamingTests : AppTestBase
{
    [Test]
    [Category("Chat")]
    public async Task StatusText_ShowsGeneratingWhileStreaming()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToContainTextAsync(ThinkingOrGenerating, new() { Timeout = 3000 });
    }

    [Test]
    [Category("Stream")]
    public async Task Stream_RendersAllChunks_IncludingTail()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        await Expect(Page.Locator(".ai-message")).ToContainTextAsync("console.log(\"hello\");", new() { Timeout = 3000 });
    }

    [Test]
    [Category("Loading")]
    public async Task LoadingIndicator_IsShownWhileWaitingForFirstChunk()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        var visibleCount = await Page.Locator(".loading-indicator:visible").CountAsync();
        Assert.That(visibleCount, Is.GreaterThan(0), "At least one loading indicator should be visible while waiting for first chunk");
    }

    [Test]
    [Category("Loading")]
    public async Task LoadingIndicator_IsReplacedByContentAfterChunkReceived()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        var visibleAfter = await Page.Locator(".loading-indicator:visible").CountAsync();
        Assert.That(visibleAfter, Is.EqualTo(0), "No loading indicators should be visible after stream completes");
    }
}
