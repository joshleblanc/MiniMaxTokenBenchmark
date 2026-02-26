# MiniMax M2.5-HighSpeed Token Per Second Benchmark

A .NET console application to test tokens per second (TPS) performance of MiniMax models using the Anthropic-compatible API.

## Requirements

- .NET 10.0 or later
- MiniMax API Key

## Installation

```bash
cd MiniMaxTokenBenchmark
dotnet build
```

## Usage

```bash
dotnet run -- -k YOUR_MINIMAX_API_KEY
```

Or set the API key as an environment variable:

```bash
$env:MINIMAX_API_KEY="your-api-key"
dotnet run
```

## Command Line Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--api-key` | `-k` | MiniMax API key | Environment variable |
| `--model` | `-m` | Model name | MiniMax-M2.5-highspeed |
| `--iterations` | `-i` | Number of iterations | 3 |
| `--max-tokens` | `-t` | Max tokens to generate | 1000 |
| `--prompt` | `-p` | Custom prompt | Random prompt |
| `--base-url` | - | API base URL | https://api.minimax.io/anthropic/v1/messages |
| `--help` | `-h` | Show help | - |

## Examples

```bash
dotnet run -- -k YOUR_API_KEY
dotnet run -- -k YOUR_API_KEY -i 5 -t 500
```

## Supported Models

- `MiniMax-M2.5-highspeed` (default - ~100 TPS)
- `MiniMax-M2.5` (~60 TPS)
- `MiniMax-M2.1-highspeed` (~100 TPS)
- `MiniMax-M2.1` (~60 TPS)

## Output

```
╔════════════════════════════════════════════════════════════════╗
║   MiniMax M2.5-HighSpeed Token Per Second Benchmark Tool      ║
╚════════════════════════════════════════════════════════════════╝

Configuration:
  Model:          MiniMax-M2.5-highspeed
  Base URL:       https://api.minimax.io/anthropic/v1/messages
  Iterations:     3
  Max Tokens:     1000

--- Iteration 1/3 ---
  Tokens Generated:  1000
  Time:              10.50s
  TPS:               95.24

--- Iteration 2/3 ---
  ...

╔════════════════════════════════════════════════════════════════╗
║                        SUMMARY                                ║
╚════════════════════════════════════════════════════════════════╝
  Iterations:           3
  Average TPS:          94.52 tokens/second
  Min TPS:              91.23 tokens/second
  Max TPS:              98.12 tokens/second
  Average Time:         10.58s
  Average Tokens:       1000

Individual Results:
  Iter  | Tokens | Time(s) | TPS
  ------|--------|---------|--------
     1  |   1000 |   10.50 |   95.24
     2  |   1000 |   10.75 |   93.02
     3  |   1000 |   10.50 |   95.29

Benchmark completed!
```

## How It Works

1. Makes streaming requests to MiniMax's Anthropic-compatible API
2. Captures `usage.output_tokens` from the `message_delta` event for accurate token counting
3. Calculates TPS as: `output_tokens / total_time`

### Note on Timing

The TPS includes network latency (time to send request + receive response). For a more accurate token generation speed, subtract the estimated first token latency (~2-4 seconds depending on your network and model).
