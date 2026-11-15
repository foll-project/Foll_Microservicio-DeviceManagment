namespace foll_device_managment.Shared.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "foll.integration";
    public int PublishTimeoutSeconds { get; set; } = 10;
    public string QueueName { get; set; } = "notificationcommunication.device-events.v1";
}
