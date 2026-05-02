using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Currere_backend.Models;
using Currere_backend.DTOs;
using Currere_backend.Hubs; 
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR; 
using Microsoft.Extensions.Logging;
using Currere_backend.Data;

namespace Currere_backend.Services
{
    public class CodeExecutionService : ICodeExecutionService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<TerminalHub> _hubContext;
        private readonly ICodePreProcessorService _codePreProcessorService;
        private readonly AppDbContext _context;
        private readonly ILogger<CodeExecutionService> _logger;

        public CodeExecutionService(
            IWebHostEnvironment env, 
            IHubContext<TerminalHub> hubContext, 
            ICodePreProcessorService codePreProcessorService, 
            AppDbContext context,
            ILogger<CodeExecutionService> logger)
        {
            _env = env;
            _hubContext = hubContext;
            _codePreProcessorService = codePreProcessorService;
            _context = context;
            _logger = logger;
        }

        public async Task<ExecutionResultDto> ExecutePythonCodeAsync(ExecutionJob job)
        {
            var stopwatch = Stopwatch.StartNew();
            int workspaceId = job.WorkspaceId;
            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            try
            {
                // ══════════════════════════════════════════════════════════════
                // ADIM 0: JOB BAŞLATILDI
                // ══════════════════════════════════════════════════════════════
                _logger.LogWarning("[Adım 0] ★ Job başladı. JobId: {JobId}, WorkspaceId: {WsId}", job.JobId, workspaceId);

                string code = job.Code?.Replace("\uFEFF", "").Replace("\r", "") ?? "";

                // ══════════════════════════════════════════════════════════════
                // ADIM 1: AST GÜVENLİK TARAMASI
                // ══════════════════════════════════════════════════════════════
                _logger.LogWarning("[Adım 1] SecurityPreprocessor çağrılıyor...");
                CodePreProcessResultDto preProcessResult;
                try
                {
                    preProcessResult = await _codePreProcessorService.ProcessCodeAsync(code);
                    code = preProcessResult.Code;
                    _logger.LogWarning("[Adım 1.1] Tarama OK. Bağımlılıklar: {Deps}", 
                        preProcessResult.Dependencies?.Any() == true ? string.Join(", ", preProcessResult.Dependencies) : "Yok");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Adım 1 HATA] SecurityPreprocessor patladı!");
                    stopwatch.Stop();
                    return new ExecutionResultDto { Output = "", Error = ex.Message, ErrorType = "SecurityOrSyntaxError", IsSuccess = false, ExecutionTimeMs = stopwatch.ElapsedMilliseconds };
                }

                // ══════════════════════════════════════════════════════════════
                // ADIM 2: WORKSPACE YOLU
                // ══════════════════════════════════════════════════════════════
                var hostWorkspacePath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());
                if (!Directory.Exists(hostWorkspacePath))
                    Directory.CreateDirectory(hostWorkspacePath);

                var dockerWorkspaceBind = hostWorkspacePath.Replace("\\", "/");
                _logger.LogWarning("[Adım 2] Workspace: {Path}", dockerWorkspaceBind);

                // ══════════════════════════════════════════════════════════════
                // ADIM 3: MATPLOTLIB INTERCEPTOR + BASE64
                // ══════════════════════════════════════════════════════════════
                const string matplotlibHeader = "import matplotlib\nmatplotlib.use('Agg')\n";
                if (!code.Contains("matplotlib.use("))
                    code = matplotlibHeader + code;

                code = code.Replace("plt.show()",
                    "plt.savefig('/workspace/output/output_plot.png', bbox_inches='tight', dpi=150)\nplt.close()");

                var codeBytes = new UTF8Encoding(false).GetBytes(code);
                if (codeBytes.Length >= 3 && codeBytes[0] == 0xEF && codeBytes[1] == 0xBB && codeBytes[2] == 0xBF)
                {
                    var clean = new byte[codeBytes.Length - 3];
                    Array.Copy(codeBytes, 3, clean, 0, clean.Length);
                    codeBytes = clean;
                }
                var base64Code = Convert.ToBase64String(codeBytes);
                _logger.LogWarning("[Adım 3] Base64 hazır ({Len} char)", base64Code.Length);

