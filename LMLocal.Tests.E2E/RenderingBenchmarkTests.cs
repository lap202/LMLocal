using System.Text.Json;

namespace LMLocal.Tests.E2E;

/// <summary>
/// Benchmark tests for measuring UI rendering performance on large/frequent streamed chunks.
/// Tests the new AppStore/AppManager architecture.
/// </summary>
[TestFixture]
[Explicit("Explicit benchmark test. Run manually via --filter 'Category=Benchmark' in NUnit")]
public class RenderingBenchmarkTests : PageTest
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
    [Category("Benchmark")]
    public async Task Benchmark_MarkdownRenderingPerformance()
    {
        // Setup console logging
        Page.Console += (_, msg) =>
        {
            Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");
        };

        // 1. Arrange CDP Session to capture low-level browser metrics
        var client = await Page.Context.NewCDPSessionAsync(Page);
        await client.SendAsync("Performance.enable");

        // 2. Load the benchmark mock page
        await GotoWithMockAsync("webview-mock-benchmark.js");
        await Expect(Page.Locator("#conn-status"))
            .ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        // Get the number of chunks that will be sent
        var totalChunks = await Page.EvaluateAsync<int>(
            "() => window.__streamingState.totalChunksToSend"
        );
        Console.WriteLine($"Total chunks to send: {totalChunks}");

        // Force a garbage collection before test
        await client.SendAsync("HeapProfiler.enable");
        await client.SendAsync("HeapProfiler.collectGarbage");

        // Take snapshot of metrics before generation starts
        var startMetricsObj = await client.SendAsync("Performance.getMetrics");
        var startMetrics = ExtractMetrics(startMetricsObj);

        // 3. Act: Trigger generation
        await Page.Locator("#userInput").FillAsync("Start benchmark");
        await Page.Locator("#mainBtn").ClickAsync();

        // 4. Wait for stream to actually start
        await Page.WaitForFunctionAsync(
            "() => window.__streamingState.isStreaming === true",
            new PageWaitForFunctionOptions { Timeout = 5000 }
        );
        Console.WriteLine("Stream started...");

        // 5. Wait for stream to complete
        await Page.WaitForFunctionAsync(
            "() => window.__streamingState.isComplete === true",
            new PageWaitForFunctionOptions { Timeout = 60000 }
        );
        Console.WriteLine("Stream completed");

        // 6. Wait for UI to finish rendering
        await Page.WaitForFunctionAsync(
            "() => document.querySelectorAll('.ai-message.completed').length > 0",
            new PageWaitForFunctionOptions { Timeout = 10000 }
        );
        Console.WriteLine("AI message marked as completed");

        // 7. Get actual chunks sent
        var chunksSent = await Page.EvaluateAsync<int>(
            "() => window.__streamingState.chunksSent"
        );
        Console.WriteLine($"Actual chunks sent: {chunksSent}");

        // 8. Gather low-level metrics using CDP
        var endMetricsObj = await client.SendAsync("Performance.getMetrics");
        var endMetrics = ExtractMetrics(endMetricsObj);

        // 9. Gather rendering timings from the JS performance API
        var measureObj = await Page.EvaluateAsync<JsonElement>(
            "() => performance.getEntriesByName('stream-duration')[0]"
        );
        var durationMs = measureObj.GetProperty("duration").GetDouble();

        // Calculate differences
        long layoutCount = endMetrics["LayoutCount"] - startMetrics["LayoutCount"];
        long recalcStyleCount = endMetrics["RecalcStyleCount"] - startMetrics["RecalcStyleCount"];
        double domDeltaBytes = (endMetrics["JSHeapUsedSize"] - startMetrics["JSHeapUsedSize"]) / 1024.0 / 1024.0;

        // 10. Output Results
        Console.WriteLine("\n================ BENCHMARK RESULTS ================");
        Console.WriteLine($"Total Chunks Sent:        {chunksSent}");
        Console.WriteLine($"Total JS Stream Duration: {durationMs:F2} ms");
        Console.WriteLine($"Avg Time per Chunk:       {durationMs / chunksSent:F2} ms");
        Console.WriteLine($"Layout Recalculations:    {layoutCount}");
        Console.WriteLine($"CSS Style Recalculations: {recalcStyleCount}");
        Console.WriteLine($"JS Heap Used Delta:       {domDeltaBytes:F2} MB");
        Console.WriteLine("===================================================\n");

        TestContext.Out.WriteLine("BENCHMARK_RESULTS:");
        TestContext.Out.WriteLine($"Chunks Sent: {chunksSent}");
        TestContext.Out.WriteLine($"Stream Duration (ms): {durationMs}");
        TestContext.Out.WriteLine($"Avg Time per Chunk (ms): {durationMs / chunksSent}");
        TestContext.Out.WriteLine($"LayoutCount: {layoutCount}");
        TestContext.Out.WriteLine($"RecalcStyleCount: {recalcStyleCount}");

        // Assert sanity limits
        var layoutThreshold = Math.Max(500, chunksSent * 5);
        Assert.That(layoutCount, Is.LessThan(layoutThreshold), 
            $"Too many layouts triggered: {layoutCount} (threshold: {layoutThreshold})");
    }

    [Test]
    [Category("Benchmark")]
    public async Task Benchmark_ThoughtRenderingPerformance()
    {
        // Benchmark measuring time from send to thought content render for a single thought.
        // Uses the thinking mock which emits StreamThought events.
        var client = await Page.Context.NewCDPSessionAsync(Page);
        await client.SendAsync("Performance.enable");

        // Relay browser console logs to test output for parity with other benchmarks
        Page.Console += (_, msg) =>
        {
            Console.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");
        };

        await GotoWithMockAsync("webview-mock-thinking.js");
        await Expect(Page.Locator("#conn-status")).ToHaveTextAsync("Connected", new() { Timeout = 3000 });

        // Try to read total chunks info from the page if available (may be undefined for some mocks)
        try
        {
            var totalChunks = await Page.EvaluateAsync<int>("() => (window.__streamingState && window.__streamingState.totalChunksToSend) || 0");
            Console.WriteLine($"Total chunks to send: {totalChunks}");
        }
        catch
        {
            // ignore if not present
        }

        // Force a garbage collection before test
        await client.SendAsync("HeapProfiler.enable");
        await client.SendAsync("HeapProfiler.collectGarbage");

        // Take snapshot of metrics before generation starts
        var startMetricsObj = await client.SendAsync("Performance.getMetrics");
        var startMetrics = ExtractMetrics(startMetricsObj);

        // Mark start just before triggering the thought stream
        await Page.EvaluateAsync("() => { window.__bench_thought_start = performance.now(); }");

        await Page.Locator("#userInput").FillAsync("Hello");
        await Page.Locator("#mainBtn").ClickAsync();

        // Wait until thought content appears and has non-empty text
        await Page.WaitForFunctionAsync(
            "() => { const el = document.querySelector('.thought-content'); return el && el.innerText && el.innerText.length > 0; }",
            new PageWaitForFunctionOptions { Timeout = 10000 }
        );

        // Compute duration
        var durationMs = await Page.EvaluateAsync<double>("() => performance.now() - (window.__bench_thought_start || performance.now())");

        // Try to read actual chunks sent info if available
        try
        {
            var chunksSent = await Page.EvaluateAsync<int>("() => (window.__streamingState && window.__streamingState.chunksSent) || 0");
            Console.WriteLine($"Actual chunks sent: {chunksSent}");
        }
        catch
        {
            // ignore
        }

        // Gather end metrics
        var endMetricsObj = await client.SendAsync("Performance.getMetrics");
        var endMetrics = ExtractMetrics(endMetricsObj);

        // Calculate differences
        long layoutCount = endMetrics["LayoutCount"] - startMetrics["LayoutCount"];
        long recalcStyleCount = endMetrics["RecalcStyleCount"] - startMetrics["RecalcStyleCount"];
        double domDeltaBytes = (endMetrics["JSHeapUsedSize"] - startMetrics["JSHeapUsedSize"]) / 1024.0 / 1024.0;

        // 10. Output Results (mirror format of Markdown benchmark)
        Console.WriteLine("\n================ THOUGHT BENCHMARK RESULTS ================");
        Console.WriteLine($"Thought render Duration: {durationMs:F2} ms");
        Console.WriteLine($"Layout Recalculations:    {layoutCount}");
        Console.WriteLine($"CSS Style Recalculations: {recalcStyleCount}");
        Console.WriteLine($"JS Heap Used Delta:       {domDeltaBytes:F2} MB");
        Console.WriteLine("===================================================\n");

        TestContext.Out.WriteLine("BENCHMARK_THOUGHT_RESULTS:");
        TestContext.Out.WriteLine($"Thought render duration (ms): {durationMs}");
        TestContext.Out.WriteLine($"LayoutCount: {layoutCount}");
        TestContext.Out.WriteLine($"RecalcStyleCount: {recalcStyleCount}");

        // Basic sanity assertions: duration should be positive and not ridiculously large
        Assert.That(durationMs, Is.GreaterThan(0), "Measured thought render duration should be > 0");

        // Assert sanity limits for layouts similar to markdown benchmark (use small fixed threshold since single thought)
        var layoutThreshold = Math.Max(50, 1 * 5);
        Assert.That(layoutCount, Is.LessThan(layoutThreshold),
            $"Too many layouts triggered for thought: {layoutCount} (threshold: {layoutThreshold})");
    }

    private System.Collections.Generic.Dictionary<string, long> ExtractMetrics(JsonElement? metricJson)
    {
        var dict = new System.Collections.Generic.Dictionary<string, long>();
        if (metricJson?.ValueKind == JsonValueKind.Object && metricJson.Value.TryGetProperty("metrics", out var metricsArray))
        {
            foreach (var item in metricsArray.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString();
                var value = item.GetProperty("value").GetDouble();
                if (!string.IsNullOrEmpty(name))
                {
                    dict[name] = (long)value;
                }
            }
        }
        return dict;
    }
}
