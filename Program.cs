using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// MiniMax M2.7-HighSpeed Token Per Second Benchmark Tool
/// Uses Anthropic-compatible API with MiniMax
/// </summary>

namespace MiniMaxTokenBenchmark;

// Anthropic API Request models
public class AnthropicMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class AnthropicRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";
    
    [JsonPropertyName("messages")]
    public List<AnthropicMessage> Messages { get; set; } = new();
    
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1024;
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;
    
    [JsonPropertyName("system")]
    public string? System { get; set; }
}

public class AnthropicUsage
{
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

public class BenchmarkConfig
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "MiniMax-M2.7";
    public string BaseUrl { get; set; } = "https://api.minimax.io/anthropic/v1/messages";
    public int Iterations { get; set; } = 3;
    public int MaxTokens { get; set; } = 1000;
    public string? Prompt { get; set; }
}

public class BenchmarkResult
{
    public int Iteration { get; set; }
    public int TokensGenerated { get; set; }
    public double TimeSeconds { get; set; }
    public double TokensPerSecond { get; set; }        // end-to-end TPS
    public double GenerationTokensPerSecond { get; set; } // TPS excluding TTFT
    public double TtftSeconds { get; set; }             // time to first token
    public string? Response { get; set; }
}

public class ModelBenchmarkResults
{
    public string ModelName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsHighSpeed { get; set; }
    public List<BenchmarkResult> Results { get; set; } = new();
    public double AverageTps { get; set; }
    public double AverageTime { get; set; }
    public double AverageTokens { get; set; }
    public double AverageTtft { get; set; }
}

public class Program
{
    private static readonly string[] DefaultPrompts = new[]
    {
        "Write a detailed explanation of how neural networks work, including backpropagation, activation functions, and common architectures. Make it comprehensive and educational.",
        "Explain the concept of distributed systems, covering CAP theorem, consensus algorithms like Raft and Paxos, and real-world applications.",
        "Provide a thorough overview of database indexing, B-trees, hash indexes, and query optimization techniques.",
        "Describe the software development lifecycle, from requirements gathering to deployment and maintenance, including best practices for each phase."
    };

    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   MiniMax M2.7-HighSpeed Token Per Second Benchmark Tool       ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var config = ParseConfiguration(args);

        if (string.IsNullOrEmpty(config.ApiKey))
        {
            Console.WriteLine("ERROR: API key is required.");
            Console.WriteLine("Please set one of:");
            Console.WriteLine("  - Environment variable: MINIMAX_API_KEY");
            Console.WriteLine("  - Command line argument: --api-key <your-key>");
            return;
        }

        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Base Model:      {config.Model}");
        Console.WriteLine($"  Base URL:       {config.BaseUrl}");
        Console.WriteLine($"  Iterations:     {config.Iterations}");
        Console.WriteLine($"  Max Tokens:     {config.MaxTokens}");
        Console.WriteLine();

        // Determine which models to run
        var modelsToRun = DetermineModelsToRun(config.Model);
        
        if (modelsToRun.Count == 1)
        {
            Console.WriteLine($"  Will run model:  {modelsToRun[0]}");
        }
        else
        {
            Console.WriteLine($"  Will run models:");
            foreach (var model in modelsToRun)
            {
                var isHighSpeed = model.ToLower().Contains("highspeed");
                Console.WriteLine($"    - {model} {(isHighSpeed ? "(highspeed)" : "(normal)")}");
            }
        }
        Console.WriteLine();

        var prompt = config.Prompt ?? DefaultPrompts[Random.Shared.Next(DefaultPrompts.Length)];
        Console.WriteLine($"Prompt length: {prompt.Length} characters");
        Console.WriteLine();

        // Run benchmarks for each model
        var allResults = new List<ModelBenchmarkResults>();
        
        foreach (var modelName in modelsToRun)
        {
            var modelConfig = new BenchmarkConfig
            {
                ApiKey = config.ApiKey,
                Model = modelName,
                BaseUrl = config.BaseUrl,
                Iterations = config.Iterations,
                MaxTokens = config.MaxTokens,
                Prompt = prompt
            };

            var isHighSpeed = modelName.ToLower().Contains("highspeed");
            var displayName = isHighSpeed ? "HighSpeed" : "Normal";
            
            Console.WriteLine();
            Console.WriteLine($"╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║                    BENCHMARKING: {displayName,-36} ║");
            Console.WriteLine($"╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            var results = new List<BenchmarkResult>();
            
            for (int i = 0; i < config.Iterations; i++)
            {
                Console.WriteLine($"--- Iteration {i + 1}/{config.Iterations} ---");
                var result = await RunBenchmarkAsync(modelConfig, prompt, i + 1);
                results.Add(result);
                
                Console.WriteLine($"  Tokens Generated:  {result.TokensGenerated}");
                Console.WriteLine($"  Time:              {result.TimeSeconds:F2}s");
                Console.WriteLine($"  TPS:               {result.GenerationTokensPerSecond:F2}");
                Console.WriteLine($"  TTFT:              {result.TtftSeconds:F2}");
                Console.WriteLine();
            }

            var modelResults = new ModelBenchmarkResults
            {
                ModelName = modelName,
                DisplayName = displayName,
                IsHighSpeed = isHighSpeed,
                Results = results,
                AverageTps = results.Average(r => r.GenerationTokensPerSecond),
                AverageTime = results.Average(r => r.TimeSeconds),
                AverageTokens = results.Average(r => r.TokensGenerated),
                AverageTtft = results.Average(r => r.TtftSeconds)
            };
            
            allResults.Add(modelResults);
        }

        PrintSummary(allResults, config);
    }

