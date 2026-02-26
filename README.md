# MiniMax M2.5-HighSpeed Token Per Second Benchmark

A .NET console application designed to test and measure the tokens per second (TPS) performance of the MiniMax M2.5-HighSpeed model.

## Features

- Measures tokens per second (TPS) performance
- Supports both streaming and non-streaming modes
- Configurable number of iterations
- Reports first-token latency
- Calculates average, min, and max TPS across iterations
- Supports custom prompts and models

## Requirements

- .NET 10.0 or later
- MiniMax API Key

## Installation

1. Clone or download this repository
2. Navigate to the project directory:
   ```
   cd MiniMaxTokenBenchmark
   ```
3. Build the project:
   ```
   dotnet build
   ```

## Usage

### Basic Usage

```bash
dotnet run -- -k YOUR_MINIMAX_API_KEY
```

### Using Environment Variable

```bash
export MINIMAX_API_KEY=your_api_key
dotnet run
```

### Command Line Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--api-key` | `-k` | MiniMax API key | Environment variable |
| `--model` | `-m` | Model name | MiniMax-M2.5-highspeed |
| `--iterations` | `-i` | Number of iterations | 3 |
| `--max-tokens` | `-t` | Max tokens to generate | 1000 |
| `--stream` | `-s` | Enable streaming mode | false |
| `--prompt` | `-p` | Custom prompt | Random prompt |
| `--base-url` | - | API base URL | https://api.minimax.chat/v1 |
| `--help` | `-h` | Show help | - |

### Examples

Run benchmark with 5 iterations:
```bash
dotnet run -- -k YOUR_API_KEY -i 5
```

Run with streaming mode:
```bash
dotnet run -- -k YOUR_API_KEY --stream
```

Generate only 500 tokens:
```bash
dotnet run -- -k YOUR_API_KEY -t 500
```

Use a custom prompt:
```bash
dotnet run -- -k YOUR_API_KEY -p "Write a Python function to calculate fibonacci numbers"
```

## Supported Models

- `MiniMax-M2.5-highspeed` (default - ~100 TPS)
- `MiniMax-M2.5` (~60 TPS)
- `MiniMax-M2.1-highspeed` (~100 TPS)
- `MiniMax-M2.1` (~60 TPS)

## Output Example

```
╔════════════════════════════════════════════════════════════════╗
║   MiniMax M2.5-HighSpeed Token Per Second Benchmark Tool      ║
╚════════════════════════════════════════════════════════════════╝

Configuration:
  Model:          MiniMax-M2.5-highspeed
  Base URL:       https://api.minimax.chat/v1
  Iterations:     3
  Max Tokens:     1000
  Stream Mode:    false

--- Iteration 1/3 ---
  Tokens Generated:  1000
  Time:              9.85s
  TPS:               101.52
  First Token Latency: 892ms

--- Iteration 2/3 ---
  Tokens Generated:  1000
  Time:              9.78s
  TPS:               102.25
  First Token Latency: 887ms

--- Iteration 3/3 ---
  Tokens Generated:  1000
  Time:              9.91s
  TPS:               100.91
  First Token Latency: 901ms

╔════════════════════════════════════════════════════════════════╗
║                        SUMMARY                                ║
╚════════════════════════════════════════════════════════════════╝
  Iterations:           3
  Average TPS:          101.56 tokens/second
  Min TPS:              100.91 tokens/second
  Max TPS:              102.25 tokens/second
  Average Time:         9.85s
  Average Tokens:       1000
  Avg First Token Lat:  893ms

Benchmark completed!
```

## How TPS is Calculated

Tokens per second is calculated as:

```
TPS = Total Tokens Generated / Total Time (seconds)
```

The benchmark measures:
- **Total time**: From when the request is sent until the complete response is received
- **Tokens generated**: Number of tokens in the model's completion
- **First token latency**: Time until the first token is received (especially useful in streaming mode)

## Notes

- The actual TPS may vary based on network latency and server load
- For streaming mode, the first token latency is measured more accurately
- The token count in non-streaming mode uses the API's usage data when available, otherwise uses an estimation algorithm
- The benchmark uses relatively long prompts to minimize the impact of prompt processing time on results
