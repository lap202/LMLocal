namespace LMLocal.Tests.E2E;

/// <summary>
/// Manual E2E tests to validate GitHub Flavored Markdown (GFM) rendering in the UI.
/// These are explicit/manual tests and should be run via --where "cat==GFM" or similar filters.
/// </summary>
[TestFixture]
[Explicit("Explicit GFM rendering tests. Run manually via --filter 'Category=GFM'")]
public class GfmMarkdownTests : PageTest
{
    private static string ReadMock(string fileName) =>
        File.ReadAllText(Path.GetFullPath($"TestAssets/{fileName}"));

    private const string TestPageUrl = "https://app.local/test-app.html";

    public override BrowserNewContextOptions ContextOptions() => new() { BypassCSP = true };

    private async Task GotoWithMockAsync(string mockFileName)
    {
        var assetsDir = Path.GetFullPath("TestAssets");
        var resourcesDir = Path.GetFullPath(@"..\..\..\..\LMLocal\Resources");

        await Context.RouteAsync("https://app.local/**", async route =>
        {
            var urlPath = new Uri(route.Request.Url).AbsolutePath.TrimStart('/');

            if (urlPath == "test-app.html")
            {
                var testHtmlPath = Path.Combine(assetsDir, "test-app.html");
                if (!File.Exists(testHtmlPath))
                {
                    testHtmlPath = Path.GetFullPath(@"..\..\..\..\LMLocal\Resources\app.html");
                }
                await route.FulfillAsync(new() { Path = testHtmlPath });
                return;
            }

            var inAssets = Path.Combine(assetsDir, Path.GetFileName(urlPath));
            if (File.Exists(inAssets))
            {
                await route.FulfillAsync(new() { Path = inAssets });
                return;
            }

            var inResources = Path.Combine(resourcesDir, urlPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(inResources))
            {
                await route.FulfillAsync(new() { Path = inResources });
                return;
            }

            await route.AbortAsync();
        });

        await Page.AddInitScriptAsync(ReadMock(mockFileName));
        await Page.GotoAsync(TestPageUrl);
    }

    [Test]
    [Category("GFM")]
    public async Task GfmMarkdown_RendersHeadingsTablesTasksLinksAndCode()
    {
        await GotoWithMockAsync("webview-mock-gfm.js");
        // Wait for connection to transition away from the transient "Connecting" state.
        // Some environments may show "Connecting" for a while; poll until either the
        // text becomes "Connected" or the element gains the 'online' class.
        await Page.WaitForFunctionAsync(
            "() => { const el = document.querySelector('#conn-status'); if (!el) return false; return el.textContent.trim() === 'Connected' || el.classList.contains('online'); }",
            new PageWaitForFunctionOptions { Timeout = 15000 }
        );

        // Finally assert we have the expected text
        await Expect(Page.Locator("#conn-status")).ToHaveTextAsync("Connected", new() { Timeout = 2000 });

        // Trigger generation
        await Page.Locator("#userInput").FillAsync("Render GFM");
        await Page.Locator("#mainBtn").ClickAsync();

        // Wait for generation to complete
        await Expect(Page.Locator("#status-text")).ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        // Headings
        await Expect(Page.Locator(".ai-message h1")).ToHaveTextAsync("Heading 1", new() { Timeout = 2000 });
        await Expect(Page.Locator(".ai-message h2")).ToHaveTextAsync("Heading 2", new() { Timeout = 2000 });

        // Table
        var table = Page.Locator(".ai-message table");
        await Expect(table).ToBeVisibleAsync();
        var cellText = await table.Locator("td").Nth(0).InnerTextAsync();
        Assert.That(cellText.Trim(), Is.EqualTo("a"));

        // Task list: expect two checkboxes and first checked
        var tasks = Page.Locator(".ai-message input[type=checkbox]");
        var taskCount = await tasks.CountAsync();
        Assert.That(taskCount, Is.GreaterThanOrEqualTo(2), "Expected at least two task checkboxes");
        var firstChecked = await tasks.Nth(0).IsCheckedAsync();
        Assert.That(firstChecked, Is.True, "First task should be checked");

        // Strikethrough should render as del or s
        var delExists = await Page.Locator(".ai-message del, .ai-message s").CountAsync();
        Assert.That(delExists, Is.GreaterThan(0), "Strikethrough should render as <del> or <s>");

        // Autolink should render as anchor
        var link = Page.Locator(".ai-message a[href='https://example.com']");
        await Expect(link).ToBeVisibleAsync();

        // Code fence should render inside pre > code
        await Expect(Page.Locator(".ai-message pre code")).ToContainTextAsync("console.log('hi')");

        // Emphasis and strong
        var emExists = await Page.Locator(".ai-message em, .ai-message strong, .ai-message b, .ai-message i").CountAsync();
        Assert.That(emExists, Is.GreaterThan(0), "Emphasis/strong should be rendered");
    }

    [Test]
    [Category("GFM")]
    public async Task GfmMarkdown_RendersComplexGfmFeatures()
    {
        await GotoWithMockAsync("webview-mock-gfm-complex.js");

        // Wait for connection to transition away from the transient "Connecting" state.
        await Page.WaitForFunctionAsync(
            "() => { const el = document.querySelector('#conn-status'); if (!el) return false; return el.textContent.trim() === 'Connected' || el.classList.contains('online'); }",
            new PageWaitForFunctionOptions { Timeout = 15000 }
        );
        await Expect(Page.Locator("#conn-status")).ToHaveTextAsync("Connected", new() { Timeout = 2000 });

        // Trigger generation
        await Page.Locator("#userInput").FillAsync("Render complex GFM");
        await Page.Locator("#mainBtn").ClickAsync();

        // Wait for generation to complete
        await Expect(Page.Locator("#status-text")).ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        // Setext heading should render as h1
        await Expect(Page.Locator(".ai-message h1")).ToHaveTextAsync("Setext Heading", new() { Timeout = 2000 });

        // Nested lists: ensure there is at least one nested <ul> inside first ordered list item
        var nestedCount = await Page.Locator(".ai-message ol > li > ul > li").CountAsync();
        Assert.That(nestedCount, Is.GreaterThan(0), "Expected nested list items to be rendered");

        // Blockquote rendered
        await Expect(Page.Locator(".ai-message blockquote")).ToBeVisibleAsync();

        // Inline code present - pick the first inline code element to avoid strict-mode errors
        var inlineCode = await Page.Locator(".ai-message code").Nth(0).InnerTextAsync();
        Assert.That(inlineCode, Does.Contain("const x = 1"));

        // Fenced code block
        await Expect(Page.Locator(".ai-message pre code")).ToContainTextAsync("print('hello')");

        // Image rendered with correct src and alt
        await Expect(Page.Locator(".ai-message img[alt='Alt text']")).ToBeVisibleAsync();

        // Reference link resolved
        await Expect(Page.Locator(".ai-message a[href='https://github.com']")).ToBeVisibleAsync();

        // Autolink rendered as anchor
        await Expect(Page.Locator(".ai-message a[href='https://example.org']")).ToBeVisibleAsync();

        // Table cell content
        var firstCell = await Page.Locator(".ai-message table td").Nth(0).InnerTextAsync();
        Assert.That(firstCell.Trim(), Is.EqualTo("L"));
    }
}