    /// <summary>
    /// Determines which models to run based on the specified model name.
    /// If the model is a base model (not highspeed), runs both normal and highspeed versions.
    /// </summary>
    private static List<string> DetermineModelsToRun(string model)
    {
        var modelLower = model.ToLower();
        
        // If it's already a highspeed model, just run it
        if (modelLower.Contains("highspeed"))
        {
            return new List<string> { model };
        }
        
        // If it's a base model, run both normal and highspeed
        // Handle both "MiniMax-M2.7" and "Minimax-M2.7" patterns
        var baseModel = model;
        
        // Normalize the model name for highspeed variant
        string highspeedModel;
        if (modelLower.StartsWith("minimax-") || modelLower.StartsWith("minimax"))
        {
            // Remove any leading variant prefix and add highspeed
            var withoutPrefix = System.Text.RegularExpressions.Regex.Replace(model, @"^(MiniMax|MiniMax-)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            highspeedModel = $"MiniMax-{withoutPrefix}-highspeed";
        }
        else
        {
            highspeedModel = $"{model}-highspeed";
        }
        
        // Return both models: normal first, then highspeed
        return new List<string> { baseModel, highspeedModel };
    }

    private static BenchmarkConfig ParseConfiguration(string[] args)
    {
        var config = new BenchmarkConfig();

        config.ApiKey = Environment.GetEnvironmentVariable("MINIMAX_API_KEY");

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--api-key":
                case "-k":
                    if (i + 1 < args.Length)
                        config.ApiKey = args[++i];
                    break;
                case "--model":
                case "-m":
                    if (i + 1 < args.Length)
                        config.Model = args[++i];
                    break;
                case "--iterations":
                case "-i":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int iterations))
                        config.Iterations = iterations;
                    break;
                case "--max-tokens":
                case "-t":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int tokens))
                        config.MaxTokens = tokens;
                    break;
                case "--prompt":
                case "-p":
                    if (i + 1 < args.Length)
                        config.Prompt = args[++i];
                    break;
                case "--base-url":
                    if (i + 1 < args.Length)
                        config.BaseUrl = args[++i];
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        return config;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: MiniMaxTokenBenchmark [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --api-key, -k <key>       MiniMax API key (or set MINIMAX_API_KEY env var)");
        Console.WriteLine("  --model, -m <model>       Model name (default: MiniMax-M2.7-highspeed)");
        Console.WriteLine("                           If base model specified (e.g., MiniMax-M2.7),");
        Console.WriteLine("                           automatically runs both normal and highspeed versions");
        Console.WriteLine("  --iterations, -i <n>      Number of iterations per model (default: 3)");
        Console.WriteLine("  --max-tokens, -t <n>      Maximum tokens to generate (default: 1000)");
        Console.WriteLine("  --prompt, -p <text>       Custom prompt");
        Console.WriteLine("  --base-url <url>          API base URL");
        Console.WriteLine("  --help, -h                Show this help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  MiniMaxTokenBenchmark -k YOUR_API_KEY -m MiniMax-M2.7");
        Console.WriteLine("    Runs both MiniMax-M2.7 and MiniMax-M2.7-highspeed");
        Console.WriteLine();
        Console.WriteLine("  MiniMaxTokenBenchmark -k YOUR_API_KEY -m MiniMax-M2.7-highspeed");
        Console.WriteLine("    Runs only the highspeed model");
        Console.WriteLine();
        Console.WriteLine("  MiniMaxTokenBenchmark -k YOUR_API_KEY -i 5 -t 500");
    }

    private static async Task<BenchmarkResult> RunBenchmarkAsync(
        BenchmarkConfig config, string prompt, int iterationNumber)
    {
        var result = new BenchmarkResult { Iteration = iterationNumber };

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var request = new AnthropicRequest
        {
            Model = config.Model,
            Messages = new List<AnthropicMessage>
        {
            new AnthropicMessage { Role = "user", Content = prompt }
        },
            MaxTokens = config.MaxTokens,
            Stream = true,
            System = "You are a helpful assistant."
        };

        var json = JsonSerializer.Serialize(request);

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, config.BaseUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var stopwatch = Stopwatch.StartNew();
        double? ttftSeconds = null;

        var response = await httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead
        );
        response.EnsureSuccessStatusCode();

        using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(responseStream);

        var output = new StringBuilder();
        int? outputTokens = null;

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null)
                break;

            if (string.IsNullOrEmpty(line) || !line.StartsWith("data:"))
                continue;

            var data = line.Substring(5).Trim();
            if (data == "[DONE]")
                break;

            try
            {
                var chunk = JsonSerializer.Deserialize<AnthropicStreamChunk>(data);

                if (chunk?.Type == "message_delta" && chunk.Usage?.OutputTokens > 0)
                {
                    outputTokens = chunk.Usage.OutputTokens;
                }

                if (chunk?.Delta?.Text is { } text)
                {
                    // Record TTFT on first actual content delta
                    if (ttftSeconds == null)
                    {
                        ttftSeconds = stopwatch.Elapsed.TotalSeconds;
                    }
                    output.Append(text);
                }
            }
            catch
            {
                // Skip invalid chunks
            }
        }

        stopwatch.Stop();

        result.Response = output.ToString();
        result.TtftSeconds = ttftSeconds ?? 0;

        if (outputTokens > 0 && stopwatch.Elapsed.TotalSeconds > 0)
        {
            result.TokensGenerated = outputTokens.Value;
            result.TimeSeconds = stopwatch.Elapsed.TotalSeconds;
            result.TokensPerSecond = outputTokens.Value / stopwatch.Elapsed.TotalSeconds;

            // Also calculate generation TPS (excluding TTFT)
            var generationTime = stopwatch.Elapsed.TotalSeconds - (ttftSeconds ?? 0);
            if (generationTime > 0)
            {
                result.GenerationTokensPerSecond = outputTokens.Value / generationTime;
            }
        }

        return result;
    }

    private static void PrintSummary(List<ModelBenchmarkResults> allResults, BenchmarkConfig config)
    {
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                        COMPARISON SUMMARY                      ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        if (allResults.Count == 1)
        {
            // Single model - just show regular summary
            var result = allResults[0];
            PrintSingleModelSummary(result);
        }
        else
        {
            // Multiple models - show comparison
            PrintComparisonSummary(allResults);
        }
        
        Console.WriteLine();
        Console.WriteLine("Benchmark completed!");
    }

    private static void PrintSingleModelSummary(ModelBenchmarkResults result)
    {
        var avgTps = result.AverageTps;
        var avgTime = result.AverageTime;
        var avgTokens = result.AverageTokens;
        var avgTtft = result.AverageTtft;
        
        var minTps = result.Results.Min(r => r.GenerationTokensPerSecond);
        var maxTps = result.Results.Max(r => r.GenerationTokensPerSecond);
        var minTtft = result.Results.Min(r => r.TtftSeconds);
        var maxTtft = result.Results.Max(r => r.TtftSeconds);

        Console.WriteLine($"  Model:                {result.ModelName}");
        Console.WriteLine($"  Iterations:           {result.Results.Count}");
        Console.WriteLine($"  Average TPS:          {avgTps:F2} tokens/second");
        Console.WriteLine($"  Min TPS:              {minTps:F2} tokens/second");
        Console.WriteLine($"  Max TPS:              {maxTps:F2} tokens/second");
        Console.WriteLine($"  Average TTFT:         {avgTtft:F2} seconds");
        Console.WriteLine($"  Min TTFT:             {minTtft:F2} seconds");
        Console.WriteLine($"  Max TTFT:             {maxTtft:F2} seconds");
        Console.WriteLine($"  Average Time:         {avgTime:F2}s");
        Console.WriteLine($"  Average Tokens:       {avgTokens:F0}");
        Console.WriteLine();
        
        Console.WriteLine("Individual Results:");
        Console.WriteLine("  Iter  | Tokens | Time(s) | TPS");
        Console.WriteLine("  ------|--------|---------|--------");
        foreach (var r in result.Results)
        {
            Console.WriteLine($"  {r.Iteration,4}  | {r.TokensGenerated,6} | {r.TimeSeconds,7:F2} | {r.GenerationTokensPerSecond,7:F2}");
        }
    }

    private static void PrintComparisonSummary(List<ModelBenchmarkResults> allResults)
    {
        // Find normal and highspeed models
        var normalResult = allResults.FirstOrDefault(r => !r.IsHighSpeed);
        var highspeedResult = allResults.FirstOrDefault(r => r.IsHighSpeed);
        
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                              RESULTS COMPARISON                              │");
        Console.WriteLine("├─────────────────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("│ Model              │ Avg TPS      │ Avg Time   │ Avg TTFT  │ Avg Tokens     │");
        Console.WriteLine("├─────────────────────────────────────────────────────────────────────────────┤");
        
        foreach (var result in allResults)
        {
            var modelDisplay = result.IsHighSpeed ? $"{result.ModelName} (highspeed)" : $"{result.ModelName} (normal)";
            Console.WriteLine($"│ {modelDisplay,-18} │ {result.AverageTps,12:F2} │ {result.AverageTime,10:F2}s │ {result.AverageTtft,8:F2}s │ {result.AverageTokens,14:F0} │");
        }
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        
        if (normalResult != null && highspeedResult != null)
        {
            var tpsSpeedup = highspeedResult.AverageTps / normalResult.AverageTps;
            var timeReduction = ((normalResult.AverageTime - highspeedResult.AverageTime) / normalResult.AverageTime) * 100;
            var ttftSpeedup = normalResult.AverageTtft / highspeedResult.AverageTtft;
            
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                     HIGHSPEED vs NORMAL SPEEDUP                ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"  HighSpeed is {tpsSpeedup:F2}x faster in token generation");
            Console.WriteLine($"  HighSpeed reduces total time by {timeReduction:F1}%");
            Console.WriteLine($"  HighSpeed TTFT is {ttftSpeedup:F2}x faster");
            Console.WriteLine();
            
            Console.WriteLine("  Breakdown:");
            Console.WriteLine($"    Normal TPS:     {normalResult.AverageTps:F2} tokens/second");
            Console.WriteLine($"    HighSpeed TPS: {highspeedResult.AverageTps:F2} tokens/second");
            Console.WriteLine($"    Difference:    +{highspeedResult.AverageTps - normalResult.AverageTps:F2} tokens/second");
            Console.WriteLine();
            Console.WriteLine($"    Normal Time:     {normalResult.AverageTime:F2}s");
            Console.WriteLine($"    HighSpeed Time: {highspeedResult.AverageTime:F2}s");
            Console.WriteLine($"    Time Saved:     {normalResult.AverageTime - highspeedResult.AverageTime:F2}s per request");
            Console.WriteLine();
            Console.WriteLine($"    Normal TTFT:     {normalResult.AverageTtft:F2}s");
            Console.WriteLine($"    HighSpeed TTFT:  {highspeedResult.AverageTtft:F2}s");
            Console.WriteLine($"    TTFT Saved:     {normalResult.AverageTtft - highspeedResult.AverageTtft:F2}s");
            Console.WriteLine();
            
            // Individual iteration comparison
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    ITERATION-BY-ITERATION COMPARISON           ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  Iter │ Normal TPS │ HighSpeed TPS │ Speedup │ Time Saved");
            Console.WriteLine("  ─────┼────────────┼───────────────┼─────────┼───────────");
            
            var minIterations = Math.Min(normalResult.Results.Count, highspeedResult.Results.Count);
            for (int i = 0; i < minIterations; i++)
            {
                var normalIter = normalResult.Results[i];
                var hsIter = highspeedResult.Results[i];
                var speedup = hsIter.GenerationTokensPerSecond / normalIter.GenerationTokensPerSecond;
                var timeSaved = normalIter.TimeSeconds - hsIter.TimeSeconds;
                Console.WriteLine($"  {i + 1,4}  │ {normalIter.GenerationTokensPerSecond,10:F2} │ {hsIter.GenerationTokensPerSecond,13:F2} │ {speedup,7:F2}x │ {timeSaved,9:F2}s");
            }
        }
        
        // Print individual results for each model
        foreach (var result in allResults)
        {
            var modelType = result.IsHighSpeed ? "HighSpeed" : "Normal";
            Console.WriteLine();
            Console.WriteLine($"╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║                  {modelType.ToUpper()} MODEL: {result.ModelName,-30} ║");
            Console.WriteLine($"╚════════════════════════════════════════════════════════════════╝");
            
            Console.WriteLine();
            Console.WriteLine("  Individual Results:");
            Console.WriteLine("  Iter  │ Tokens │ Time(s) │ TPS");
            Console.WriteLine("  ──────┼────────┼────────┼────────");
            foreach (var r in result.Results)
            {
                Console.WriteLine($"  {r.Iteration,4}  │ {r.TokensGenerated,6} │ {r.TimeSeconds,7:F2} │ {r.GenerationTokensPerSecond,7:F2}");
            }
        }
    }
}

// Stream response models for Anthropic
public class AnthropicStreamChunk
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("delta")]
    public AnthropicDelta? Delta { get; set; }
    
    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }
}

public class AnthropicDelta
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