                // ══════════════════════════════════════════════════════════════
                // ADIM 4: ARTIFACT OUTPUT KLASÖRÜ
                // ══════════════════════════════════════════════════════════════
                var hostOutputPath = Path.Combine(webRootPath, "artifacts", job.JobId);
                if (!Directory.Exists(hostOutputPath))
                    Directory.CreateDirectory(hostOutputPath);
                var dockerOutputBind = hostOutputPath.Replace("\\", "/");

                // ══════════════════════════════════════════════════════════════
                // ADIM 5: DOCKER RUN KOMUTU (Native Process — Docker.DotNet YOK)
                // ══════════════════════════════════════════════════════════════
                // docker run --rm --network none
                //   --memory 512m --cpus 0.5
                //   -w /workspace
                //   -v "workspacePath:/workspace/data:ro"
                //   -v "outputPath:/workspace/output:rw"
                //   -e CODE_TO_RUN=base64...
                //   -e PYTHONPATH=/workspace/site-packages
                //   -e PYTHONUNBUFFERED=1
                //   currere-sandbox:latest
                //   python /app/runner.py

                var args = new StringBuilder();
                args.Append("run --rm --network none ");
                args.Append("--memory 512m --cpus 0.5 ");
                args.Append("--user 1000:1000 --read-only --tmpfs /tmp:rw,noexec,nosuid,size=64m ");
                args.Append("--security-opt no-new-privileges --cap-drop ALL ");
                args.Append("--pids-limit 50 ");
                args.Append("--ipc none ");
                args.Append("-w /workspace ");
                args.Append($"-v \"{dockerWorkspaceBind}:/workspace/data:ro\" ");
                args.Append($"-v \"{dockerOutputBind}:/workspace/output:rw\" ");
                args.Append($"-e PYTHONUNBUFFERED=1 ");
                args.Append($"-e PYTHONPATH=/workspace/site-packages ");
                args.Append($"-e CODE_TO_RUN={base64Code} ");
                args.Append("currere-sandbox:latest ");
                args.Append("python /app/runner.py");

                var dockerArgs = args.ToString();
                _logger.LogWarning("[Adım 5] Docker komutu hazırlandı:");
                _logger.LogWarning("[Adım 5.1] docker {Args}", dockerArgs.Length > 200 ? dockerArgs.Substring(0, 200) + "..." : dockerArgs);

                // ══════════════════════════════════════════════════════════════
                // ADIM 6: PROCESS BAŞLAT (Asenkron, Deadlock-Free)
                // ══════════════════════════════════════════════════════════════
                _logger.LogWarning("[Adım 6] Process başlatılıyor...");

                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = dockerArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                process.Start();
                _logger.LogWarning("[Adım 6.1] Process başlatıldı. PID: {PID}", process.Id);

