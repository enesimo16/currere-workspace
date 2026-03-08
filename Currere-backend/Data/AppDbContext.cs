using Microsoft.EntityFrameworkCore;
using Currere_backend.Models;

namespace Currere_backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }





        public DbSet<Workspace> Workspaces { get; set; }
        public DbSet<WorkspaceSecret> WorkspaceSecrets { get; set; }
        public DbSet<WorkspaceFile> WorkspaceFiles { get; set; }
        public DbSet<WorkspaceSnapshot> WorkspaceSnapshots { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
    }
}