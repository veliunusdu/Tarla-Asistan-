namespace TarlaAsistani.Domain.Enums;

public enum UserRole { Farmer, Agronomist }
public enum IrrigationMethod { Drip, Sprinkler, Flood, Rainfed, Other }
public enum CropType { Wheat, Barley, Corn, Sunflower, Tomato }
public enum CropPeriodStatus { Active, Archived }
public enum ActivityType { Irrigation, Fertilization, Spraying, Pruning, FieldCheck, Harvest, Other }
public enum TaskPriority { Low, Medium, High, Critical }
public enum TaskStatus { New, Viewed, Planned, Completed, NotApplied, Overdue, Cancelled }