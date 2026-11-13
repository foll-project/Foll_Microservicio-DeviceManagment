namespace foll_device_managment.DeviceManagment.Infrastructure.Configuration;
public class TelemetryOptions
{
    public int Capacity { get; set; } = 1000;
    public int MaxAgeSeconds { get; set; } = 30;
    public int PollIntervalMilliseconds { get; set; } = 500;
}
