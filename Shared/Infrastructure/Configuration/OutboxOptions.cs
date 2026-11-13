namespace foll_device_managment.Shared.Infrastructure.Configuration;

public class OutboxOptions
{
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
}
