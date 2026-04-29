using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Currere_backend.Agents.Core
{
    /// <summary>
    /// Hem yerel Ollama motorunu (Birincil) hem de Groq API'yi (Yedek/Fallback) barındıran Hibrid Chat Completion Servisi.
    /// Ollama yanıt vermezse, hata fırlatırsa veya zaman aşımına uğrarsa (Timeout), sessizce Groq API'ye yönlendirir.
    /// </summary>
    public class HybridChatCompletionService : IChatCompletionService
    {
        private readonly IChatCompletionService _ollamaService;
        private readonly IChatCompletionService _groqService;
        private readonly ILogger<HybridChatCompletionService> _logger;

        // Semantic Kernel'in beklediği Attribute zenginliği (İlk motorun özelliklerini baz alır)
        public IReadOnlyDictionary<string, object?> Attributes => _ollamaService.Attributes;

        public HybridChatCompletionService(IConfiguration config, ILogger<HybridChatCompletionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // AppSettings konfigürasyonlarını oku (Ollama ve Groq her ikisi de OpenAI uyumludur)
            var ollamaEndpoint = config["AgentSettings:OllamaEndpoint"] ?? "http://localhost:11434/v1/";
            var ollamaModel = config["AgentSettings:OllamaModel"] ?? "qwen2.5-coder:7b";
            
            var groqApiKey = config["AgentSettings:GroqApiKey"] ?? throw new ArgumentNullException("GroqApiKey eksik!");
            var groqModel = config["AgentSettings:GroqModel"] ?? "llama-3.3-70b-versatile";

            // 1. OLLAMA (Birincil Motor)
            // Ollama'nın OpenAI uyumlu `/v1` endpointini kullanıyoruz.
            var ollamaHttpClient = new HttpClient { BaseAddress = new Uri(ollamaEndpoint) };
            _ollamaService = new OpenAIChatCompletionService(
                modelId: ollamaModel, 
                apiKey: "ollama-local-key", // Ollama için rastgele bir key yeterlidir
                httpClient: ollamaHttpClient);

            // 2. GROQ (Fallback Motor)
            // Groq'un OpenAI uyumlu endpointini kullanıyoruz.
            var groqHttpClient = new HttpClient { BaseAddress = new Uri("https://api.groq.com/openai/v1/") };
            _groqService = new OpenAIChatCompletionService(
                modelId: groqModel, 
                apiKey: groqApiKey, 
                httpClient: groqHttpClient);
        }

        public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, 
            PromptExecutionSettings? executionSettings = null, 
            Kernel? kernel = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Ollama'ya en fazla 15 saniye süre ver (Fallback hızı için)
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(15));

                _logger.LogInformation("[HYBRID ENGINE] Ollama ({Model}) deneniyor...", executionSettings?.ModelId ?? "qwen");
                return await _ollamaService.GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cts.Token);
            }
            catch (Exception ex)
            {
                // Timeout, HttpRequestException veya OutOfMemory vb. patlamalarda sessizce yedek motora geç.
                _logger.LogWarning("[HYBRID ENGINE FALLBACK] Ollama çöktü veya zaman aşımına uğradı. Sebep: {Reason}. Anında Groq API devreye alınıyor...", ex.Message);
                return await _groqService.GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
            }
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory, 
            PromptExecutionSettings? executionSettings = null, 
            Kernel? kernel = null, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IAsyncEnumerator<StreamingChatMessageContent>? enumerator = null;
            bool useGroqFallback = false;
            bool hasFirstChunk = false;

            try
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(15)); // İlk chunk yanıtı için timeout
                
                var ollamaStream = _ollamaService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cts.Token);
                enumerator = ollamaStream.GetAsyncEnumerator(cts.Token);
                
                // İlk kelimeyi (chunk) almayı dene
                if (await enumerator.MoveNextAsync())
                {
                    hasFirstChunk = true;
                }
                else
                {
                    useGroqFallback = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[HYBRID ENGINE FALLBACK] Ollama stream başlatılamadı. Sebep: {Reason}. Groq API devreye giriyor...", ex.Message);
                useGroqFallback = true;
            }

            if (useGroqFallback)
            {
                if (enumerator != null) await enumerator.DisposeAsync();

                // Groq API'nin stream servisini başlat ve oradan akıt
                await foreach (var content in _groqService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken))
                {
                    yield return content;
                }
            }
            else
            {
                try
                {
                    if (hasFirstChunk && enumerator != null)
                    {
                        yield return enumerator.Current;
                    }
                    
                    while (enumerator != null && await enumerator.MoveNextAsync())
                    {
                        yield return enumerator.Current;
                    }
                }
                finally
                {
                    if (enumerator != null) await enumerator.DisposeAsync();
                }
            }
        }
    }
}
