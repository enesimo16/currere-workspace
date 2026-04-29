using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting;
using Currere_backend.Agents.Plugins;
using Currere_backend.Agents.Models;
using Currere_backend.Hubs;

namespace Currere_backend.Agents.Core
{
    /// <summary>
    /// AgentOrchestrator: Architect, Coder ve Healer ajanlarını yöneten,
    /// Dosya tabanlı Semantic Kernel plugin okuma ve katı DTO dönüştürme mantığı içeren ana beyin.
    /// </summary>
    public class AgentOrchestrator
    {
        private readonly Kernel _kernel;
        private readonly DockerExecutionPlugin _dockerPlugin;
        private readonly ILogger<AgentOrchestrator> _logger;
        private readonly IHubContext<TerminalHub> _hubContext;
        private readonly IWebHostEnvironment _env;

        public AgentOrchestrator(
            Kernel kernel, 
            DockerExecutionPlugin dockerPlugin, 
            ILogger<AgentOrchestrator> logger, 
            IHubContext<TerminalHub> hubContext,
            IWebHostEnvironment env)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _dockerPlugin = dockerPlugin ?? throw new ArgumentNullException(nameof(dockerPlugin));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        public async Task<string> RunAgenticWorkflowAsync(string userPrompt, int workspaceId, string profileJson, string domainContext, string enginePreference = "auto")
        {
            _logger.LogInformation("[Sistem] Agent Orchestrator görevi devraldı. WorkspaceId: {WsId}", workspaceId);

            // 1. Dosya sisteminden (Agents/Prompts) ajan kimliklerini fiziksel olarak yükle
            string promptsPath = Path.Combine(_env.ContentRootPath, "Agents", "Prompts");
            var agentsPlugin = _kernel.CreatePluginFromPromptDirectory(promptsPath);

            // ══════════════════════════════════════════════════════════════
            // ADIM 1: ARCHITECT (Planlama Aşaması)
            // ══════════════════════════════════════════════════════════════
            _logger.LogInformation("[Sistem] Architect ajanı planı hazırlıyor...");
            await SendStatusAsync(workspaceId, "Architect planı hazırlıyor...");
            
            // Seçili motora göre ServiceId basıyoruz
            var executionSettings = new PromptExecutionSettings
            {
                ServiceId = enginePreference
            };

            var architectArgs = new KernelArguments(executionSettings)
            {
                ["userPrompt"] = userPrompt,
                ["profileJson"] = profileJson,
                ["domainContext"] = domainContext
            };
            
            var architectResult = await _kernel.InvokeAsync(agentsPlugin["Architect"], architectArgs);
            string planJson = CleanMarkdown(architectResult.GetValue<string>() ?? "{}");

            ExecutionPlan executionPlan;
            try 
            {
                // Katı (Type-Safe) DTO Deserialize işlemi
                executionPlan = JsonSerializer.Deserialize<ExecutionPlan>(planJson) ?? new ExecutionPlan();
                
                if (executionPlan == null)
                    throw new JsonException("Plan Deserialize edilemedi.");

                // Domain Guard & Early Exit Mantığı
                if (executionPlan.Steps == null || executionPlan.Steps.Count == 0)
                {
                    _logger.LogInformation("[Sistem] Domain Guard tetiklendi. Soru bağlam dışı veya kodlanacak adım yok.");
                    string rejectionMessage = !string.IsNullOrWhiteSpace(executionPlan.Goal) ? executionPlan.Goal : "Bağlam dışı istek reddedildi.";
                    await SendStatusAsync(workspaceId, rejectionMessage);
                    return rejectionMessage;
                }

                _logger.LogInformation("[Sistem] Architect planı başarıyla oluşturdu ve doğrulandı.");
                await SendStatusAsync(workspaceId, "Plan oluşturuldu. Coder kodlamaya başlıyor...");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[Sistem Hata] Architect geçerli bir JSON üretemedi.");
                return $"Sistem Hatası: Architect planlaması anlaşılamadı. Lütfen isteğinizi daha net belirtin.\nÜretilen Bozuk İçerik:\n{planJson}";
            }

            // ══════════════════════════════════════════════════════════════
            // ADIM 2: CODER (Kod Üretim Aşaması)
            // ══════════════════════════════════════════════════════════════
            _logger.LogInformation("[Sistem] Coder ajanı kodu yazıyor...");
            
            var coderArgs = new KernelArguments(executionSettings)
            {
                // DTO'yu güvenli JSON'a çevirip Coder'a veriyoruz
                ["planJson"] = JsonSerializer.Serialize(executionPlan)
            };
            
            var coderResult = await _kernel.InvokeAsync(agentsPlugin["Coder"], coderArgs);
            string pythonCode = CleanMarkdown(coderResult.GetValue<string>() ?? "");
            
            if (string.IsNullOrWhiteSpace(pythonCode))
            {
                _logger.LogError("[Sistem Hata] Coder boş kod üretti.");
                return "Sistem Hatası: Coder ajanı kod üretemedi.";
            }
            _logger.LogInformation("[Sistem] Kod üretimi tamamlandı. Docker Sandbox'a gönderiliyor.");
            await SendStatusAsync(workspaceId, "Kod üretildi. Docker kalesinde çalıştırılıyor...");

            // ══════════════════════════════════════════════════════════════
            // ADIM 3: DOCKER İŞLETİMİ VE SELF-HEALING (ONARMA) DÖNGÜSÜ
            // ══════════════════════════════════════════════════════════════
            const int maxRetries = 3;
            int attempt = 0;

            while (attempt <= maxRetries)
            {
                _logger.LogInformation("[Sistem] Docker Sandbox çalıştırılıyor. (Deneme: {Attempt}/{Max})", attempt + 1, maxRetries + 1);
                
                string executionResult = await _dockerPlugin.RunPythonInSandboxAsync(pythonCode, workspaceId);

                if (executionResult.StartsWith("SUCCESS:\n"))
                {
                    _logger.LogInformation("[Sistem] İşlem Başarılı! Docker Sandbox temiz çıktı döndü.");
                    await SendStatusAsync(workspaceId, "İşlem başarıyla tamamlandı!");
                    return executionResult.Replace("SUCCESS:\n", "").Trim();
                }
                else if (executionResult.StartsWith("ERROR:\n"))
                {
                    string errorDetail = executionResult.Replace("ERROR:\n", "").Trim();
                    
                    if (attempt == maxRetries)
                    {
                        _logger.LogWarning("[Sistem] Healer {Max} onarma denemesini doldurdu. Sonsuz döngü engellendi.", maxRetries);
                        await SendStatusAsync(workspaceId, "Hata: Maksimum denemeye ulaşıldı. Onarma başarısız.");
                        return $"Hata: Maksimum onarma (Self-Healing) denemesine ulaşıldı. Lütfen isteğinizi detaylandırın.\n\nSon Alınan Hata:\n{errorDetail}";
                    }

                    _logger.LogWarning("[Sistem] Hata tespit edildi! Healer devrede. (Kalan Hak: {Rem})", maxRetries - attempt);
                    await SendStatusAsync(workspaceId, $"Hata tespit edildi. Healer kodu onarmaya çalışıyor (Deneme {attempt + 1})...");
                    
                    var healerArgs = new KernelArguments(executionSettings)
                    {
                        ["faultyCode"] = pythonCode,
                        ["errorDetails"] = errorDetail
                    };
                    
                    var healerResult = await _kernel.InvokeAsync(agentsPlugin["Healer"], healerArgs);
                    pythonCode = CleanMarkdown(healerResult.GetValue<string>() ?? "");
                    
                    attempt++;
                }
                else
                {
                    _logger.LogError("[Sistem Hata] DockerExecutionPlugin beklenen formatta yanıt dönmedi. Yanıt: {Res}", executionResult);
                    return $"Bilinmeyen Hata Formatı:\n{executionResult}";
                }
            }

            return "Sistem beklenmeyen bir akış durumuyla karşılaştı.";
        }

        private async Task SendStatusAsync(int workspaceId, string message)
        {
            if (_hubContext != null)
            {
                await _hubContext.Clients.Group(workspaceId.ToString()).SendAsync("ReceiveAgentStatus", message);
            }
        }

        /// <summary>
        /// LLM inat edip Markdown formatı (```python ... ```) üretirse, bu tagleri temizler.
        /// </summary>
        private string CleanMarkdown(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            
            input = input.Trim();
            
            if (input.StartsWith("```"))
            {
                int firstNewline = input.IndexOf('\n');
                if (firstNewline != -1)
                {
                    input = input.Substring(firstNewline + 1);
                }
                else
                {
                    input = input.Substring(3);
                }
            }
            
            if (input.EndsWith("```"))
            {
                input = input.Substring(0, input.Length - 3);
            }
            
            return input.Trim();
        }
    }
}
