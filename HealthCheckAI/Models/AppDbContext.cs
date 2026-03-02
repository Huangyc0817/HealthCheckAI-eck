using Microsoft.EntityFrameworkCore;

namespace HealthCheckAI.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } // 對應資料庫中的 Users 表
        public DbSet<Report> Reports { get; set; } // ✅ 新增報告資料表
        public DbSet<PatientFile> PatientFiles { get; set; }

        public DbSet<ReportFile> ReportFiles { get; set; }
        public DbSet<MfaOtp> MfaOtps { get; set; }

    }
}