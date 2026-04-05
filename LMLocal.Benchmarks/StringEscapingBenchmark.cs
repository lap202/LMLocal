using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Text;

namespace LMLocal.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[SimpleJob(RuntimeMoniker.Net472)]
public class StringEscapingBenchmark
{
    private readonly string _chunk = "```csharp\npublic void Fix()\n{\n    // this isn't easy\n}\n```";
    private readonly string _chunkWithQuotes = "Here's a string with 'single quotes' and \"double quotes\", ain't it grand?";

    [Benchmark(Baseline = true)]
    public string LegacyReplace_NoQuotes()
    {
        // This is exactly what happens on EVERY token chunk in LMLocalBridge
        return _chunk.Replace("'", "\\'");
    }

    [Benchmark]
    public string LegacyReplace_WithQuotes()
    {
        return _chunkWithQuotes.Replace("'", "\\'");
    }

    [Benchmark]
    public string CustomReplace_WithQuotes()
    {
        // A custom manual escape to see if it saves allocations compared to base Replace
        var sb = new StringBuilder(_chunkWithQuotes.Length + 5);
        foreach (char c in _chunkWithQuotes)
        {
            if (c == '\'')
            {
                sb.Append("\\'");
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
