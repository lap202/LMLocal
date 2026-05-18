namespace LMLocal.Tests.E2E.AppTests;

[TestFixture]
public class ConnectionStateTests : AppTestBase
{
    [Test]
    [Category("ConnectionState")]
    public async Task ConnStatus_ShowsConnectedAfterSuccessfulInit()
    {
        await GotoWithMockAsync("webview-mock.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });
        await Expect(Page.Locator("#conn-status")).ToHaveClassAsync(Online);
    }

    [Test]
    [Category("ConnectionState")]
    public async Task ConnStatus_ShowsDisconnectedOnError()
    {
        await GotoWithMockAsync("webview-mock-offline.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Disconnected", new() { Timeout = 3000 });
        await Expect(Page.Locator("#retry-btn")).ToBeVisibleAsync(new() { Timeout = 3000 });
    }

    [Test]
    [Category("ConnectionState")]
    public async Task ModelName_IsVisibleAfterConnected()
    {
        await GotoWithMockAsync("webview-mock.js");
        await Expect(Page.Locator("#model-name"))
            .ToHaveTextAsync("Test Model", new() { Timeout = 3000 });
        await Expect(Page.Locator("#model-name")).ToBeVisibleAsync();
        await Expect(Page.Locator("#status-separator")).ToBeVisibleAsync();
    }

    [Test]
    [Category("ConnectionState")]
    public async Task ModelName_ShowsPlaceHolderWhenDisconnected()
    {
        await GotoWithMockAsync("webview-mock-offline.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Disconnected", new() { Timeout = 3000 });

        await Expect(Page.Locator("#model-name"))
            .ToHaveTextAsync("Select model...", new() { Timeout = 3000 });
        await Expect(Page.Locator("#model-name")).ToBeVisibleAsync();
        await Expect(Page.Locator("#status-separator")).ToBeVisibleAsync();
    }

    [Test]
    [Category("ConnectionState")]
    public async Task StatusText_ShowsErrorMessageWhenOffline()
    {
        await GotoWithMockAsync("webview-mock-offline.js");
        // The status text may include a prefix (e.g. "Not ready | ") so assert it contains
        // the expected error message rather than matching the whole string exactly.
        await Expect(Page.Locator("#status-text"))
            .ToContainTextAsync("LM Studio unreachable", new() { Timeout = 3000 });
    }

    [Test]
    [Category("StatusBar")]
    public async Task StatusText_ShowsReadyOnLoad()
    {
        await GotoWithMockAsync("webview-mock.js");
        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 3000 });
    }
}
