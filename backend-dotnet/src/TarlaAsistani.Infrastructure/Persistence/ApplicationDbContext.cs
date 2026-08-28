using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TaskStatus = TarlaAsistani.Domain.Enums.TaskStatus; // disambiguate from System.Threading.Tasks.TaskStatus

namespace TarlaAsistani.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ── Core ────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<FirebaseLinkApproval> FirebaseLinkApprovals => Set<FirebaseLinkApproval>();
    public DbSet<AccountDeletionJob> AccountDeletionJobs => Set<AccountDeletionJob>();

    // ── Farm ────────────────────────────────────────────────
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<CropPeriod> CropPeriods => Set<CropPeriod>();
    public DbSet<WeatherSnapshot> WeatherSnapshots => Set<WeatherSnapshot>();

    // ── Activity ────────────────────────────────────────────
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityRevision> ActivityRevisions => Set<ActivityRevision>();

    // ── Tasks ───────────────────────────────────────────────
    public DbSet<FarmTask> FarmTasks => Set<FarmTask>();

    // ── Support Cases ────────────────────────────────────────
    public DbSet<SupportCase> SupportCases => Set<SupportCase>();
    public DbSet<CaseMessage> CaseMessages => Set<CaseMessage>();
    public DbSet<CaseMedia> CaseMedia => Set<CaseMedia>();
    public DbSet<CaseMessageMedia> CaseMessageMedia => Set<CaseMessageMedia>();

    // ── Media ────────────────────────────────────────────────
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    // ── Notifications & Devices ──────────────────────────────
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // ── Idempotency ──────────────────────────────────────────
    public DbSet<ClientOperation> ClientOperations => Set<ClientOperation>();

    // ── Pilot ────────────────────────────────────────────────
    public DbSet<PilotFeedback> PilotFeedbacks => Set<PilotFeedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ══════════════════════════════════════════════════════
        // USER
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.PhoneNumber).IsUnique();
            entity.HasIndex(u => u.FirebaseUid).IsUnique();
            entity.HasIndex(u => u.AnonymizedSubjectId).IsUnique();

            entity.Property(u => u.PhoneNumber).HasMaxLength(64).IsRequired();
            entity.Property(u => u.FirebaseUid).HasMaxLength(128);
            entity.Property(u => u.AnonymizedSubjectId).HasMaxLength(64);
            entity.Property(u => u.AccountStatus)
                  .HasConversion<string>()
                  .HasDefaultValue(AccountStatus.Active);
            entity.Property(u => u.Role)
                  .HasConversion<string>()
                  .HasDefaultValue(UserRole.Farmer);
        });

        // ══════════════════════════════════════════════════════
        // PROFILE (shared PK with User)
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("profiles");
            entity.HasKey(p => p.UserId);

            entity.Property(p => p.FullName).HasMaxLength(120);
            entity.Property(p => p.Province).HasMaxLength(80);
            entity.Property(p => p.District).HasMaxLength(80);

            entity.HasOne(p => p.User)
                  .WithOne(u => u.Profile)
                  .HasForeignKey<Profile>(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // REFRESH TOKEN
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(rt => rt.Id);
            entity.HasIndex(rt => rt.TokenHash).IsUnique();
            entity.HasIndex(rt => rt.FamilyId);

            entity.Property(rt => rt.TokenHash).HasMaxLength(64).IsRequired();

            entity.HasOne(rt => rt.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // OTP CODE
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<OtpCode>(entity =>
        {
            entity.ToTable("otp_codes");
            entity.HasKey(o => o.Id);
            entity.HasIndex(o => o.PhoneNumber);
        });

        // ══════════════════════════════════════════════════════
        // FIREBASE LINK APPROVAL
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<FirebaseLinkApproval>(entity =>
        {
            entity.ToTable("firebase_link_approvals");
            entity.HasKey(fla => fla.Id);

            // Partial unique indexes: one unconsumed approval per user/uid
            entity.HasIndex(fla => fla.UserId)
                  .IsUnique()
                  .HasFilter("\"ConsumedAtUtc\" IS NULL")
                  .HasDatabaseName("uq_firebase_link_approvals_one_unconsumed_per_user");

            entity.HasIndex(fla => fla.FirebaseUid)
                  .IsUnique()
                  .HasFilter("\"ConsumedAtUtc\" IS NULL")
                  .HasDatabaseName("uq_firebase_link_approvals_one_unconsumed_per_uid");

            entity.HasIndex(fla => fla.ExpiresAtUtc);
            entity.Property(fla => fla.FirebaseUid).HasMaxLength(128).IsRequired();
            entity.Property(fla => fla.ApprovedBy).HasMaxLength(120).IsRequired();

            entity.HasOne(fla => fla.User)
                  .WithMany(u => u.FirebaseLinkApprovals)
                  .HasForeignKey(fla => fla.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // ACCOUNT DELETION JOB
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<AccountDeletionJob>(entity =>
        {
            entity.ToTable("account_deletion_jobs");
            entity.HasKey(j => j.Id);
            entity.HasIndex(j => j.UserId).IsUnique();

            // Composite index for the retry scheduler worker query
            entity.HasIndex(j => new { j.Status, j.NextRetryAtUtc, j.LeaseUntilUtc, j.CreatedAtUtc })
                  .HasDatabaseName("ix_account_deletion_jobs_retry_schedule");

            entity.Property(j => j.FirebaseUidSnapshot).HasMaxLength(128);
            entity.Property(j => j.LastErrorCode).HasMaxLength(80);
            entity.Property(j => j.ProcessingOwnerToken).HasMaxLength(64);
            entity.Property(j => j.Status)
                  .HasConversion<string>()
                  .HasDefaultValue(AccountDeletionStatus.Pending);

            entity.HasOne(j => j.User)
                  .WithOne(u => u.AccountDeletionJob)
                  .HasForeignKey<AccountDeletionJob>(j => j.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // FARM
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<Farm>(entity =>
        {
            entity.ToTable("farms");
            entity.HasKey(f => f.Id);

            // Composite indexes for listing and duplicate name warnings
            entity.HasIndex(f => new { f.OwnerId, f.ArchivedAt })
                  .HasDatabaseName("ix_farms_owner_archived");
            entity.HasIndex(f => new { f.OwnerId, f.Name })
                  .HasDatabaseName("ix_farms_owner_name");

            entity.Property(f => f.Name).HasMaxLength(120).IsRequired();
            entity.Property(f => f.SoilType).HasMaxLength(80);
            entity.Property(f => f.Note).HasMaxLength(1000);

            entity.HasOne(f => f.Owner)
                  .WithMany(u => u.Farms)
                  .HasForeignKey(f => f.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // CROP PERIOD
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<CropPeriod>(entity =>
        {
            entity.ToTable("crop_periods");
            entity.HasKey(cp => cp.Id);

            entity.HasIndex(cp => new { cp.FarmId, cp.Status })
                  .HasDatabaseName("ix_crop_periods_farm_status");
            entity.HasIndex(cp => new { cp.FarmId, cp.PlantedAt })
                  .HasDatabaseName("ix_crop_periods_farm_planted");

            // Partial unique index: only one ACTIVE crop period per farm
            entity.HasIndex(cp => new { cp.FarmId, cp.Status })
                  .IsUnique()
                  .HasFilter("\"Status\" = 0") // 0 = Active enum value
                  .HasDatabaseName("uq_crop_periods_one_active_per_farm");

            entity.Property(cp => cp.Variety).HasMaxLength(120);
            entity.Property(cp => cp.Status)
                  .HasConversion<string>()
                  .HasDefaultValue(CropPeriodStatus.Active);
            entity.Property(cp => cp.CropType).HasConversion<string>();

            entity.HasOne(cp => cp.Farm)
                  .WithMany(f => f.CropPeriods)
                  .HasForeignKey(cp => cp.FarmId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // WEATHER SNAPSHOT
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<WeatherSnapshot>(entity =>
        {
            entity.ToTable("weather_snapshots");
            entity.HasKey(ws => ws.Id);
            entity.HasIndex(ws => new { ws.FarmId, ws.FetchedAtUtc })
                  .HasDatabaseName("ix_weather_snapshots_farm_fetched");

            entity.Property(ws => ws.Provider).HasMaxLength(50).IsRequired();
            entity.Property(ws => ws.Payload).HasColumnType("jsonb");

            entity.HasOne(ws => ws.Farm)
                  .WithMany(f => f.WeatherSnapshots)
                  .HasForeignKey(ws => ws.FarmId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // ACTIVITY
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.ToTable("activities");
            entity.HasKey(a => a.Id);

            entity.HasIndex(a => new { a.FarmId, a.OccurredAtUtc })
                  .HasDatabaseName("ix_activities_farm_occurred");
            entity.HasIndex(a => new { a.FarmId, a.ArchivedAtUtc })
                  .HasDatabaseName("ix_activities_farm_archived");

            // Unique task_id per activity (one activity per task completion)
            entity.HasIndex(a => a.TaskId)
                  .IsUnique()
                  .HasFilter("\"TaskId\" IS NOT NULL")
                  .HasDatabaseName("uq_activities_task_id");

            // Idempotency: unique (CreatedById, ClientOperationId) when not null
            entity.HasIndex(a => new { a.CreatedById, a.ClientOperationId })
                  .IsUnique()
                  .HasFilter("\"ClientOperationId\" IS NOT NULL")
                  .HasDatabaseName("uq_activities_client_operation");

            entity.Property(a => a.ActivityType).HasConversion<string>();
            entity.Property(a => a.Status)
                  .HasConversion<string>()
                  .HasDefaultValue(ActivityStatus.Confirmed);
            entity.Property(a => a.Source)
                  .HasConversion<string>()
                  .HasDefaultValue(ActivitySource.Manual);
            entity.Property(a => a.Unit).HasMaxLength(40);
            entity.Property(a => a.PhotoUrl).HasMaxLength(2048);
            entity.Property(a => a.VoiceUrl).HasMaxLength(2048);
            entity.Property(a => a.PerformedBy).HasMaxLength(120);

            entity.HasOne(a => a.Farm)
                  .WithMany(f => f.Activities)
                  .HasForeignKey(a => a.FarmId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.CropPeriod)
                  .WithMany()
                  .HasForeignKey(a => a.CropPeriodId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.CreatedBy)
                  .WithMany()
                  .HasForeignKey(a => a.CreatedById)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ══════════════════════════════════════════════════════
        // ACTIVITY REVISION
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<ActivityRevision>(entity =>
        {
            entity.ToTable("activity_revisions");
            entity.HasKey(ar => ar.Id);
            entity.HasIndex(ar => new { ar.ActivityId, ar.ChangedAtUtc })
                  .HasDatabaseName("ix_activity_revisions_activity_changed");

            entity.Property(ar => ar.PreviousValues).HasColumnType("jsonb");

            entity.HasOne(ar => ar.Activity)
                  .WithMany(a => a.Revisions)
                  .HasForeignKey(ar => ar.ActivityId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ar => ar.ChangedBy)
                  .WithMany()
                  .HasForeignKey(ar => ar.ChangedById)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ══════════════════════════════════════════════════════
        // FARM TASK
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<FarmTask>(entity =>
        {
            entity.ToTable("tasks");
            entity.HasKey(t => t.Id);

            entity.HasIndex(t => new { t.FarmId, t.DueDate, t.Status })
                  .HasDatabaseName("ix_tasks_farm_due_status");
            entity.HasIndex(t => new { t.FarmId, t.CreatedAtUtc })
                  .HasDatabaseName("ix_tasks_farm_created");

            // Unique dedupe key per (farm, due_date, dedupe_key)
            entity.HasIndex(t => new { t.FarmId, t.DueDate, t.DedupeKey })
                  .IsUnique()
                  .HasDatabaseName("uq_tasks_farm_due_dedupe");

            entity.Property(t => t.Title).HasMaxLength(160).IsRequired();
            entity.Property(t => t.DedupeKey).HasMaxLength(64).IsRequired();
            entity.Property(t => t.NotAppliedReason).HasMaxLength(500);
            entity.Property(t => t.CompletionNote).HasMaxLength(1000);
            entity.Property(t => t.PhotoUrl).HasMaxLength(2048);
            entity.Property(t => t.Priority).HasConversion<string>();
            entity.Property(t => t.Status)
                  .HasConversion<string>()
                  .HasDefaultValue(TaskStatus.New);
            entity.Property(t => t.Source).HasConversion<string>();
            entity.Property(t => t.Confidence)
                  .HasConversion<string>()
                  .HasDefaultValue(TaskConfidence.Medium);

            // Computed property — never persisted
            entity.Ignore(t => t.ExpertReviewRecommended);

            entity.HasOne(t => t.Farm)
                  .WithMany(f => f.Tasks)
                  .HasForeignKey(t => t.FarmId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.CropPeriod)
                  .WithMany()
                  .HasForeignKey(t => t.CropPeriodId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.CreatedBy)
                  .WithMany()
                  .HasForeignKey(t => t.CreatedById)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ══════════════════════════════════════════════════════
        // MEDIA ASSET
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<MediaAsset>(entity =>
        {
            entity.ToTable("media_assets");
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.OwnerId, m.CreatedAtUtc })
                  .HasDatabaseName("ix_media_assets_owner_created");
            entity.HasIndex(m => m.StorageKey).IsUnique();

            entity.Property(m => m.OriginalName).HasMaxLength(255).IsRequired();
            entity.Property(m => m.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(m => m.StorageKey).HasMaxLength(255).IsRequired();
            entity.Property(m => m.ChecksumSha256).HasMaxLength(64).IsRequired();

            // Computed property — never persisted
            entity.Ignore(m => m.Url);

            entity.HasOne(m => m.Owner)
                  .WithMany()
                  .HasForeignKey(m => m.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // SUPPORT CASE
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<SupportCase>(entity =>
        {
            entity.ToTable("support_cases");
            entity.HasKey(sc => sc.Id);
            entity.HasIndex(sc => new { sc.Status, sc.Priority })
                  .HasDatabaseName("ix_support_cases_status_priority");
            entity.HasIndex(sc => new { sc.FarmId, sc.CreatedAtUtc })
                  .HasDatabaseName("ix_support_cases_farm_created");

            entity.Property(sc => sc.Title).HasMaxLength(160).IsRequired();
            entity.Property(sc => sc.Category).HasConversion<string>();
            entity.Property(sc => sc.Priority)
                  .HasConversion<string>()
                  .HasDefaultValue(CasePriority.Medium);
            entity.Property(sc => sc.Status)
                  .HasConversion<string>()
                  .HasDefaultValue(CaseStatus.Open);

            entity.HasOne(sc => sc.Farm)
                  .WithMany()
                  .HasForeignKey(sc => sc.FarmId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Disambiguate multiple User FKs
            entity.HasOne(sc => sc.CreatedBy)
                  .WithMany()
                  .HasForeignKey(sc => sc.CreatedById)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sc => sc.AssignedExpert)
                  .WithMany()
                  .HasForeignKey(sc => sc.AssignedExpertId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(sc => sc.Messages)
                  .WithOne(m => m.Case)
                  .HasForeignKey(m => m.CaseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(sc => sc.MediaLinks)
                  .WithOne(cm => cm.Case)
                  .HasForeignKey(cm => cm.CaseId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // CASE MESSAGE
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<CaseMessage>(entity =>
        {
            entity.ToTable("case_messages");
            entity.HasKey(cm => cm.Id);
            entity.HasIndex(cm => new { cm.CaseId, cm.CreatedAtUtc })
                  .HasDatabaseName("ix_case_messages_case_created");

            entity.Property(cm => cm.MessageType)
                  .HasConversion<string>()
                  .HasDefaultValue(CaseMessageType.Comment);

            entity.HasOne(cm => cm.Sender)
                  .WithMany()
                  .HasForeignKey(cm => cm.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(cm => cm.MediaLinks)
                  .WithOne(cmm => cmm.Message)
                  .HasForeignKey(cmm => cmm.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // CASE MEDIA (join table)
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<CaseMedia>(entity =>
        {
            entity.ToTable("case_media");
            entity.HasKey(cm => new { cm.CaseId, cm.MediaId });

            entity.HasOne(cm => cm.Case)
                  .WithMany(sc => sc.MediaLinks)
                  .HasForeignKey(cm => cm.CaseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cm => cm.Media)
                  .WithMany()
                  .HasForeignKey(cm => cm.MediaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // CASE MESSAGE MEDIA (join table)
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<CaseMessageMedia>(entity =>
        {
            entity.ToTable("case_message_media");
            entity.HasKey(cmm => new { cmm.MessageId, cmm.MediaId });

            entity.HasOne(cmm => cmm.Message)
                  .WithMany(cm => cm.MediaLinks)
                  .HasForeignKey(cmm => cmm.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cmm => cmm.Media)
                  .WithMany()
                  .HasForeignKey(cmm => cmm.MediaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // CLIENT OPERATION (idempotency)
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<ClientOperation>(entity =>
        {
            entity.ToTable("client_operations");
            entity.HasKey(co => co.Id);

            entity.HasIndex(co => new { co.ActorId, co.ClientOperationId })
                  .IsUnique()
                  .HasDatabaseName("uq_client_operations_actor_key");
            entity.HasIndex(co => new { co.ActorId, co.CreatedAtUtc })
                  .HasDatabaseName("ix_client_operations_actor_created");

            entity.Property(co => co.Scope).HasMaxLength(80).IsRequired();
            entity.Property(co => co.PayloadHash).HasMaxLength(64).IsRequired();
            entity.Property(co => co.ResourceType).HasMaxLength(50).IsRequired();

            entity.HasOne(co => co.Actor)
                  .WithMany()
                  .HasForeignKey(co => co.ActorId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // DEVICE TOKEN
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.ToTable("device_tokens");
            entity.HasKey(dt => dt.Id);
            entity.HasIndex(dt => new { dt.UserId, dt.Active })
                  .HasDatabaseName("ix_device_tokens_user_active");
            entity.HasIndex(dt => dt.Token).IsUnique();

            entity.Property(dt => dt.Token).HasMaxLength(512).IsRequired();
            entity.Property(dt => dt.Platform).HasConversion<string>();

            entity.HasOne(dt => dt.User)
                  .WithMany()
                  .HasForeignKey(dt => dt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // NOTIFICATION
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(n => n.Id);
            entity.HasIndex(n => new { n.UserId, n.CreatedAtUtc })
                  .HasDatabaseName("ix_notifications_user_created");
            entity.HasIndex(n => new { n.Status, n.CreatedAtUtc })
                  .HasDatabaseName("ix_notifications_status_created");
            entity.HasIndex(n => n.DedupeKey).IsUnique();

            entity.Property(n => n.Title).HasMaxLength(160).IsRequired();
            entity.Property(n => n.Body).HasMaxLength(1000).IsRequired();
            entity.Property(n => n.DeepLink).HasMaxLength(500).IsRequired();
            entity.Property(n => n.DedupeKey).HasMaxLength(160).IsRequired();
            entity.Property(n => n.ProviderMessageId).HasMaxLength(255);
            entity.Property(n => n.LastError).HasMaxLength(1000);
            entity.Property(n => n.Data).HasColumnType("jsonb");
            entity.Property(n => n.NotificationType).HasConversion<string>();
            entity.Property(n => n.Status)
                  .HasConversion<string>()
                  .HasDefaultValue(NotificationStatus.Pending);

            entity.HasOne(n => n.User)
                  .WithMany()
                  .HasForeignKey(n => n.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // PILOT FEEDBACK
        // ══════════════════════════════════════════════════════
        modelBuilder.Entity<PilotFeedback>(entity =>
        {
            entity.ToTable("pilot_feedback");
            entity.HasKey(pf => pf.Id);
            entity.HasIndex(pf => new { pf.FeedbackType, pf.CreatedAtUtc })
                  .HasDatabaseName("ix_pilot_feedback_type_created");
            entity.HasIndex(pf => new { pf.Status, pf.CreatedAtUtc })
                  .HasDatabaseName("ix_pilot_feedback_status_created");

            entity.Property(pf => pf.FeedbackType).HasConversion<string>();
            entity.Property(pf => pf.Status)
                  .HasConversion<string>()
                  .HasDefaultValue(FeedbackStatus.Open);

            // Disambiguate multiple User FKs
            entity.HasOne(pf => pf.CreatedBy)
                  .WithMany()
                  .HasForeignKey(pf => pf.CreatedById)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pf => pf.ReviewedBy)
                  .WithMany()
                  .HasForeignKey(pf => pf.ReviewedById)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(pf => pf.RelatedTask)
                  .WithMany()
                  .HasForeignKey(pf => pf.RelatedTaskId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(pf => pf.RelatedCase)
                  .WithMany()
                  .HasForeignKey(pf => pf.RelatedCaseId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }

    public async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.IsRelational())
        {
            return await Database.BeginTransactionAsync(cancellationToken);
        }

        return new NoOpTransaction();
    }

    private sealed class NoOpTransaction : Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public void Commit() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}