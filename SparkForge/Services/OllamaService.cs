using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using SparkForge.Models;

namespace SparkForge.Services;

public class OllamaService : IDisposable
{
    private HttpClient _http;
    private const string BaseUrl = "http://localhost:11434";
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;

    public OllamaService()
    {
        _http = CreateClient();
    }

    private static HttpClient CreateClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromMinutes(10)
        };
    }

    public void Reset()
    {
        _http.Dispose();
        _http = CreateClient();
        Logger.Info("OllamaService HttpClient reset");
    }

    public async Task<bool> IsRunningAsync()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var response = await _http.GetAsync("/api/tags");
                if (response.IsSuccessStatusCode) return true;

                if (attempt < 2) await Task.Delay(500);
            }
            catch (HttpRequestException ex)
            {
                Logger.Debug($"Ollama health check attempt {attempt + 1} failed: {ex.Message}");
                if (attempt < 2) await Task.Delay(500);
            }
            catch (TaskCanceledException)
            {
                Logger.Debug($"Ollama health check attempt {attempt + 1} timed out");
                if (attempt < 2) await Task.Delay(500);
            }
        }
        return false;
    }

    public async Task<List<OllamaModel>> ListModelsAsync()
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var response = await _http.GetFromJsonAsync<OllamaListResponse>("/api/tags");
                return response?.Models ?? new List<OllamaModel>();
            }
            catch (Exception ex)
            {
                Logger.Warn($"ListModels attempt {attempt + 1} failed: {ex.Message}");
                if (attempt == MaxRetries - 1) throw;
                await Task.Delay(RetryDelayMs * (attempt + 1));
            }
        }
        return new List<OllamaModel>();
    }

    public async IAsyncEnumerable<string?> ChatStreamAsync(
        string model,
        List<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
            stream = true
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage? response = null;
        var lastException = (Exception?)null;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
                {
                    Content = content
                };

                response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                lastException = null;
                break;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == null)
            {
                // Connection refused — Ollama not running
                lastException = ex;
                Logger.Warn($"ChatStream connection refused (attempt {attempt + 1}): {ex.Message}");
                break;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Logger.Warn($"ChatStream attempt {attempt + 1} failed: {ex.Message}");
                if (ct.IsCancellationRequested) yield break;
                if (attempt < MaxRetries - 1)
                    await Task.Delay(RetryDelayMs * (attempt + 1), ct);
            }
        }

        if (response == null || lastException != null)
        {
            if (lastException is HttpRequestException { StatusCode: null })
            {
                // Connection refused signal — return null to trigger UI reconnect
                yield return null;
                yield break;
            }
            Logger.Error("ChatStream all retries exhausted", lastException);
            yield return $"[Error: {lastException?.Message ?? "Unknown error"}]";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);

            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaChatChunk? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize<OllamaChatChunk>(line);
            }
            catch (JsonException ex)
            {
                Logger.Debug($"ChatStream JSON parse error: {ex.Message}");
            }

            if (chunk?.Message?.Content != null)
            {
                yield return chunk.Message.Content;
            }

            if (chunk?.Done == true)
            {
                yield break;
            }
        }
    }

    public async Task<string> ChatAsync(string model, List<ChatMessage> messages)
    {
        var request = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
            stream = false
        };

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("/api/chat", request);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
                return result?.Message?.Content ?? "";
            }
            catch (Exception ex)
            {
                Logger.Warn($"Chat attempt {attempt + 1} failed: {ex.Message}");
                if (attempt == MaxRetries - 1) throw;
                await Task.Delay(RetryDelayMs * (attempt + 1));
            }
        }
        return "";
    }

    public async IAsyncEnumerable<string?> PullModelAsync(
        string modelName,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new { name = modelName, stream = true };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage? response = null;
        var errorMsg = (string?)null;
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/pull")
            {
                Content = content
            };
            response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == null)
        {
            Logger.Error("PullModel: Ollama not running", ex);
            errorMsg = "Ollama is not running. Start Ollama and try again.";
        }
        catch (Exception ex)
        {
            Logger.Error("PullModel failed", ex);
            errorMsg = ex.Message;
        }

        if (errorMsg != null)
        {
            yield return $"{{\"error\": \"{errorMsg.Replace("\"", "'")}\"}}";
            yield break;
        }

        using var stream = await response!.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            yield return line;
        }
    }

    public async Task DeleteModelAsync(string modelName)
    {
        var request = new { name = modelName };
        var response = await _http.PostAsJsonAsync("/api/delete", request);
        response.EnsureSuccessStatusCode();
        Logger.Info($"Deleted model: {modelName}");
    }

    public async Task<ModelInfo?> GetModelInfoAsync(string modelName)
    {
        try
        {
            var request = new { name = modelName };
            var response = await _http.PostAsJsonAsync("/api/show", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaShowResponse>();
            if (result == null) return null;

            return new ModelInfo
            {
                Name = modelName,
                ParameterSize = result.Details?.ParameterSize ?? "",
                Family = result.Details?.Family ?? "",
                Format = result.Details?.Format ?? "",
                SizeBytes = result.SizeBytes
            };
        }
        catch (Exception ex)
        {
            Logger.Warn($"GetModelInfo failed for {modelName}: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    // --- JSON response types ---

    private class OllamaListResponse
    {
        public List<OllamaModel> Models { get; set; } = new();
    }

    private class OllamaChatChunk
    {
        public OllamaChatMessage? Message { get; set; }
        public bool Done { get; set; }
    }

    private class OllamaChatMessage
    {
        public string Content { get; set; } = "";
    }

    private class OllamaChatResponse
    {
        public OllamaChatMessage? Message { get; set; }
    }

    private class OllamaShowResponse
    {
        public long SizeBytes { get; set; }
        public OllamaModelDetails? Details { get; set; }
    }

    private class OllamaModelDetails
    {
        public string ParameterSize { get; set; } = "";
        public string Family { get; set; } = "";
        public string Format { get; set; } = "";
    }
}