                // Asenkron okuma — ÖNCE oku, SONRA bekle (klasik .NET Process deadlock koruması)
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                // 45 saniye timeout ile bekle
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Timeout! Process'i zorla öldür
                    _logger.LogError("[Adım 6 TIMEOUT] 45 saniye doldu, process öldürülüyor!");
                    try { process.Kill(entireProcessTree: true); } catch { }
                    stopwatch.Stop();
                    return new ExecutionResultDto
                    {
                        Output = "",
                        Error = "Time-out: Kod 45 saniyeden uzun sürdü ve durduruldu.",
                        ErrorType = "TimeoutError",
                        IsSuccess = false,
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                // Process bitti — çıktıları al
                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                _logger.LogWarning("[Adım 7] Process bitti. ExitCode: {Code}, Stdout: {StdLen}b, Stderr: {ErrLen}b",
                    process.ExitCode, stdout.Length, stderr.Length);

                // SignalR'a gönder (post-execution replay)
                if (!string.IsNullOrEmpty(stdout))
                    await _hubContext.Clients.Group(workspaceId.ToString()).SendAsync("ReceiveLog", stdout);

                // ══════════════════════════════════════════════════════════════
                // ADIM 8: JSON PARSE
                // ══════════════════════════════════════════════════════════════
                stopwatch.Stop();

                // runner.py çıktısı: son satır JSON, önceki satırlar [DEBUG] logları
                // JSON'ı bulmak için son satırdan geriye doğru tara
                string rawOutput = "";
                var outputLines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                for (int i = outputLines.Length - 1; i >= 0; i--)
                {
                    var line = outputLines[i].Trim();
                    if (line.StartsWith("{") && line.EndsWith("}"))
                    {
                        rawOutput = line;
                        break;
                    }
                }

                _logger.LogWarning("[Adım 8] JSON aranıyor. Bulunan: {Found}", !string.IsNullOrEmpty(rawOutput) ? "EVET" : "HAYIR");

                bool isSuccess = false;
                string finalOutput = "";
                string errorType = "";

                if (!string.IsNullOrEmpty(rawOutput))
                {
                    try
                    {
                        var jsonResult = JsonDocument.Parse(rawOutput).RootElement;
                        isSuccess = jsonResult.GetProperty("success").GetBoolean();
                        finalOutput = jsonResult.GetProperty("message").GetString() ?? "";
                        errorType = isSuccess ? "" : (jsonResult.TryGetProperty("error_type", out var et) ? et.GetString() : "PythonError");
                        _logger.LogWarning("[Adım 8.1] JSON parse OK. isSuccess: {S}", isSuccess);

                        if (!isSuccess)
                        {
                            _logger.LogError("[Adım 8.1] Python Hatası: {Error}", finalOutput);
                        }
                    }
                    catch
                    {
                        isSuccess = false;
                        finalOutput = stdout + "\n" + stderr;
                        errorType = "SystemCrashError";
                        _logger.LogError("[Adım 8.1] JSON parse BAŞARISIZ");
                    }
                }
                else
                {
                    // JSON bulunamadı — ham çıktıyı hata olarak dön
                    isSuccess = false;
                    finalOutput = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : stdout.Trim();
                    errorType = "SystemCrashError";
                    _logger.LogError("[Adım 8.1] JSON bulunamadı. Stderr: {Err}", stderr.Length > 300 ? stderr.Substring(0, 300) : stderr);
                }

                // ══════════════════════════════════════════════════════════════
                // ADIM 9: ARTIFACT TOPLAMA
                // ══════════════════════════════════════════════════════════════
                var artifactUrls = new List<string>();
                var base64Images = new List<string>();
                if (Directory.Exists(hostOutputPath))
                {
                    foreach (var file in Directory.GetFiles(hostOutputPath))
                    {
                        var fileName = Path.GetFileName(file);
                        artifactUrls.Add($"/artifacts/{job.JobId}/{fileName}");

                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext is ".png" or ".jpg" or ".jpeg" or ".svg")
                        {
                            var bytes = await File.ReadAllBytesAsync(file);
                            base64Images.Add(Convert.ToBase64String(bytes));
                        }
                    }
                }

                _logger.LogWarning("[Adım 9] ★ TAMAMLANDI. isSuccess: {S}, Süre: {Ms}ms", isSuccess, stopwatch.ElapsedMilliseconds);

                return new ExecutionResultDto
                {
                    Output = isSuccess ? finalOutput : "",
                    Error = !isSuccess ? finalOutput : "",
                    ErrorType = errorType,
                    IsSuccess = isSuccess,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    ArtifactUrls = artifactUrls,
                    Images = base64Images
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "[KRİTİK HATA] ★★★ ExecutePythonCodeAsync patladı!");
                var innerMsg = ex.InnerException != null ? $" | Inner: {ex.InnerException.Message}" : "";
                return new ExecutionResultDto
                {
                    Output = "",
                    Error = $"Sistem Hatası: {ex.Message}{innerMsg}",
                    ErrorType = "SystemError",
                    IsSuccess = false,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private string ParsePythonTraceback(string rawError, ref string errorType)
        {
            if (string.IsNullOrWhiteSpace(rawError)) return rawError;

            int lineNumber = 0;
            var lineMatches = System.Text.RegularExpressions.Regex.Matches(rawError, @"line\s+(\d+)");
            if (lineMatches.Count > 0)
                int.TryParse(lineMatches[lineMatches.Count - 1].Groups[1].Value, out lineNumber);

            var lines = rawError.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string lastLine = lines.LastOrDefault()?.Trim() ?? "";
            string errorMessage = lastLine;

            var errorMatch = System.Text.RegularExpressions.Regex.Match(lastLine, @"^([\w\.]+Error|Exception|Warning|SyntaxError):\s*(.*)$");
            if (errorMatch.Success)
            {
                errorType = errorMatch.Groups[1].Value;
                errorMessage = errorMatch.Groups[2].Value;
            }

            return JsonSerializer.Serialize(new { ErrorType = errorType, LineNumber = lineNumber, ErrorMessage = errorMessage });
        }
    }
}