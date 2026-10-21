namespace RemoteWork.WinForms.Features.Collaboration;

internal static class CollaborationUiLifetime
{
    public static CancellationTokenRegistration CloseOnIdentityEnd(Form form, ICollaborationService service)
    {
        form.Shown += (_, _) => { if (!service.IsCurrent) form.Close(); };
        return service.Cancellation.Register(() =>
        {
            if (form.IsDisposed || !form.IsHandleCreated) return;
            try { form.BeginInvoke((Action)(() => { if (!form.IsDisposed) form.Close(); })); }
            catch (InvalidOperationException) { }
        });
    }
}
