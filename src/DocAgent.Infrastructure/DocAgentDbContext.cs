using DocAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocAgent.Infrastructure;

public sealed class DocAgentDbContext(DbContextOptions<DocAgentDbContext> options) : DbContext(options)
{
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<RunFile> RunFiles => Set<RunFile>();
    public DbSet<RunResult> RunResults => Set<RunResult>();
    public DbSet<RunReview> RunReviews => Set<RunReview>();
    public DbSet<Feedback> Feedback => Set<Feedback>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Run>(run =>
        {
            // Enums as text: readable in SQL, and safe if someone reorders the enum.
            run.Property(r => r.Status).HasConversion<string>();
            run.Property(r => r.EstimatedCostUsd).HasColumnType("numeric(10,4)");

            run.HasIndex(r => r.CreatedAt).IsDescending();
            run.HasIndex(r => new { r.TemplateId, r.Author, r.Retrieval, r.Detector });

            run.HasMany(r => r.Files).WithOne().HasForeignKey(f => f.RunId);
            run.HasOne(r => r.Result).WithOne().HasForeignKey<RunResult>(r => r.RunId);
            run.HasMany(r => r.Reviews).WithOne().HasForeignKey(r => r.RunId);
            // A rerun outlives the run it came from.
            run.HasOne<Run>().WithMany().HasForeignKey(r => r.SourceRunId).OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<RunFile>(file => file.Property(f => f.Kind).HasConversion<string>());

        model.Entity<RunResult>(result =>
        {
            result.HasKey(r => r.RunId);
            result.Property(r => r.ScreeningJson).HasColumnType("jsonb");
            result.Property(r => r.ModelJson).HasColumnType("jsonb");
            result.Property(r => r.DetectionJson).HasColumnType("jsonb");
            result.Property(r => r.DesignJson).HasColumnType("jsonb");
        });

        model.Entity<RunReview>(review =>
        {
            review.Property(r => r.IssuesJson).HasColumnType("jsonb");
            review.Property(r => r.ChecksJson).HasColumnType("jsonb");
        });

        model.Entity<Feedback>(feedback =>
        {
            feedback.HasOne<Run>().WithMany().HasForeignKey(f => f.RunId);
            feedback.ToTable(table => table.HasCheckConstraint("CK_Feedback_Rating", "\"Rating\" IN (-1, 1)"));
        });

        model.Entity<ChatMessage>(message =>
        {
            message.HasOne<Run>().WithMany().HasForeignKey(m => m.RunId).OnDelete(DeleteBehavior.Cascade);
            // POST /api/ask loads a conversation's history in order.
            message.HasIndex(m => new { m.ConversationId, m.CreatedAt });
            message.Property(m => m.CitationsJson).HasColumnType("jsonb");
        });
    }
}
