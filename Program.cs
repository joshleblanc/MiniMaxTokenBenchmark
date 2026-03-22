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
    public string Model { get; set; } = "MiniMax-M2.7-highspeed";
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
        Console.WriteLine($"  Model:          {config.Model}");
        Console.WriteLine($"  Base URL:       {config.BaseUrl}");
        Console.WriteLine($"  Iterations:     {config.Iterations}");
        Console.WriteLine($"  Max Tokens:     {config.MaxTokens}");
        Console.WriteLine();

        var prompt = config.Prompt ?? DefaultPrompts[Random.Shared.Next(DefaultPrompts.Length)];
        Console.WriteLine($"Prompt length: {prompt.Length} characters");
        Console.WriteLine();

        var results = new List<BenchmarkResult>();
        
        for (int i = 0; i < config.Iterations; i++)
        {
            Console.WriteLine($"--- Iteration {i + 1}/{config.Iterations} ---");
            var result = await RunBenchmarkAsync(config, prompt, i + 1);
            results.Add(result);
            
            Console.WriteLine($"  Tokens Generated:  {result.TokensGenerated}");
            Console.WriteLine($"  Time:              {result.TimeSeconds:F2}s");
            Console.WriteLine($"  TPS:               {result.GenerationTokensPerSecond:F2}");
            Console.WriteLine($"  TTFT:              {result.TtftSeconds:F2}");
            Console.WriteLine();
        }

        PrintSummary(results, config);
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
        Console.WriteLine("  --iterations, -i <n>      Number of iterations (default: 3)");
        Console.WriteLine("  --max-tokens, -t <n>      Maximum tokens to generate (default: 1000)");
        Console.WriteLine("  --prompt, -p <text>       Custom prompt");
        Console.WriteLine("  --base-url <url>          API base URL");
        Console.WriteLine("  --help, -h                Show this help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  MiniMaxTokenBenchmark -k YOUR_API_KEY");
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

    private static void PrintSummary(List<BenchmarkResult> results, BenchmarkConfig config)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                        SUMMARY                                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        
        var avgTps = results.Average(r => r.GenerationTokensPerSecond);
        var avgTime = results.Average(r => r.TimeSeconds);
        var avgTokens = results.Average(r => r.TokensGenerated);
        var avgTtft = results.Average(r => r.TtftSeconds);
        
        var minTps = results.Min(r => r.GenerationTokensPerSecond);
        var maxTps = results.Max(r => r.GenerationTokensPerSecond);
        var minTtft = results.Min(r => r.TtftSeconds);
        var maxTtft = results.Max(r => r.TtftSeconds);

        Console.WriteLine($"  Model:                {config.Model}");
        Console.WriteLine($"  Iterations:           {results.Count}");
        Console.WriteLine($"  Average TPS:          {avgTps:F2} tokens/second");
        Console.WriteLine($"  Min TPS:              {minTps:F2} tokens/second");
        Console.WriteLine($"  Max TPS:              {maxTps:F2} tokens/second");
        Console.WriteLine($"  Average TTFT          {avgTtft:F2} seconds");
        Console.WriteLine($"  Min TTFT:             {minTtft:F2} seconds");
        Console.WriteLine($"  Max TTFT:             {maxTtft:F2} seconds");
        Console.WriteLine($"  Average Time:         {avgTime:F2}s");
        Console.WriteLine($"  Average Tokens:       {avgTokens:F0}");
        Console.WriteLine();
        
        Console.WriteLine("Individual Results:");
        Console.WriteLine("  Iter  | Tokens | Time(s) | TPS");
        Console.WriteLine("  ------|--------|---------|--------");
        foreach (var r in results)
        {
            Console.WriteLine($"  {r.Iteration,4}  | {r.TokensGenerated,6} | {r.TimeSeconds,7:F2} | {r.GenerationTokensPerSecond,7:F2}");
        }
        
        Console.WriteLine();
        Console.WriteLine("Benchmark completed!");
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
