using Currere_backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Services
{
    public class ProfileJobService : IProfileJobService
    {
        private readonly AppDbContext _context;
        private readonly IDatasetProfilerService _profilerService;

        public ProfileJobService(AppDbContext context, IDatasetProfilerService profilerService)
        {
            _context = context;
            _profilerService = profilerService;
        }

        public async Task GenerateAndSaveProfileAsync(int workspaceId, string uniqueFileName, int fileId)
        {
            try
            {
                var profileJson = await _profilerService.ProfileDatasetAsync(workspaceId, uniqueFileName);

                var workspaceFile = await _context.WorkspaceFiles.FirstOrDefaultAsync(f => f.Id == fileId);

                if (workspaceFile != null)
                {
                    workspaceFile.ProfileJson = profileJson;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // sistem çökmemesi için
                Console.WriteLine($"Arka planda profil çıkarılırken hata: {ex.Message}");
                throw;
            }
        }
    }
}