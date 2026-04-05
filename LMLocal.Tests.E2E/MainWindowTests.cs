namespace LMLocal.Tests.E2E;
using System.Text.RegularExpressions;

/// <summary>
/// E2E tests for main-window.html with AppStore/AppManager architecture.
/// Tests connection states, chat flow, streaming, markdown rendering, and error handling.
/// </summary>
[TestFixture]
public partial class MainWindowTests : PageTest
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

    private const string TestPageUrl = "https://app.local/test-main-window.html";

    public override BrowserNewContextOptions ContextOptions() => new() { BypassCSP = true };

    private async Task GotoWithMockAsync(string mockFileName)
    {
        var assetsDir = Path.GetFullPath("TestAssets");
        var resourcesDir = Path.GetFullPath(@"..\..\..\..\LMLocal\Resources");

        await Page.RouteAsync("https://app.local/**", async route =>
        {
            var urlPath = new Uri(route.Request.Url).AbsolutePath.TrimStart('/');

            if (urlPath == "test-main-window.html")
            {
                var testHtmlPath = Path.Combine(assetsDir, "test-main-window.html");
                if (!File.Exists(testHtmlPath))
                {
                    testHtmlPath = Path.GetFullPath(@"..\..\..\..\LMLocal\Resources\main-window.html");
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
    [Category("Skeleton")]
    public async Task Skeleton_IsShownWhileWaitingForFirstChunk()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        await Expect(Page.Locator(".skeleton-loader"))
            .ToBeVisibleAsync(new() { Timeout = 3000 });
    }

    [Test]
    [Category("Skeleton")]
    public async Task Skeleton_IsReplacedByContentAfterChunkReceived()
    {
        await GotoWithMockAsync("webview-mock-streaming.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        // Wait for stream to complete
        await Expect(Page.Locator("#status-text"))
            .ToHaveTextAsync("Ready", new() { Timeout = 5000 });

        // Skeleton should be gone
        await Expect(Page.Locator(".skeleton-loader")).ToHaveCountAsync(0);
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

        // Empty AI message should be removed
        await Expect(Page.Locator(".ai-message")).ToHaveCountAsync(0);
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

        // Verify user message is created with expandable class
        var userMessage = Page.Locator(".user-message.expandable");
        await Expect(userMessage).ToBeVisibleAsync(new() { Timeout = 3000 });

        // Verify "Show more" button is visible
        await Expect(Page.Locator(".user-message.expandable .show-more-btn"))
            .ToHaveTextAsync("Show more", new() { Timeout = 3000 });
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
        await Expect(Page.Locator(".user-message.expandable"))
            .ToBeVisibleAsync(new() { Timeout = 3000 });

        // Click "Show more" button
        await Page.Locator(".user-message.expandable .show-more-btn").ClickAsync();

        // Verify button text changed to "Show less"
        await Expect(Page.Locator(".user-message.expandable .show-more-btn"))
            .ToHaveTextAsync("Show less", new() { Timeout = 3000 });
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

        await Expect(Page.Locator(".user-message.expandable"))
            .ToBeVisibleAsync(new() { Timeout = 3000 });

        // Message should have max-height: 150px initially
        var messageContent = Page.Locator(".user-message.expandable .message-content");
        var maxHeightBefore = await messageContent.EvaluateAsync<string>("el => window.getComputedStyle(el).maxHeight");
        Assert.That(maxHeightBefore, Is.EqualTo("150px"), "Message should be truncated to 150px initially");

        // Click "Show more" button
        await Page.Locator(".user-message.expandable .show-more-btn").ClickAsync();

        // After expansion, max-height should be "none"
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

        var messageContent = Page.Locator(".user-message.expandable .message-content");

        // Expand the message
        await Page.Locator(".user-message.expandable .show-more-btn").ClickAsync();

        // Collapse the message by clicking "Show less"
        await Page.Locator(".user-message.expandable .show-more-btn").ClickAsync();

        // Verify button text is back to "Show more"
        await Expect(Page.Locator(".user-message.expandable .show-more-btn"))
            .ToHaveTextAsync("Show more", new() { Timeout = 3000 });

        // Verify max-height is back to 150px
        var maxHeightAfterCollapse = await messageContent.EvaluateAsync<string>("el => window.getComputedStyle(el).maxHeight");
        Assert.That(maxHeightAfterCollapse, Is.EqualTo("150px"), "Message should be truncated again after collapse");
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

        Page.Dialog += (_, d) => d.AcceptAsync();
        await Page.Locator(".btn-clear-icon").ClickAsync();

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

        Page.Dialog += (_, d) => d.DismissAsync();
        await Page.Locator(".btn-clear-icon").ClickAsync();

        await Expect(Page.Locator("#chat-container > *")).Not.ToHaveCountAsync(0);
    }
}
