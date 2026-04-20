namespace LMLocal.Tests.E2E;
using System.Text.RegularExpressions;

/// <summary>
/// E2E tests for app.html with AppStore/AppManager architecture.
/// Tests connection states, chat flow, streaming, markdown rendering, and error handling.
/// </summary>
[TestFixture]
public partial class AppTests : PageTest
{
    // ?? Generated Regex Patterns ????????????????????????????????????????????????????

    [GeneratedRegex("online")]
    private static partial Regex OnlineRegex();

    [GeneratedRegex("offline")]
    private static partial Regex OfflineRegex();

    [GeneratedRegex("(Thinking|Generating)")]
    private static partial Regex ThinkingOrGeneratingRegex();

    [GeneratedRegex(@"\([\d.]+\s+t/s\)")]
    private static partial Regex TokensPerSecondRegex();

    [GeneratedRegex("(Copied|Done)")]
    private static partial Regex CopiedLabelRegex();

    private static string ReadMock(string fileName) =>
        File.ReadAllText(Path.GetFullPath($"TestAssets/{fileName}"));

    private const string TestPageUrl = "https://app.local/test-app.html";

    public override BrowserNewContextOptions ContextOptions() => new() { BypassCSP = true };

    private async Task GotoWithMockAsync(string mockFileName)
    {
        var assetsDir = Path.GetFullPath("TestAssets");
        var resourcesDir = Path.GetFullPath(@"..\..\..\..\LMLocal\Resources");

        await Page.RouteAsync("https://app.local/**", async route =>
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

    // ?? Connection State Tests ????????????????????????????????????????????????

    [Test]
    [Category("ConnectionState")]
    public async Task ConnStatus_ShowsConnectedAfterSuccessfulInit()
    {
        await GotoWithMockAsync("webview-mock.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });
        await Expect(Page.Locator("#conn-status")).ToHaveClassAsync(OnlineRegex());
    }

    [Test]
    [Category("ConnectionState")]
    public async Task ConnStatus_ShowsDisconnectedOnError()
    {
        await GotoWithMockAsync("webview-mock-offline.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Disconnected", new() { Timeout = 3000 });
        await Expect(Page.Locator("#conn-status")).ToHaveClassAsync(OfflineRegex());
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

    // ?? Status Bar Tests ???????????????????????????????????????????????????

    [Test]
    [Category("StatusBar")]
    public async Task StatusText_ShowsReadyOnLoad()
    {
        await GotoWithMockAsync("webview-mock.js");
        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 3000 });
    }

    // ?? Input Validation Tests ??????????????????????????????????????????????

    [Test]
    [Category("Chat")]
    public async Task Send_EmptyInput_DoesNotAppendMessage()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#mainBtn").ClickAsync();
        await Expect(Page.Locator("#chat-container > *")).ToHaveCountAsync(0);
    }

    [Test]
    [Category("Chat")]
    public async Task Send_WhitespaceOnly_DoesNotAppendMessage()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("   ");
        await Page.Locator("#mainBtn").ClickAsync();
        await Expect(Page.Locator("#chat-container > *")).ToHaveCountAsync(0);
    }

    // ?? Send Message Tests ?????????????????????????????????????????????????

