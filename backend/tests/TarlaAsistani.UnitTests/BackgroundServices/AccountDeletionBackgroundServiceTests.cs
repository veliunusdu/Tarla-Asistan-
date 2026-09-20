using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Infrastructure.BackgroundServices;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.BackgroundServices;

[Trait("Category", "BackgroundServices")]
public class AccountDeletionBackgroundServiceTests
{
    [Fact]
    public async Task ProcessPendingDeletions_WhenFullSuccess_ShouldSetStepTimestamps_AnonymizeUser_AndComplete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            PhoneNumber = "+905551112233",
            FirebaseUid = "fb-uid-123",
            AccountStatus = AccountStatus.DeletionPending,
            Profile = new Profile { FullName = "Test User", Province = "Ankara" }
        };
        var job = new AccountDeletionJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FirebaseUidSnapshot = "fb-uid-123",
            Status = AccountDeletionStatus.Pending,
            User = user
        };
        var media = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            StorageKey = "uploads/users/photo.jpg"
        };
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "hash123"
        };

        var db = new MockDbContextBuilder()
            .WithUsers(user)
            .WithAccountDeletionJobs(job)
            .WithMediaAssets(media)
            .WithRefreshTokens(token)
            .Build();

        var storageMock = new Mock<IMediaStorageService>();
        storageMock.Setup(s => s.DeleteAsync(media.StorageKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var firebaseMock = new Mock<IFirebaseAuthService>();
        firebaseMock.Setup(f => f.RevokeUserSessionsAsync("fb-uid-123", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        firebaseMock.Setup(f => f.DeleteUserAsync("fb-uid-123", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddScoped(_ => db);
        services.AddScoped(_ => storageMock.Object);
        services.AddScoped(_ => firebaseMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var logger = Mock.Of<ILogger<AccountDeletionBackgroundService>>();
        var service = new AccountDeletionBackgroundService(serviceProvider, logger);

        // Act
        await service.ProcessPendingDeletionsAsync(CancellationToken.None);

        // Assert
        job.Status.Should().Be(AccountDeletionStatus.Completed);
        job.MediaDeletedAtUtc.Should().NotBeNull();
        job.FirebaseTokensRevokedAtUtc.Should().NotBeNull();
        job.FirebaseAuthDeletedAtUtc.Should().NotBeNull();
        job.FirestoreAnonymizedAtUtc.Should().BeNull("no real Firestore step runs in backend");
        job.PostgresAnonymizedAtUtc.Should().NotBeNull();
        job.CompletedAtUtc.Should().NotBeNull();
        token.RevokedAtUtc.Should().NotBeNull();
        user.AccountStatus.Should().Be(AccountStatus.Anonymized);
        user.PhoneNumber.Should().StartWith("deleted-");
        user.Profile!.FullName.Should().BeNull();
    }

    [Fact]
    public async Task ProcessPendingDeletions_WhenMediaFails_ShouldMarkRetryRequired_AndNotAnonymize()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            PhoneNumber = "+905551112233",
            FirebaseUid = "fb-uid-fail-media",
            AccountStatus = AccountStatus.DeletionPending
        };
        var job = new AccountDeletionJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FirebaseUidSnapshot = "fb-uid-fail-media",
            Status = AccountDeletionStatus.Pending,
            User = user
        };
        var media = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            StorageKey = "uploads/corrupt.jpg"
        };

        var db = new MockDbContextBuilder()
            .WithUsers(user)
            .WithAccountDeletionJobs(job)
            .WithMediaAssets(media)
            .Build();

        var storageMock = new Mock<IMediaStorageService>();
        storageMock.Setup(s => s.DeleteAsync(media.StorageKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Storage failure"));

        var firebaseMock = new Mock<IFirebaseAuthService>();

        var services = new ServiceCollection();
        services.AddScoped(_ => db);
        services.AddScoped(_ => storageMock.Object);
        services.AddScoped(_ => firebaseMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var logger = Mock.Of<ILogger<AccountDeletionBackgroundService>>();
        var service = new AccountDeletionBackgroundService(serviceProvider, logger);

        // Act
        await service.ProcessPendingDeletionsAsync(CancellationToken.None);

        // Assert
        job.Status.Should().Be(AccountDeletionStatus.RetryRequired);
        job.LastErrorCode.Should().Be("MEDIA_DELETION_FAILED");
        job.MediaDeletedAtUtc.Should().BeNull();
        user.AccountStatus.Should().Be(AccountStatus.DeletionPending, "user should not be anonymized if steps fail");
    }

    [Fact]
    public async Task ProcessPendingDeletions_WhenFirebaseFails_ShouldMarkRetryRequired_AndNotAnonymize()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            PhoneNumber = "+905551112233",
            FirebaseUid = "fb-uid-fail-firebase",
            AccountStatus = AccountStatus.DeletionPending
        };
        var job = new AccountDeletionJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FirebaseUidSnapshot = "fb-uid-fail-firebase",
            Status = AccountDeletionStatus.Pending,
            User = user
        };

        var db = new MockDbContextBuilder()
            .WithUsers(user)
            .WithAccountDeletionJobs(job)
            .Build();

        var storageMock = new Mock<IMediaStorageService>();
        var firebaseMock = new Mock<IFirebaseAuthService>();
        firebaseMock.Setup(f => f.RevokeUserSessionsAsync("fb-uid-fail-firebase", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Firebase API down"));

        var services = new ServiceCollection();
        services.AddScoped(_ => db);
        services.AddScoped(_ => storageMock.Object);
        services.AddScoped(_ => firebaseMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var logger = Mock.Of<ILogger<AccountDeletionBackgroundService>>();
        var service = new AccountDeletionBackgroundService(serviceProvider, logger);

        // Act
        await service.ProcessPendingDeletionsAsync(CancellationToken.None);

        // Assert
        job.Status.Should().Be(AccountDeletionStatus.RetryRequired);
        job.LastErrorCode.Should().Be("FIREBASE_DELETION_FAILED");
        job.FirebaseTokensRevokedAtUtc.Should().BeNull();
        job.FirebaseAuthDeletedAtUtc.Should().BeNull();
        user.AccountStatus.Should().Be(AccountStatus.DeletionPending);
    }

    [Fact]
    public async Task ProcessPendingDeletions_OnRetry_ShouldSkipAlreadyCompletedSteps()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            PhoneNumber = "+905551112233",
            FirebaseUid = "fb-uid-retry",
            AccountStatus = AccountStatus.DeletionPending
        };
        var priorMediaTime = DateTime.UtcNow.AddMinutes(-30);
        var priorRevokeTime = DateTime.UtcNow.AddMinutes(-30);
        var job = new AccountDeletionJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FirebaseUidSnapshot = "fb-uid-retry",
            Status = AccountDeletionStatus.RetryRequired,
            NextRetryAtUtc = DateTime.UtcNow.AddMinutes(-5),
            MediaDeletedAtUtc = priorMediaTime,
            FirebaseTokensRevokedAtUtc = priorRevokeTime,
            FirebaseAuthDeletedAtUtc = null, // failed previously
            User = user
        };
        var media = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            StorageKey = "uploads/skipped.jpg"
        };

        var db = new MockDbContextBuilder()
            .WithUsers(user)
            .WithAccountDeletionJobs(job)
            .WithMediaAssets(media)
            .Build();

        var storageMock = new Mock<IMediaStorageService>();
        var firebaseMock = new Mock<IFirebaseAuthService>();
        firebaseMock.Setup(f => f.DeleteUserAsync("fb-uid-retry", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddScoped(_ => db);
        services.AddScoped(_ => storageMock.Object);
        services.AddScoped(_ => firebaseMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var logger = Mock.Of<ILogger<AccountDeletionBackgroundService>>();
        var service = new AccountDeletionBackgroundService(serviceProvider, logger);

        // Act
        await service.ProcessPendingDeletionsAsync(CancellationToken.None);

        // Assert
        storageMock.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "media was already deleted in prior attempt");
        firebaseMock.Verify(f => f.RevokeUserSessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "sessions were already revoked in prior attempt");
        firebaseMock.Verify(f => f.DeleteUserAsync("fb-uid-retry", It.IsAny<CancellationToken>()), Times.Once);

        job.Status.Should().Be(AccountDeletionStatus.Completed);
        job.MediaDeletedAtUtc.Should().Be(priorMediaTime);
        job.FirebaseTokensRevokedAtUtc.Should().Be(priorRevokeTime);
        job.FirebaseAuthDeletedAtUtc.Should().NotBeNull();
        user.AccountStatus.Should().Be(AccountStatus.Anonymized);
    }
}
