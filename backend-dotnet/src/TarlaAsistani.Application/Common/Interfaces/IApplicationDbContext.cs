using Microsoft.EntityFrameworkCore;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // ── Core ────────────────────────────────────────────────
    DbSet<User> Users { get; }
    DbSet<Profile> Profiles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<OtpCode> OtpCodes { get; }
    DbSet<FirebaseLinkApproval> FirebaseLinkApprovals { get; }
    DbSet<AccountDeletionJob> AccountDeletionJobs { get; }

    // ── Farm ────────────────────────────────────────────────
    DbSet<Farm> Farms { get; }
    DbSet<CropPeriod> CropPeriods { get; }
    DbSet<WeatherSnapshot> WeatherSnapshots { get; }

    // ── Activity ────────────────────────────────────────────
    DbSet<Activity> Activities { get; }
    DbSet<ActivityRevision> ActivityRevisions { get; }

    // ── Tasks ───────────────────────────────────────────────
    DbSet<FarmTask> FarmTasks { get; }

    // ── Support Cases ────────────────────────────────────────
    DbSet<SupportCase> SupportCases { get; }
    DbSet<CaseMessage> CaseMessages { get; }
    DbSet<CaseMedia> CaseMedia { get; }
    DbSet<CaseMessageMedia> CaseMessageMedia { get; }

    // ── Media ────────────────────────────────────────────────
    DbSet<MediaAsset> MediaAssets { get; }

    // ── Notifications & Devices ──────────────────────────────
    DbSet<DeviceToken> DeviceTokens { get; }
    DbSet<Notification> Notifications { get; }

    // ── Idempotency ──────────────────────────────────────────
    DbSet<ClientOperation> ClientOperations { get; }

    // ── Pilot ────────────────────────────────────────────────
    DbSet<PilotFeedback> PilotFeedbacks { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
