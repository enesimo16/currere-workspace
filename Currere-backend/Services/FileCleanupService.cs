using Currere_backend.Data;
using Microsoft.EntityFrameworkCore;

public class FileCleanupService
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public FileCleanupService(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public async Task CleanupExpiredFilesAsync()
    {
        var now = DateTime.UtcNow;
        var expiredFiles = await _context.WorkspaceFiles
            .Where(f => f.ExpiresAt <= now)
            .ToListAsync();

        foreach (var file in expiredFiles)
        {

            var filePath = Path.Combine(_env.WebRootPath, "workspaces", file.WorkspaceId.ToString(), file.FileName);
            if (File.Exists(filePath)) File.Delete(filePath);

            _context.WorkspaceFiles.Remove(file);
        }

        await _context.SaveChangesAsync();
    }
}