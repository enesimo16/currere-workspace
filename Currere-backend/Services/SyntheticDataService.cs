using Currere_backend.Data;
using Currere_backend.DTOs;
using Currere_backend.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Services
{
    public class SyntheticDataService : ISyntheticDataService
    {
        private readonly IAiService _aiService;
        private readonly ICodeExecutionService _codeExecutionService;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public SyntheticDataService(
            IAiService aiService,
            ICodeExecutionService codeExecutionService,
            AppDbContext context,
            IWebHostEnvironment env,
            IBackgroundJobClient backgroundJobClient)
        {
            _aiService = aiService;
            _codeExecutionService = codeExecutionService;
            _context = context;
            _env = env;
            _backgroundJobClient = backgroundJobClient;
        }

        public async Task<WorkspaceFile> GenerateDataAsync(int workspaceId, SyntheticDataRequest request)
        {
            // csv ile bitmeli
            // aksi halde hata
            if (!request.FileName.EndsWith(".csv"))
                request.FileName += ".csv";

            string systemPrompt = "";
            string userPrompt = "";

            // modlara göre prompt
            switch (request.Mode)
            {
                case GenerationMode.FastAndFake:
                    systemPrompt = "Sen bir Python veri üreticisisin. SADECE çalıştırılabilir Python kodu yaz. Markdown veya açıklama KULLANMA. Kodlarında SADECE built-in python kütüphaneleri ile pandas, numpy ve faker kütüphanelerini kullanabilirsin. Başka hiçbir 3. parti kütüphane import etme. Ağ bağlantısı (internet) olmadığını unutma.";
                    userPrompt = $"Bana {request.RowCount} satırlık '{request.Prompt}' konulu bir veri seti üret. İstenen Sütunlar: {request.Columns}. Faker kütüphanesini ve Pandas'ı kullan. Veriyi '{request.FileName}' adıyla kaydet.";
                    break;

                case GenerationMode.ZeroShotRealistic:
                    systemPrompt = "Sen kıdemli bir Veri Bilimci ve İstatistikçisin. SADECE çalıştırılabilir Python kodu yaz. Markdown kullanma. Kodlarında SADECE built-in python kütüphaneleri ile pandas, numpy ve faker kütüphanelerini kullanabilirsin. Başka hiçbir 3. parti kütüphane import etme. Ağ bağlantısı (internet) olmadığını unutma.";
                    userPrompt = $"Bana {request.RowCount} satırlık '{request.Prompt}' konulu GERÇEKÇİ bir veri seti üret. İstenen Sütunlar: {request.Columns}. Numpy ve Scipy kullanarak istatistiksel dağılımlar (Gaussian vb.) ve sütunlar arası mantıksal korelasyonlar (covariance) kur. Veriyi '{request.FileName}' adıyla kaydet.";
                    break;

                case GenerationMode.DigitalTwin:
                    if (request.SourceFileId == null) throw new Exception("Digital Twin modu için referans bir dosya (SourceFileId) seçmelisiniz.");

                    var sourceFile = await _context.WorkspaceFiles.FirstOrDefaultAsync(f => f.Id == request.SourceFileId && f.WorkspaceId == workspaceId);
                    if (sourceFile == null) throw new Exception("Referans alınacak kaynak dosya bulunamadı.");

                    systemPrompt = "Sen bir Veri Klonlama (Digital Twin) uzmanısın. SADECE çalıştırılabilir Python kodu yaz. Markdown kullanma. Kodlarında SADECE built-in python kütüphaneleri ile pandas, numpy ve faker kütüphanelerini kullanabilirsin. Başka hiçbir 3. parti kütüphane import etme. Ağ bağlantısı (internet) olmadığını unutma.";
                    userPrompt = $"Çalışma dizininde '{sourceFile.FileName}' adında bir dosya var. Pandas ile bu dosyayı oku, sütunların istatistiksel dağılımlarını (ortalama, varyans, kategorik olasılıklar) ve aralarındaki korelasyonları analiz et. Ardından bu matematiğe %100 sadık kalarak {request.RowCount} satırlık YENİ bir sentetik veri üret ve '{request.FileName}' adıyla kaydet.";
                    break;
            }

            // ai yazar
            var aiResponse = await _aiService.ChatAsync(userPrompt, systemPrompt);

            // AI  ```python ve ``` eklerse, onları temizliyoruz
            var cleanCode = aiResponse.Replace("```python", "").Replace("```", "").Trim();

            // sandbox'ta calıstırıyoruz
            var executionResult = await _codeExecutionService.ExecutePythonCodeAsync(new ExecutionJob { WorkspaceId = workspaceId, Code = cleanCode });

            if (!executionResult.IsSuccess)
                throw new Exception(executionResult.Error);

            // dosya kontrolü 
            // cidden var mı
            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspacePath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());
            var newFilePath = Path.Combine(workspacePath, request.FileName);

            if (!File.Exists(newFilePath))
                throw new Exception("Kod başarıyla çalıştı ancak AI dosyayı kaydetmeyi unuttu veya yanlış yere kaydetti.");

            // db kaydetme ve dataprofilleme
            var workspaceFile = new WorkspaceFile
            {
                WorkspaceId = workspaceId,
                FileName = request.FileName,
                FilePath = newFilePath
            };

            _context.WorkspaceFiles.Add(workspaceFile);
            await _context.SaveChangesAsync();

            // Otonom Hangfire Profiler
            _backgroundJobClient.Enqueue<IDatasetProfilerService>(
                profiler => profiler.ProfileDatasetAsync(workspaceFile.Id, request.FileName)
            );

            return workspaceFile;
        }
    }
}