using Currere_backend.Data;
using Microsoft.EntityFrameworkCore;
using Octokit;

namespace Currere_backend.Services
{
    public class GithubService : IGithubService
    {
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IWebHostEnvironment _env;

        public GithubService(AppDbContext context, IEncryptionService encryptionService, IWebHostEnvironment env)
        {
            _context = context;
            _encryptionService = encryptionService;
            _env = env;
        }

        public async Task<string> PushWorkspaceToGithubAsync(int userId, int workspaceId, string repoName, string commitMessage)
        {
            // github token al
            var integration = await _context.UserIntegrations.FirstOrDefaultAsync(i => i.UserId == userId);

            if (integration == null || string.IsNullOrEmpty(integration.GithubToken))
                throw new Exception("GitHub entegrasyonu bulunamadı. Lütfen önce Token'ınızı kaydedin.");

            var decryptedToken = _encryptionService.Decrypt(integration.GithubToken);

            // octokit istemcisi
            var client = new GitHubClient(new ProductHeaderValue("Currere-AI-Agent"))
            {
                Credentials = new Credentials(decryptedToken)
            };

            // new repo
            var newRepo = new NewRepository(repoName)
            {
                AutoInit = true, // README.md ile başlıyor ki commit atalım
                Description = "Bu proje Currere AI Otopilotu tarafından otonom olarak oluşturulmuştur.",
                Private = false  // non public
            };

            var createdRepo = await client.Repository.Create(newRepo);

            var webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var workspaceFolderPath = Path.Combine(webRootPath, "workspaces", workspaceId.ToString());

            if (!Directory.Exists(workspaceFolderPath))
                throw new Exception("Çalışma alanı klasörü bulunamadı veya boş.");

            var files = Directory.GetFiles(workspaceFolderPath);

            foreach (var filePath in files)
            {
                var fileName = Path.GetFileName(filePath);

                // Çok büyük dosyaları ileride es gecicez
                var fileContent = await File.ReadAllTextAsync(filePath);

                // Octokit ile dosyayı repoya ekle
                // her dosya için yeni commit
                var createChangeSet = new CreateFileRequest(
                    $"{commitMessage} - {fileName}",
                    fileContent,
                    "main" // Varsayılan branch
                );

                await client.Repository.Content.CreateFile(createdRepo.Id, fileName, createChangeSet);
            }

            // Başarıyla oluşturulan reponun linkini dönüyoruz
            return createdRepo.HtmlUrl;
        }
    }
}