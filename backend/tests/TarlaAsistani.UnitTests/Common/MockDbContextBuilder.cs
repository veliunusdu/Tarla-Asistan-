using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.UnitTests.Common;

public class MockDbContextBuilder
{
    private readonly Mock<IApplicationDbContext> _mockDb = new();

    private List<User> _users = new();
    private List<Profile> _profiles = new();
    private List<RefreshToken> _refreshTokens = new();
    private List<OtpCode> _otpCodes = new();
    private List<FirebaseLinkApproval> _firebaseLinkApprovals = new();
    private List<AccountDeletionJob> _accountDeletionJobs = new();

    private List<Farm> _farms = new();
    private List<CropPeriod> _cropPeriods = new();
    private List<WeatherSnapshot> _weatherSnapshots = new();

    private List<Activity> _activities = new();
    private List<ActivityRevision> _activityRevisions = new();

    private List<FarmTask> _farmTasks = new();

    private List<SupportCase> _supportCases = new();
    private List<CaseMessage> _caseMessages = new();
    private List<CaseMedia> _caseMedia = new();
    private List<CaseMessageMedia> _caseMessageMedia = new();

    private List<MediaAsset> _mediaAssets = new();

    private List<DeviceToken> _deviceTokens = new();
    private List<Notification> _notifications = new();

    private List<PilotFeedback> _pilotFeedbacks = new();

    public MockDbContextBuilder WithUsers(params User[] users)
    {
        _users.AddRange(users);
        return this;
    }

    public MockDbContextBuilder WithProfiles(params Profile[] profiles)
    {
        _profiles.AddRange(profiles);
        return this;
    }

    public MockDbContextBuilder WithRefreshTokens(params RefreshToken[] tokens)
    {
        _refreshTokens.AddRange(tokens);
        return this;
    }

    public MockDbContextBuilder WithOtpCodes(params OtpCode[] otps)
    {
        _otpCodes.AddRange(otps);
        return this;
    }

    public MockDbContextBuilder WithFirebaseLinkApprovals(params FirebaseLinkApproval[] approvals)
    {
        _firebaseLinkApprovals.AddRange(approvals);
        return this;
    }

    public MockDbContextBuilder WithAccountDeletionJobs(params AccountDeletionJob[] jobs)
    {
        _accountDeletionJobs.AddRange(jobs);
        return this;
    }

    public MockDbContextBuilder WithFarms(params Farm[] farms)
    {
        _farms.AddRange(farms);
        return this;
    }

    public MockDbContextBuilder WithCropPeriods(params CropPeriod[] cropPeriods)
    {
        _cropPeriods.AddRange(cropPeriods);
        return this;
    }

    public MockDbContextBuilder WithWeatherSnapshots(params WeatherSnapshot[] snapshots)
    {
        _weatherSnapshots.AddRange(snapshots);
        return this;
    }

    public MockDbContextBuilder WithActivities(params Activity[] activities)
    {
        _activities.AddRange(activities);
        return this;
    }

    public MockDbContextBuilder WithActivityRevisions(params ActivityRevision[] revisions)
    {
        _activityRevisions.AddRange(revisions);
        return this;
    }

    public MockDbContextBuilder WithFarmTasks(params FarmTask[] tasks)
    {
        _farmTasks.AddRange(tasks);
        return this;
    }

    public MockDbContextBuilder WithSupportCases(params SupportCase[] cases)
    {
        _supportCases.AddRange(cases);
        return this;
    }

    public MockDbContextBuilder WithCaseMessages(params CaseMessage[] messages)
    {
        _caseMessages.AddRange(messages);
        return this;
    }

    public MockDbContextBuilder WithCaseMedia(params CaseMedia[] caseMedia)
    {
        _caseMedia.AddRange(caseMedia);
        return this;
    }

    public MockDbContextBuilder WithCaseMessageMedia(params CaseMessageMedia[] caseMessageMedia)
    {
        _caseMessageMedia.AddRange(caseMessageMedia);
        return this;
    }

    public MockDbContextBuilder WithMediaAssets(params MediaAsset[] mediaAssets)
    {
        _mediaAssets.AddRange(mediaAssets);
        return this;
    }

    public MockDbContextBuilder WithDeviceTokens(params DeviceToken[] tokens)
    {
        _deviceTokens.AddRange(tokens);
        return this;
    }

    public MockDbContextBuilder WithNotifications(params Notification[] notifications)
    {
        _notifications.AddRange(notifications);
        return this;
    }

    public MockDbContextBuilder WithPilotFeedbacks(params PilotFeedback[] feedbacks)
    {
        _pilotFeedbacks.AddRange(feedbacks);
        return this;
    }

    public IApplicationDbContext Build()
    {
        SetupDbSet(_mockDb, db => db.Users, _users);
        SetupDbSet(_mockDb, db => db.Profiles, _profiles);
        SetupDbSet(_mockDb, db => db.RefreshTokens, _refreshTokens);
        SetupDbSet(_mockDb, db => db.OtpCodes, _otpCodes);
        SetupDbSet(_mockDb, db => db.FirebaseLinkApprovals, _firebaseLinkApprovals);
        SetupDbSet(_mockDb, db => db.AccountDeletionJobs, _accountDeletionJobs);

        SetupDbSet(_mockDb, db => db.Farms, _farms);
        SetupDbSet(_mockDb, db => db.CropPeriods, _cropPeriods);
        SetupDbSet(_mockDb, db => db.WeatherSnapshots, _weatherSnapshots);

        SetupDbSet(_mockDb, db => db.Activities, _activities);
        SetupDbSet(_mockDb, db => db.ActivityRevisions, _activityRevisions);

        SetupDbSet(_mockDb, db => db.FarmTasks, _farmTasks);

        SetupDbSet(_mockDb, db => db.SupportCases, _supportCases);
        SetupDbSet(_mockDb, db => db.CaseMessages, _caseMessages);
        SetupDbSet(_mockDb, db => db.CaseMedia, _caseMedia);
        SetupDbSet(_mockDb, db => db.CaseMessageMedia, _caseMessageMedia);

        SetupDbSet(_mockDb, db => db.MediaAssets, _mediaAssets);

        SetupDbSet(_mockDb, db => db.DeviceTokens, _deviceTokens);
        SetupDbSet(_mockDb, db => db.Notifications, _notifications);

        SetupDbSet(_mockDb, db => db.PilotFeedbacks, _pilotFeedbacks);

        _mockDb.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var mockTransaction = new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();
        mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockTransaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockDb.Setup(db => db.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTransaction.Object);

        return _mockDb.Object;
    }

    private static void SetupDbSet<T>(
        Mock<IApplicationDbContext> mockContext,
        System.Linq.Expressions.Expression<Func<IApplicationDbContext, DbSet<T>>> expression,
        List<T> sourceList) where T : class
    {
        var mockDbSet = sourceList.BuildMockDbSet();
        mockDbSet.Setup(m => m.Add(It.IsAny<T>())).Callback<T>(sourceList.Add);
        mockDbSet.Setup(m => m.AddRange(It.IsAny<IEnumerable<T>>())).Callback<IEnumerable<T>>(sourceList.AddRange);
        mockDbSet.Setup(m => m.Remove(It.IsAny<T>())).Callback<T>(item => sourceList.Remove(item));
        mockContext.Setup(expression).Returns(mockDbSet.Object);
    }
}
