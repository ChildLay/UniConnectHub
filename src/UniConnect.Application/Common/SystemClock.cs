namespace UniConnect.Application.Common;

public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
