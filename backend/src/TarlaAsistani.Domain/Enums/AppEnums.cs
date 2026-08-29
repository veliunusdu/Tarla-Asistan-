namespace TarlaAsistani.Domain.Enums;

public enum UserRole
{
    Farmer,
    Agronomist,
}

public enum IrrigationMethod
{
    Drip,
    Sprinkler,
    Flood,
    Rainfed,
    Other,
}

public enum CropType
{
    Wheat,
    Barley,
    Corn,
    Sunflower,
    Tomato,
}

public enum CropPeriodStatus
{
    Active,
    Archived,
}

public enum TaskPriority
{
    Low,
    Medium,
    High,
    Critical,
}

public enum TaskStatus
{
    New,
    Viewed,
    Planned,
    Completed,
    NotApplied,
    Overdue,
    Cancelled,
}

public enum TaskSource
{
    System,
    CropCalendar,
    Weather,
    Expert,
}

public enum TaskConfidence
{
    Low,
    Medium,
    High,
}

public enum ActivityType
{
    Irrigation,
    Fertilization,
    Spraying,
    Pruning,
    FieldCheck,
    Harvest,
    Other,
}

public enum ActivityStatus
{
    Draft,
    Confirmed,
}

public enum ActivitySource
{
    Manual,
    Voice,
    Task,
}

public enum MediaKind
{
    Image,
    Audio,
}

public enum CaseCategory
{
    Disease,
    Pest,
    Irrigation,
    Nutrition,
    Weather,
    Other,
}

public enum CasePriority
{
    Low,
    Medium,
    High,
    Critical,
}

public enum CaseStatus
{
    Open,
    InReview,
    WaitingFarmer,
    Answered,
    Closed,
}

public enum CaseMessageType
{
    Comment,
    AdditionalInfoRequest,
    ExpertResponse,
}

public enum DevicePlatform
{
    Android,
    Ios,
    Web,
}

public enum NotificationType
{
    TaskAssigned,
    CriticalWeather,
    ExpertResponse,
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed,
}

public enum FeedbackType
{
    WeeklyCheckin,
    FalseAlert,
    Bug,
    Suggestion,
}

public enum FeedbackStatus
{
    Open,
    Reviewed,
    Resolved,
}

public enum AccountStatus
{
    Active,
    DeletionPending,
    Anonymized,
}

public enum AccountDeletionStatus
{
    Pending,
    Processing,
    RetryRequired,
    Completed,
}
