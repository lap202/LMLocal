namespace LMLocal.Tests.E2E.AppTests;

[TestFixture]
public class ClearChatTests : AppTestBase
{
    [Test]
    [Category("ClearChat")]
    public async Task ClearChat_RemovesAllMessagesFromContainer()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();
        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        await Page.Locator("#menu-btn").ClickAsync();
        await Page.Locator("#dropdown-menu button[data-action=\"clear-chat\"]").ClickAsync();

        var confirmDialog = Page.Locator("#confirm-dialog");
        await Expect(confirmDialog).ToBeVisibleAsync(new() { Timeout = 3000 });
        await Page.Locator("#confirm-dialog #dialog-confirm").ClickAsync();

        await Expect(Page.Locator("#chat-container > *")).ToHaveCountAsync(0);
    }

    [Test]
    [Category("ClearChat")]
    public async Task ClearChat_CancelledByUser_KeepsMessages()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();
        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        await Page.Locator("#menu-btn").ClickAsync();
        await Page.Locator("#dropdown-menu button[data-action=\"clear-chat\"]").ClickAsync();

        var confirmDialog = Page.Locator("#confirm-dialog");
        await Expect(confirmDialog).ToBeVisibleAsync(new() { Timeout = 3000 });
        await Page.Locator("#confirm-dialog #dialog-cancel").ClickAsync();

        await Expect(Page.Locator("#chat-container > *")).Not.ToHaveCountAsync(0);
    }
}
