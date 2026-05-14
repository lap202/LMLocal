namespace LMLocal.Tests.E2E.AppTests;

[TestFixture]
public class StreamErrorAndStopTests : AppTestBase
{
    [Test]
    [Category("StreamError")]
    public async Task StreamError_ShowsErrorInStatusBar()
    {
        await GotoWithMockAsync("webview-mock-stream-error.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToContainTextAsync("model crashed", new() { Timeout = 5000 });
    }

    [Test]
    [Category("StreamError")]
    public async Task StreamError_RemovesEmptyAiMessage()
    {
        await GotoWithMockAsync("webview-mock-stream-error.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToContainTextAsync("model crashed", new() { Timeout = 5000 });

        var aiCountAfterError = await Page.Locator(".ai-message").CountAsync();
        if (aiCountAfterError > 0)
        {
            var aiText = await Page.Locator(".ai-message").First.InnerTextAsync();
            Assert.That(string.IsNullOrWhiteSpace(aiText), Is.False, "If an AI message remains after a stream error it must contain an error/stop message, not be empty");
        }
    }

    [Test]
    [Category("StreamError")]
    public async Task StreamError_RestoresSendButton()
    {
        await GotoWithMockAsync("webview-mock-stream-error.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToContainTextAsync("model crashed", new() { Timeout = 5000 });

        await Expect(Page.Locator("#mainBtn")).ToHaveTextAsync("Send");
        await Expect(Page.Locator("#userInput")).ToBeEnabledAsync();
    }

    [Test]
    [Category("Stop")]
    public async Task Stop_ButtonRestoresToSendAfterStopping()
    {
        await GotoWithMockAsync("webview-mock-slow-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#mainBtn"))
            .ToHaveTextAsync("Stop", new() { Timeout = 3000 });

        await Page.Locator("#mainBtn").ClickAsync();

        // After stopping, the app sends ChatSessionComplete (or ChatSessionCancelled)
        // Wait for the session to complete/cancel, which triggers state reset to IDLE
        // This is indicated by button returning to "Send"
        await Page.WaitForFunctionAsync("() => document.querySelector('#mainBtn')?.textContent.trim() === 'Send'");
    }
}