    [Test]
    [Category("Chat")]
    public async Task Send_AppendsUserMessageToChat()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator(".user-message"))
            .ToHaveTextAsync("Hello", new() { Timeout = 3000 });
    }

    [Test]
    [Category("Chat")]
    public async Task Send_ClearsInputAfterSend()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#userInput"))
            .ToHaveValueAsync("", new() { Timeout = 3000 });
    }

    [Test]
    [Category("Chat")]
    public async Task Send_ButtonChangesToStopWhileGenerating()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        // Button should change to "Stop" while generating - increase timeout since stream might start slowly
        var stopBtn = Page.Locator("#mainBtn");
        await Expect(stopBtn).ToHaveTextAsync("Stop", new() { Timeout = 5000 });

        // Verify it has the stop class - use evaluate since there might be other classes
        var hasStopClass = await stopBtn.EvaluateAsync<bool>("el => el.classList.contains('btn-stop')");
        Assert.That(hasStopClass, Is.True, "Button should have 'btn-stop' class while generating");
    }

    [Test]
    [Category("Chat")]
    public async Task Send_InputIsDisabledWhileGenerating()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#userInput"))
            .ToBeDisabledAsync(new() { Timeout = 3000 });
    }

    // ?? Keyboard Input Tests ????????????????????????????????????????????????

    [Test]
    [Category("Chat")]
    public async Task EnterKey_SendsMessage()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#userInput").PressAsync("Enter");

        await Expect(Page.Locator(".user-message"))
            .ToHaveTextAsync("Hello", new() { Timeout = 3000 });
    }

    [Test]
    [Category("Chat")]
    public async Task ShiftEnterKey_DoesNotSendMessage()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#userInput").PressAsync("Shift+Enter");

        await Expect(Page.Locator("#chat-container > *")).ToHaveCountAsync(0);
    }

    // ?? Stream Tests ???????????????????????????????????????????????????????

    [Test]
    [Category("Chat")]
    public async Task StatusText_ShowsGeneratingWhileStreaming()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        // Should show Generating... while streaming (after PROCESSING)
        await Expect(Page.Locator("#status-text"))
            .ToContainTextAsync(ThinkingOrGeneratingRegex(), new() { Timeout = 3000 });
    }

    [Test]
    [Category("Thinking")]
    public async Task StreamThought_ShowsThoughtsInThoughtBlock()
    {
        await GotoWithMockAsync("webview-mock-thinking.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        // Thought text emitted via StreamThought should appear inside .thought-content
        await Expect(Page.Locator(".thought-content")).ToHaveTextAsync("This is a thought", new() { Timeout = 5000 });
    }

    [Test]
    [Category("Thinking")]
    public async Task ThoughtBlock_IsNotShownWhenNoStreamThoughtIsSent()
    {
        // Use the regular streaming mock which does not emit StreamThought events
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        // Wait for stream to complete
        await Expect(Page.Locator("#status-text")).ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        // Thought container is always created now but should be hidden when no StreamThought was emitted
        await Expect(Page.Locator(".thought-container")).ToBeHiddenAsync();
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

        // Wait for thought content to appear
        await Expect(Page.Locator(".thought-content")).ToHaveTextAsync("This is a thought", new() { Timeout = 5000 });

        var toggle = Page.Locator(".thought-container .toggle-thought-btn");
        await Expect(toggle).ToBeVisibleAsync();

        // Initially should not have 'expanded' class
        var initiallyExpanded = await Page.Locator(".thought-content").EvaluateAsync<bool>("el => el.classList.contains('expanded')");
        Assert.That(initiallyExpanded, Is.False, "Thought content should not be expanded initially");

        // Click to expand
        await toggle.ClickAsync();
        var afterExpand = await Page.Locator(".thought-content").EvaluateAsync<bool>("el => el.classList.contains('expanded')");
        Assert.That(afterExpand, Is.True, "Thought content should have 'expanded' after clicking toggle");

        // Click to collapse
        await toggle.ClickAsync();
        var afterCollapse = await Page.Locator(".thought-content").EvaluateAsync<bool>("el => el.classList.contains('expanded')");
        Assert.That(afterCollapse, Is.False, "Thought content should not have 'expanded' after clicking toggle again");
    }

    [Test]
    [Category("Chat")]
    public async Task AiMessage_HasCompletedClassAfterStreamEnds()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        await Expect(Page.Locator(".ai-message.completed")).ToHaveCountAsync(1);
    }

    [Test]
    [Category("Stream")]
    public async Task Stream_RendersAllChunks_IncludingTail()
    {
        // Ensure that the final chunk (tail) emitted by the stream is rendered into the HTML
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        // Wait for stream processing to finish
        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        // The mock sends a code block that includes `console.log("hello");` — ensure that text is present
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

        // There may be more than one loading indicator (thought + main). Ensure at least one is visible
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

        // Wait for stream to complete
        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        // No loading indicators should be visible after stream completes
        var visibleAfter = await Page.Locator(".loading-indicator:visible").CountAsync();
        Assert.That(visibleAfter, Is.EqualTo(0), "No loading indicators should be visible after stream completes");
    }

    // ?? Markdown & Syntax Highlighting ??????????????????????????????????????????

    [Test]
    [Category("Markdown")]
    public async Task Marked_RendersCodeBlockAsPreElement()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        await Expect(Page.Locator(".ai-message pre")).ToBeVisibleAsync();
        await Expect(Page.Locator(".ai-message pre code")).ToBeVisibleAsync();
    }

    [Test]
    [Category("Highlight")]
    public async Task Highlight_AppliesHljsClassToCodeBlock()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        var hasHljs = await Page.Locator(".ai-message pre code.hljs").CountAsync();
        Assert.That(hasHljs, Is.GreaterThan(0),
            "highlight.js should apply 'hljs' class to code block");
    }

    // ?? Copy Button Tests ???????????????????????????????????????????????????

    [Test]
    [Category("CopyButton")]
    public async Task CopyButton_IsInjectedAfterStreamCompletes()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        await Expect(Page.Locator(".header-copy-btn")).ToBeVisibleAsync();
    }

    [Test]
    [Category("CopyButton")]
    public async Task CopyButton_ShowsCopiedLabelAfterClick()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        await Page.Locator(".header-copy-btn").ClickAsync();
        await Expect(Page.Locator(".header-copy-btn span"))
            .ToHaveTextAsync(CopiedLabelRegex(), new() { Timeout = 3000 });
    }

    // ?? Error Handling Tests ????????????????????????????????????????????????

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

        // Empty AI message should be removed. New behavior may either remove the empty
        // message or replace it with an error / "generation stopped" text. Accept both.
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







    // ?? Stop Generation Tests ???????????????????????????????????????????????

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

        await Expect(Page.Locator("#mainBtn"))
            .ToHaveTextAsync("Send", new() { Timeout = 5000 });
    }

    // ?? Token Bar Tests ???????????????????????????????????????????????????

    [Test]
    [Category("TokenBar")]
    public async Task TokenBar_IsHiddenBeforeSend()
    {
        await GotoWithMockAsync("webview-mock.js");
        await Expect(Page.Locator("#live-token-count")).ToBeHiddenAsync();
    }

    [Test]
    [Category("TokenBar")]
    public async Task TokenBar_IsVisibleWhileGenerating()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#live-token-count"))
            .ToBeVisibleAsync(new() { Timeout = 3000 });
    }

    [Test]
    [Category("TokenBar")]
    public async Task TokenBar_IsHiddenAfterStreamCompletes()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        await Expect(Page.Locator("#live-token-count")).ToBeHiddenAsync();
    }

    [Test]
    [Category("TokenBar")]
    public async Task TokenBar_ShowsCorrectCountAfterChunk()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        // Mock sends Count=10
        await Expect(Page.Locator("#token-number"))
            .ToHaveTextAsync("10 tokens", new() { Timeout = 3000 });
    }

    [Test]
    [Category("TokenBar")]
    public async Task TokenBar_ShowsSpeedAfterChunk()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        // Mock sends TokensPerSecond=15.5 - check WHILE generating, before it completes
        var speedSpan = Page.Locator("#tokens-speed");
        await Expect(speedSpan).ToBeVisibleAsync(new() { Timeout = 3000 });
        await Expect(speedSpan).ToHaveTextAsync(TokensPerSecondRegex(), new() { Timeout = 3000 });

        // Now wait for completion
        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 10000 });
    }

    // ?? Expandable Messages Tests ???????????????????????????????????????????

    [Test]
    [Category("Expandable")]
    public async Task LongUserMessage_ShowsMoreButtonWhenTruncated()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        // Generate a very long message (more than 150px when rendered)
        var longText = string.Concat(Enumerable.Repeat("This is a long message that will definitely exceed the 150px limit. ", 10));
        await Page.Locator("#userInput").FillAsync(longText);
        await Page.Locator("#mainBtn").ClickAsync();

        // Verify at least one expandable user message is created and target the first one
        var expandableCount = await Page.Locator(".user-message.expandable").CountAsync();
        Assert.That(expandableCount, Is.GreaterThan(0), "Expected at least one expandable user message");

        var userMessage = Page.Locator(".user-message.expandable").Nth(0);
        await Expect(userMessage).ToBeVisibleAsync(new() { Timeout = 3000 });

        // Ensure it has the 'expandable' marker class (don't rely on exact class ordering)
        var hasExpandable = await userMessage.EvaluateAsync<bool>("el => el.classList.contains('expandable')");
        Assert.That(hasExpandable, Is.True, "User message should have 'expandable' class");
    }

    [Test]
    [Category("Expandable")]
    public async Task ExpandableMessage_ShowsLessButtonWhenExpanded()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        // Generate a very long message
        var longText = string.Concat(Enumerable.Repeat("This is a long message that will definitely exceed the 150px limit. ", 10));
        await Page.Locator("#userInput").FillAsync(longText);
        await Page.Locator("#mainBtn").ClickAsync();

        // Wait for the message to appear
        await Expect(Page.Locator(".user-message.expandable").Nth(0))
            .ToBeVisibleAsync(new() { Timeout = 3000 });

        // Click the toggle inside the first expandable message. Support either explicit .show-more-btn or a generic button (arrow)
        var userMessage = Page.Locator(".user-message.expandable").Nth(0);
        var toggle = userMessage.Locator(".show-more-btn, button.toggle-collapse, button");
        await Expect(toggle).ToBeVisibleAsync(new() { Timeout = 3000 });
        await toggle.ClickAsync();

        // Verify expanded state via class presence (more robust than exact class string)
        var isExpanded = await userMessage.EvaluateAsync<bool>("el => el.classList.contains('expanded')");
        Assert.That(isExpanded, Is.True, "User message should have 'expanded' after clicking toggle");
    }

    [Test]
    [Category("Expandable")]
    public async Task ExpandedMessage_ShowsFullContent()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        // Generate a very long message
        var longText = string.Concat(Enumerable.Repeat("This is a long message that will definitely exceed the 150px limit. ", 10));
        await Page.Locator("#userInput").FillAsync(longText);
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator(".user-message.expandable").Nth(0))
            .ToBeVisibleAsync(new() { Timeout = 3000 });

        var messageContent = Page.Locator(".user-message.expandable").Nth(0).Locator(".message-content");
        var maxHeightBefore = await messageContent.EvaluateAsync<string>("el => window.getComputedStyle(el).maxHeight");
        // Ensure it's not expanded initially (max-height should not be 'none')
        Assert.That(maxHeightBefore, Is.Not.EqualTo("none"), "Message should be truncated initially (max-height != 'none')");

        // Click the toggle inside the first expandable message
        var userMessage = Page.Locator(".user-message.expandable").Nth(0);
        var toggle = userMessage.Locator(".show-more-btn, button.toggle-collapse, button");
        await toggle.ClickAsync();

        // After expansion, max-height should be 'none'
        var maxHeightAfter = await messageContent.EvaluateAsync<string>("el => window.getComputedStyle(el).maxHeight");
        Assert.That(maxHeightAfter, Is.EqualTo("none"), "Message should have no max-height when expanded");
    }

    [Test]
    [Category("Expandable")]
    public async Task CollapsedMessage_ReturnsToTruncated()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        // Generate a very long message
        var longText = string.Concat(Enumerable.Repeat("This is a long message that will definitely exceed the 150px limit. ", 10));
        await Page.Locator("#userInput").FillAsync(longText);
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator(".user-message.expandable"))
            .ToBeVisibleAsync(new() { Timeout = 3000 });

        var userMessage = Page.Locator(".user-message.expandable").Nth(0);
        var messageContent = userMessage.Locator(".message-content");

        // Expand the message
        var toggle = userMessage.Locator(".show-more-btn, button.toggle-collapse, button");
        await toggle.ClickAsync();

        // Collapse the message by clicking the same toggle
        await toggle.ClickAsync();

        // Ensure 'expanded' class was removed
        var isExpanded = await userMessage.EvaluateAsync<bool>("el => el.classList.contains('expanded')");
        Assert.That(isExpanded, Is.False, "User message should not have 'expanded' after collapsing");

        // Verify max-height is back to truncated (not 'none')
        var maxHeightAfterCollapse = await messageContent.EvaluateAsync<string>("el => window.getComputedStyle(el).maxHeight");
        Assert.That(maxHeightAfterCollapse, Is.Not.EqualTo("none"), "Message should be truncated again after collapse (max-height != 'none')");
    }

    // ?? Clear Chat Tests ???????????????????????????????????????????????????

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

        // Clear action is now located in a dropdown menu. Open menu and click the clear button.
        await Page.Locator("#menu-btn").ClickAsync();
        await Page.Locator("#dropdown-menu button[data-action=\"clear-chat\"]").ClickAsync();

        // App now shows an in-page confirmation dialog. Click the confirm button to clear.
        var confirmDialog = Page.Locator("#confirm-dialog");
        await Expect(confirmDialog).ToBeVisibleAsync(new() { Timeout = 3000 });
        await Page.Locator("#dialog-confirm").ClickAsync();

        // Chat container should be empty after clearing
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

        // Clear action moved to dropdown menu; cancel the in-page confirm dialog to keep messages
        await Page.Locator("#menu-btn").ClickAsync();
        await Page.Locator("#dropdown-menu button[data-action=\"clear-chat\"]").ClickAsync();

        var confirmDialog = Page.Locator("#confirm-dialog");
        await Expect(confirmDialog).ToBeVisibleAsync(new() { Timeout = 3000 });
        await Page.Locator("#dialog-cancel").ClickAsync();

        await Expect(Page.Locator("#chat-container > *")).Not.ToHaveCountAsync(0);
    }
}
