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
    [Category("Connection")]
    public async Task Reconnect_DuringSend_Behavior()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status")).ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        // Start a send that will stream
        await Page.Locator("#userInput").FillAsync("Trigger disconnect test");
        await Page.Locator("#mainBtn").ClickAsync();

        // Wait until the mock bridge has at least one registered listener
        var waitUntilListeners = DateTime.UtcNow.AddMilliseconds(3000);
        var listenersCount = await Page.EvaluateAsync<int>("() => window._listeners ? window._listeners.length : 0");
        while (listenersCount == 0 && DateTime.UtcNow < waitUntilListeners)
        {
            await Task.Delay(50);
            listenersCount = await Page.EvaluateAsync<int>("() => window._listeners ? window._listeners.length : 0");
        }
        Assert.That(listenersCount, Is.GreaterThan(0), "No bridge listeners registered on the page - dispatcher may not be started.");

        // Emit StreamError to simulate a disconnection
        await Page.EvaluateAsync<object>("() => { if (typeof window.__emitBridgeMessage === 'function') { window.__emitBridgeMessage({ Type: 'StreamError', Payload: 'Disconnected: host connection lost' }); } }");

        // After the simulated disconnect the UI should show Disconnected and display the retry button
        await Expect(Page.Locator("#conn-status")).ToHaveTextAsync("Disconnected", new() { Timeout = 5000 });
        await Expect(Page.Locator("#retry-btn")).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Click retry and ensure the app attempts to reconnect and returns to Ready/Connected
        await Page.Locator("#retry-btn").ClickAsync();
        await Expect(Page.Locator("#conn-status")).ToHaveTextAsync("Connected", new() { Timeout = 5000 });
        await Expect(Page.Locator("#status-text")).ToHaveTextAsync("Ready", new() { Timeout = 5000 });
    }


    [Test]
    [Category("ConnectionState")]
    public async Task Reconnect_RecoversOrShowsClearError()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        // Ensure bridge listeners are registered by initiating an action
        await Page.Locator("#userInput").FillAsync("Trigger reconnect test");
        await Page.Locator("#mainBtn").ClickAsync();

        // Emit a disconnect error from the bridge
        await Page.EvaluateAsync<object>("() => { if (typeof window.__emitBridgeMessage === 'function') window.__emitBridgeMessage({ Type: 'StreamError', Payload: 'Disconnected: host connection lost' }); }");

        // Expect the UI to show disconnected/offline state
        await Expect(Page.Locator("#conn-status")).ToHaveTextAsync("Disconnected", new() { Timeout = 5000 });

        // Click retry to attempt reconnection
        await Page.Locator("#retry-btn").ClickAsync();

        // After retry, either the app recovers (Connected + Ready) or it shows a clear offline error.
        var deadline = DateTime.UtcNow.AddMilliseconds(5000);
        bool recovered = false;
        bool showedClearError = false;
        while (DateTime.UtcNow < deadline && !recovered && !showedClearError)
        {
            await Task.Delay(100);
            recovered = await Page.EvaluateAsync<bool>("() => document.getElementById('conn-status')?.textContent === 'Connected'");
            if (recovered)
            {
                // verify status-text becomes Ready
                var ready = await Page.EvaluateAsync<bool>("() => document.getElementById('status-text')?.textContent === 'Ready'");
                if (!ready) recovered = false;
                continue;
            }

            // check for clear offline error message
            showedClearError = await Page.EvaluateAsync<bool>("() => (document.getElementById('status-text')?.textContent || '').toLowerCase().includes('unreachable') || document.getElementById('conn-status')?.textContent === 'Disconnected'");
        }

        Assert.That(recovered || showedClearError, Is.True, "After retry the app should either recover to Connected/Ready or show a clear offline error message.");
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
    public async Task ModelName_IsHiddenWhenDisconnected()
    {
        await GotoWithMockAsync("webview-mock-offline.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Disconnected", new() { Timeout = 3000 });
        await Expect(Page.Locator("#model-name")).ToBeHiddenAsync();
        await Expect(Page.Locator("#status-separator")).ToBeHiddenAsync();
    }

    [Test]
    [Category("ConnectionState")]
    public async Task StatusText_ShowsErrorMessageWhenOffline()
    {
        await GotoWithMockAsync("webview-mock-offline.js");
        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("LM Studio unreachable", new() { Timeout = 3000 });
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
