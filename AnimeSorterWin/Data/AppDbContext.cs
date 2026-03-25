using AnimeSorterWin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnimeSorterWin.Data;

/// <summary>
/// SQLite EF Core DbContext：用于缓存识别结果，避免重复计算与重复请求 API。
/// </summary>
public sealed class AppDbContext : DbContext
{
    public DbSet<RecognitionCacheEntity> RecognitionCaches => Set<RecognitionCacheEntity>();
    public DbSet<RecognitionCandidatesCacheEntity> RecognitionCandidatesCaches => Set<RecognitionCandidatesCacheEntity>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MD5 作为主键，无需额外配置；这里可扩展索引/约束。
        modelBuilder.Entity<RecognitionCacheEntity>()
            .Property(x => x.Md5)
            .HasMaxLength(64);

        modelBuilder.Entity<RecognitionCandidatesCacheEntity>()
            .Property(x => x.Md5)
            .HasMaxLength(64);

        // CandidatesJson 不做最大长度限制，由于只是少量 JSON。
    }
}

