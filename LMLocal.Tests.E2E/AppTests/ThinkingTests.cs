namespace LMLocal.Tests.E2E.AppTests;

[TestFixture]
public class ThinkingTests : AppTestBase
{
    [Test]
    [Category("Thinking")]
    public async Task StreamThought_ShowsThoughtsInThoughtBlock()
    {
        await GotoWithMockAsync("webview-mock-thinking.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator(".thought-content")).ToHaveTextAsync("This is a thought", new() { Timeout = 5000 });
    }

    [Test]
    [Category("Thinking")]
    public async Task ThoughtBlock_IsNotShownWhenNoStreamThoughtIsSent()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        // Wait for chat to complete and return to idle/ready state
        // The thought-container should be hidden if no StreamThought was sent
        await Expect(Page.Locator(".thought-container")).ToBeHiddenAsync(new() { Timeout = 5000 });

        var thoughtCount = await Page.Locator(".thought-content").CountAsync();
        if (thoughtCount > 0)
        {
            var txt = await Page.Locator(".thought-content").InnerTextAsync();
            Assert.That(string.IsNullOrWhiteSpace(txt), Is.True, "Thought content should be empty when no StreamThought was emitted");
        }
    }

    [Test]
    [Category("Thinking")]
    public async Task ThoughtBlock_CanExpandAndCollapse()
    {
        await GotoWithMockAsync("webview-mock-thinking.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator(".thought-content")).ToHaveTextAsync("This is a thought", new() { Timeout = 5000 });

        var toggle = Page.Locator(".thought-container .toggle-thought-btn");
        await Expect(toggle).ToBeVisibleAsync();

        var initiallyExpanded = await Page.Locator(".thought-content").EvaluateAsync<bool>("el => el.classList.contains('expanded')");
        Assert.That(initiallyExpanded, Is.False, "Thought content should not be expanded initially");

        await toggle.ClickAsync();
        var afterExpand = await Page.Locator(".thought-content").EvaluateAsync<bool>("el => el.classList.contains('expanded')");
        Assert.That(afterExpand, Is.True, "Thought content should have 'expanded' after clicking toggle");

        await toggle.ClickAsync();
        var afterCollapse = await Page.Locator(".thought-content").EvaluateAsync<bool>("el => el.classList.contains('expanded')");
        Assert.That(afterCollapse, Is.False, "Thought content should not have 'expanded' after clicking toggle again");
    }
}
