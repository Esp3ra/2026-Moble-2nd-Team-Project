# Manual review request client and UI regression

Run on Windows from the repository root:

```powershell
dotnet run --project Tests/TaskReviewRequests.Tests -- C:/temp/remote-work-review-captures
```

The optional argument saves synthetic-data screenshots. The executable uses the
actual shared `ApiClient`, review client, and WinForms controls with explicit HTTP
response doubles. It starts no server and performs no database or product writes.

Checks cover exact BIGINT and original-text transport; one-key replay after an
unknown result; receipt-first retry; expiry and reset guards; malformed keys and
cross-task/request/round responses; current-session isolation; concurrent sends
and late completions; closing and reopening an unresolved dialog; current and
reopened screen clearing after 403; content-free receipt recovery after access is
restored; explicit inbox acknowledgement; and long status text at the minimum
window size and a 150% control scale.

Local validation on 2026-10-26: 127 checks passed, build warnings/errors 0/0.
This does not establish shared-server, real-account, or two-PC acceptance.
The separate server `test_task_web_review_requests.py` exercises real Flask
cookie login, CSRF, links, POST/redirect/GET, and common receipts on the explicitly
owned disposable MySQL fixture.
