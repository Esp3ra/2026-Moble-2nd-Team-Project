using RemoteWork.WinForms.Features.Tasks.Creation;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Queries;

// One page load's borrowed services. The view owns cancellation/UI lifetime;
// the shared ApiClient and session stores retain their own ownership.
internal sealed record TaskQuerySession(
    ApiClient.Session Session,
    HttpTaskQueryService QueryService,
    ITaskPreparationService PreparationService,
    TaskCreationWorkflow? CreationWorkflow);
