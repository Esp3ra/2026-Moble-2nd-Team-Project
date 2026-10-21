using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RemoteWork.WinForms.Features.Collaboration;

/// <summary>One explicit login-owned receiver. Forms borrow it without disposing it.</summary>
public sealed class CollaborationNotificationScope : Component
{
    private static readonly ConditionalWeakTable<Form, CollaborationNotificationScope> owners = new();
    private readonly Form owner;
    private readonly CollaborationNotificationCoordinator coordinator;
    private bool stopped;
    public ICollaborationService Service { get; }
    public bool IsCurrent => !stopped && !owner.IsDisposed && Service.IsCurrent;
    public event Action<string>? StatusChanged;
    public CollaborationNotificationScope(Form owner, ICollaborationService service, TimeSpan interval)
    {
        if (owners.TryGetValue(owner, out _)) throw new InvalidOperationException("이미 이 창에 협업 수신이 연결돼 있습니다.");
        this.owner = owner; Service = service;
        coordinator = new(owner, service, interval);
        coordinator.StatusChanged += ForwardStatus;
        owners.Add(owner, this);
        owner.FormClosed += OwnerClosed; owner.Disposed += OwnerDisposed;
    }
    private void ForwardStatus(string message) => StatusChanged?.Invoke(message);
    public Task StartAsync() => IsCurrent ? coordinator.StartAsync() : Task.CompletedTask;
    public Task CheckAsync() => IsCurrent ? coordinator.CheckAsync() : Task.CompletedTask;
    internal bool Matches(ICollaborationService service) => IsCurrent && service.IsCurrent &&
        Service.UserId == service.UserId && Service.Cancellation == service.Cancellation;
    internal static CollaborationNotificationScope? Find(Form? owner)
    {
        while (owner is not null)
        {
            if (owners.TryGetValue(owner, out var scope)) return scope;
            owner = owner.Owner;
        }
        return null;
    }
    private void OwnerClosed(object? sender, FormClosedEventArgs e) => Dispose();
    private void OwnerDisposed(object? sender, EventArgs e) => Dispose();
    protected override void Dispose(bool disposing)
    {
        if (disposing && !stopped)
        {
            stopped = true; owners.Remove(owner);
            owner.FormClosed -= OwnerClosed; owner.Disposed -= OwnerDisposed;
            coordinator.StatusChanged -= ForwardStatus;
            coordinator.Dispose(); StatusChanged = null;
        }
        base.Dispose(disposing);
    }
}
