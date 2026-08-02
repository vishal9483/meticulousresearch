using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Data;

/// <summary>
/// EF Core context over the relational tables of the MeticulousResearch data model (SPEC §5).
/// FTS5 virtual tables and their sync triggers are NOT modeled here (EF cannot model virtual
/// tables); they are created by a raw-SQL migration in the <see cref="Migrations"/> runner.
/// Table and column names are pinned so downstream features can assert on them.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<ArtifactVersion> ArtifactVersions => Set<ArtifactVersion>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Project>(e =>
        {
            e.ToTable("Project");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.CustomInstructions).HasColumnName("custom_instructions");
            e.Property(x => x.DefaultModel).HasColumnName("default_model");
            e.Property(x => x.Color).HasColumnName("color");
            e.Property(x => x.Archived).HasColumnName("archived");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        b.Entity<Resource>(e =>
        {
            e.ToTable("Resource");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.SourceUri).HasColumnName("source_uri");
            e.Property(x => x.BlobPath).HasColumnName("blob_path");
            e.Property(x => x.ExtractedPath).HasColumnName("extracted_path");
            e.Property(x => x.ByteSize).HasColumnName("byte_size");
            e.Property(x => x.TokenEstimate).HasColumnName("token_estimate");
            e.Property(x => x.Enabled).HasColumnName("enabled");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ProjectId);
        });

        b.Entity<Conversation>(e =>
        {
            e.ToTable("Conversation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.ModelDefault).HasColumnName("model_default");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ProjectId);
        });

        b.Entity<Message>(e =>
        {
            e.ToTable("Message");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ConversationId).HasColumnName("conversation_id");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.Model).HasColumnName("model");
            e.Property(x => x.TokensIn).HasColumnName("tokens_in");
            e.Property(x => x.TokensOut).HasColumnName("tokens_out");
            e.Property(x => x.TokensCacheRead).HasColumnName("tokens_cache_read");
            e.Property(x => x.TokensCacheWrite).HasColumnName("tokens_cache_write");
            e.Property(x => x.CostUsd).HasColumnName("cost_usd");
            e.Property(x => x.LatencyMs).HasColumnName("latency_ms");
            e.Property(x => x.ResourceScopeJson).HasColumnName("resource_scope_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ConversationId);
        });

        b.Entity<Artifact>(e =>
        {
            e.ToTable("Artifact");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.CurrentVersionId).HasColumnName("current_version_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ProjectId);
        });

        b.Entity<ArtifactVersion>(e =>
        {
            e.ToTable("ArtifactVersion");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ArtifactId).HasColumnName("artifact_id");
            e.Property(x => x.VersionNo).HasColumnName("version_no");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.ContentFormat).HasColumnName("content_format");
            e.Property(x => x.Model).HasColumnName("model");
            e.Property(x => x.Prompt).HasColumnName("prompt");
            e.Property(x => x.TokensIn).HasColumnName("tokens_in");
            e.Property(x => x.TokensOut).HasColumnName("tokens_out");
            e.Property(x => x.CostUsd).HasColumnName("cost_usd");
            e.Property(x => x.ResourceScopeJson).HasColumnName("resource_scope_json");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ArtifactId);
            e.HasIndex(x => new { x.ArtifactId, x.VersionNo }).IsUnique();
        });

        b.Entity<Setting>(e =>
        {
            e.ToTable("Setting");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasColumnName("key");
            e.Property(x => x.Value).HasColumnName("value");
        });
    }
}
