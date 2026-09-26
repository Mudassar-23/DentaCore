namespace DentaCore.Application.Interfaces;

public interface INotificationService
{
    Task NotifyUserAsync(Guid userId, string title, string message, CancellationToken cancellationToken = default);
}
