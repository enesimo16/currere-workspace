using Currere_backend.Data;
using Currere_backend.DTOs;
using Currere_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Currere_backend.Services
{
    public class WorkspaceService : IWorkspaceService
    {
        private readonly AppDbContext _context;

        public WorkspaceService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<WorkspaceResponseDto> CreateWorkspaceAsync(int userId, CreateWorkspaceDto dto)
        {
            var workspace = new Workspace
            {
                UserId = userId,
                Title = dto.Title,
                Format = dto.Format,
                Runtime = dto.Runtime,
                CurrentState = "", 
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Workspaces.Add(workspace);
            await _context.SaveChangesAsync();

            return MapToResponseDto(workspace);
        }

        public async Task<List<WorkspaceResponseDto>> GetUserWorkspacesAsync(int userId)
        {
            // Kullanıcının tüm projelerini getiriyoruz
            var workspaces = await _context.Workspaces
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.UpdatedAt)
                .ToListAsync();

            return workspaces.Select(MapToResponseDto).ToList();
        }

        public async Task<WorkspaceResponseDto?> GetWorkspaceByIdAsync(int workspaceId, int userId)
        {
            // sadece kullanıcı kendi projelerini görebilir
            var workspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId && w.UserId == userId);

            if (workspace == null) return null;

            return MapToResponseDto(workspace);
        }

        public async Task<bool> UpdateWorkspaceAsync(int workspaceId, int userId, UpdateWorkspaceDto dto)
        {
            var workspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId && w.UserId == userId);

            if (workspace == null) return false;

            workspace.Title = string.IsNullOrWhiteSpace(dto.Title) ? workspace.Title : dto.Title;
            workspace.CurrentState = dto.CurrentState;
            workspace.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteWorkspaceAsync(int workspaceId, int userId)
        {
            var workspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.Id == workspaceId && w.UserId == userId);

            if (workspace == null) return false;

            _context.Workspaces.Remove(workspace);
            await _context.SaveChangesAsync();
            return true;
        }

        // DTo çevrimi mapper manuel
        private static WorkspaceResponseDto MapToResponseDto(Workspace w)
        {
            return new WorkspaceResponseDto
            {
                Id = w.Id,
                Title = w.Title,
                Format = w.Format.ToString(),
                Runtime = w.Runtime.ToString(),
                CurrentState = w.CurrentState,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt
            };
        }
    }
}