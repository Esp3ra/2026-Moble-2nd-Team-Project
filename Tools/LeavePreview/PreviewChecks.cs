using System.Reflection;
using RemoteWork.WinForms.Features.Attendance;

namespace RemoteWork.LeavePreview;

internal static class PreviewChecks
{
    private static int count;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void Check(bool ok, string name) { count++; if (!ok) throw new InvalidOperationException(name); }
    private static void Wait(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(1); }
        if (!task.IsCompleted) throw new TimeoutException();
        task.GetAwaiter().GetResult();
    }
    public static int Run(string? output)
    {
        try
        {
            Control.CheckForIllegalCrossThreadCalls = true;
            using var employee = PreviewStore.CreateIdentity(false); using var reviewer = PreviewStore.CreateIdentity(true);
            Wait(employee.LoginAsync("preview-employee", "preview-only")); Wait(reviewer.LoginAsync("preview-reviewer", "preview-only"));
            var store = new PreviewStore();
            var own = store.For(employee.CaptureSession()); var managed = store.For(reviewer.CaptureSession());
            Check(own.ListAsync(default).Result.All(r => r.ApplicantId == PreviewStore.Employee.Id && !r.CanReview), "Employee sees only own rows");
            Check(managed.ListAsync(default).Result.All(r => r.ApplicantId == PreviewStore.Employee.Id && r.CanReview), "Reviewer sees only assigned rows");
            using var application = new LeaveApplicationForm(employee.CaptureSession(), own); application.Show();
            Field<ComboBox>(application, "cmbOptions").SelectedItem = "공가";
            Field<TextBox>(application, "txtLeaveReason").Text = "화면 체험 자동 검사";
            Field<Button>(application, "btnApplyLeave").PerformClick(); Wait(application.PendingOperation);
            Check(application.SubmissionCompleted, "Actual application form submits to RAM fixture");
            var row = managed.ListAsync(default).Result.Single(r => r.Draft.Reason == "화면 체험 자동 검사");
            using var review = new LeaveDecisionForm(reviewer.CaptureSession(), managed, row); review.Show();
            Check(Field<Label>(review, "lblCharge").Text.Contains("차감하지"), "Official leave charge notice shown to reviewer");
            Field<Button>(review, "btnApprove").PerformClick(); Wait(review.PendingOperation);
            Check(review.DecisionCompleted && own.ListAsync(default).Result.Single(r => r.Id == row.Id).Status == "approved", "Actual review form updates the same preview request");
            var draft = new LeaveDraft("병가", row.Draft.StartDate, row.Draft.EndDate, "same-id-fixture");
            var id = Guid.NewGuid();
            var first = own.SubmitAsync(draft, id, default).Result;
            Check(own.SubmitAsync(draft, id, default).Result == first, "RAM receipt repeat does not duplicate");
            var rejectRow = managed.ListAsync(default).Result.Single(r => r.Id == first.Request.Id);
            using var reject = new LeaveDecisionForm(reviewer.CaptureSession(), managed, rejectRow); reject.Show();
            string rejection = "해당 날짜에 부서 일정이 있습니다.\r\n다른 날짜로 다시 신청해 주세요.";
            Field<TextBox>(reject, "txtDecisionReason").Text = rejection;
            Field<Button>(reject, "btnReject").PerformClick(); Wait(reject.PendingOperation);
            var rejected = own.ListAsync(default).Result.Single(r => r.Id == rejectRow.Id);
            Check(reject.DecisionCompleted && rejected.Status == "rejected" && rejected.DecisionReason == rejection,
                "Reviewer reason is retained in the same employee request");
            using var detail = new LeaveDecisionForm(employee.CaptureSession(), own, rejected, readOnly: true); detail.Show();
            Check(Field<TextBox>(detail, "txtDecisionReason").Text == rejection && Field<TextBox>(detail, "txtDecisionReason").ReadOnly,
                "Employee details display full rejection reason read-only");
            if (output is not null) { Directory.CreateDirectory(output); Save(detail, Path.Combine(output, "leave-rejection-detail.png")); }
            detail.Close(); reject.Close();
            bool refused = false;
            try { own.DecideAsync(row, "approved", "", Guid.NewGuid(), default).GetAwaiter().GetResult(); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused, "Employee cannot review using the preview adapter");
            employee.ClearIdentity(); refused = false;
            try { own.ListAsync(default).GetAwaiter().GetResult(); } catch (OperationCanceledException) { refused = true; }
            Check(refused, "Ended preview identity cannot use old adapter");
            application.Close(); review.Close();
            using var workspace = new LeavePreviewForm(); workspace.Show(); Wait(workspace.Ready); Application.DoEvents();
            Check(workspace.Ready.IsCompletedSuccessfully, "Standalone Designer workspace opens without a server");
            if (output is not null)
            {
                Directory.CreateDirectory(output);
                Save(workspace, Path.Combine(output, "leave-employee-workspace.png"));
                Field<TabControl>(workspace, "tabs").SelectedIndex = 1; Application.DoEvents();
                Save(workspace, Path.Combine(output, "leave-reviewer-workspace.png"));
            }
            workspace.Close();
            Console.WriteLine($"PASS Leave preview: {count} checks; actual forms and memory fixtures; no network/database/device.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void Save(Form form, string file)
    {
        Application.DoEvents();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(file);
    }
}
