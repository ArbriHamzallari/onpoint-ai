namespace OnPoint.Domain;

public enum BusinessType
{
    hotel,
    restaurant,
    retail,
    service,
    healthcare,
    other
}

public enum BusinessPlan
{
    trial,
    starter,
    growth,
    enterprise
}

public enum LocationType
{
    room,
    table,
    public_area,
    department,
    service_point,
    other
}

public enum UserRole
{
    platform_admin,
    owner,
    manager,
    staff
}

public enum FeedbackSentiment
{
    positive,
    neutral,
    negative,
    unknown
}

public enum FeedbackSeverity
{
    low,
    medium,
    high,
    urgent,
    unknown
}

public enum IssueStatus
{
    open,
    assigned,
    in_progress,
    resolved,
    cancelled
}

public enum IssuePriority
{
    low,
    medium,
    high,
    urgent
}
