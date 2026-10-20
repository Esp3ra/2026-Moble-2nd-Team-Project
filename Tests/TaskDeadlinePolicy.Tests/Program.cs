using System.Reflection;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private static int checks;
    private static readonly DateOnly Today = DeadlineExtensionPolicy.KoreanDate(DateTimeOffset.UtcNow);

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            // Same UTC day straddles the Korean calendar boundary; PC timezone must not decide it.
            Check(DeadlineExtensionPolicy.KoreanDate(DateTimeOffset.Parse("2026-10-20T14:59:59Z")) == new DateOnly(2026, 10, 20), "before KST midnight");
            Check(DeadlineExtensionPolicy.KoreanDate(DateTimeOffset.Parse("2026-10-20T15:00:00Z")) == new DateOnly(2026, 10, 21), "at KST midnight");
            Check(DeadlineExtensionPolicy.KoreanDate(DateTimeOffset.Parse("2026-10-20T08:00:00-07:00")) == new DateOnly(2026, 10, 21), "offset independent");
            Check(DeadlineExtensionPolicy.DateError(Today.AddDays(-2), Today, Today) is null, "overdue work can request today");
            Check(DeadlineExtensionPolicy.DateError(Today.AddDays(2), Today.AddDays(3), Today) is null, "unexpired work can request later");
            Check(DeadlineExtensionPolicy.DateError(Today, Today, Today) is not null, "same due date is not extension");
            Check(DeadlineExtensionPolicy.DateError(Today.AddDays(1), Today, Today) is not null, "shortening is not extension");
            Check(DeadlineExtensionPolicy.DateError(Today.AddDays(-3), Today.AddDays(-1), Today) is not null, "later than old date but already past");
            Check(DeadlineExtensionPolicy.DateError(Today.AddDays(-2), Today, Today.AddDays(1)) is not null, "yesterday's request needs rechecking before approval");
            foreach (var status in Enum.GetValues<TaskDisplayStatus>()) FormState(status);
            ReturnedOverdue();
            Console.WriteLine($"TaskDeadlinePolicy: {checks} checks passed; local policy/real WinForms only, no extension HTTP/DB implementation.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static TaskDetail Detail(TaskDisplayStatus status, DateOnly due) =>
        new(new TaskSummary("19", "연장 준비 검증", "본인", status, due.AddDays(-2), due), "검증");
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new InvalidOperationException(message); }
    private static void Show(Form form)
    { form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new(-20000, -20000); form.Show(); Application.DoEvents(); }
    private static void SelectDate(DeadlineExtensionForm form, DateOnly value)
    { var picker = Field<DateTimePicker>(form, "dtpRequestedDeadline"); picker.Value = value.ToDateTime(TimeOnly.MinValue); picker.Checked = true; }

    private static void FormState(TaskDisplayStatus status)
    {
        using var form = new DeadlineExtensionForm(PreviewRole.Employee, Detail(status, Today));
        Show(form);
        bool allowed = status is TaskDisplayStatus.InProgress or TaskDisplayStatus.Returned;
        Check(Field<Button>(form, "btnConfirm").Enabled == allowed, status + " enabled only in approved states");
        SelectDate(form, Today.AddDays(1));
        Field<TextBox>(form, "txtExtensionReason").Text = "일정 조정";
        Field<Button>(form, "btnConfirm").PerformClick();
        Check((form.CheckedInput is not null) == allowed, status + " only publishes an eligible local snapshot");
        Check(form.DialogResult == DialogResult.None, "local check is not a successful submission");
    }

    private static void ReturnedOverdue()
    {
        var original = Detail(TaskDisplayStatus.Returned, Today.AddDays(-2));
        using var form = new DeadlineExtensionForm(PreviewRole.Administrator, original);
        Show(form);
        Field<Button>(form, "btnConfirm").PerformClick();
        Check(form.CheckedInput is null, "no date/reason cannot confirm");
        Field<TextBox>(form, "txtExtensionReason").Text = "반려 결과 재작업을 위한 연장";
        SelectDate(form, Today.AddDays(-1));
        Field<Button>(form, "btnConfirm").PerformClick();
        Check(form.CheckedInput is null, "past requested date fails even after overdue current date");
        SelectDate(form, Today);
        Field<Button>(form, "btnConfirm").PerformClick();
        Check(form.CheckedInput?.RequestedDueDate == Today, "returned/overdue accepts local input without changing state");
        Check(original.Summary.Status == TaskDisplayStatus.Returned && original.Summary.DueDate == Today.AddDays(-2), "source status and due date stay unchanged");
        var picker = Field<DateTimePicker>(form, "dtpRequestedDeadline"); picker.Checked = false;
        Check(form.CheckedInput is null, "programmatic unchecked date invalidates snapshot");
        SelectDate(form, Today.AddDays(1));
        Field<Button>(form, "btnConfirm").PerformClick();
        Check(form.CheckedInput is not null, "can recheck a later local date");
        Field<TextBox>(form, "txtExtensionReason").Clear();
        Check(form.CheckedInput is null, "edited reason invalidates snapshot");
        form.Close();
        Check(form.CheckedInput is null, "closed form never exposes preparation");
    }
}
