using BenchmarkDotNet.Running;
// Use this to run the benchmark (args allow passing --filter from command line):
// dotnet run -c Release --project LMLocal.Benchmarks
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
