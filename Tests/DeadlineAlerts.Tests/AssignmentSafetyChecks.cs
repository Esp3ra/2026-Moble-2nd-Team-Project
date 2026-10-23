using RemoteWork.WinForms.Features.DeadlineAlerts;

internal static partial class Program
{
    private static async Task AssignmentSafetyChecks()
    {
        foreach (bool saved in new[] { false, true })
        {
            using var source = new Source(); using var main = await ShowMain();
            using var scope = new DeadlineAlertScope(main, source);
            await scope.CheckAsync();
            if (saved)
            {
                source.Confirm = _ => Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed,
                    Snap(source.Now, Entry() with { IsConfirmed = true })));
                await scope.ConfirmAsync("event-A");
            }
            ClosePopups();
            source.Items = [Entry() with { AssignmentRevision = 3 }];
            await scope.CheckAsync();
            Check(scope.Snapshot is null && scope.Unread.Count == 0 && Popups() == 0,
                "same raw event/new assignment is blocked before generation-aware owner contract saved=" + saved);
            Check(scope.Status.Contains("배정") && source.Confirms == (saved ? 1 : 0),
                "assignment alias never applies an old confirmation or sends another POST saved=" + saved);
        }
    }
}
