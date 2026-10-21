"""Member 4 collaboration development proposal; never auto-installed."""


def install(app, *, task_context):
    """Explicit reviewed bridge required; no shared registry/DDL changes here.

    task_context(db, task_id, since_version=None) must lock and return CURRENT task/assignment data,
    including cancellation and continuity since prior versions, in the caller's TX.
    The candidate SQL bridge is a proposal, not an agreed member 2 interface.
    """
    if not app.config.get("ENABLE_COLLABORATION_DRAFT", False):
        return False
    if not app.config.get("ENABLE_COMMON_WRITE_DRAFT") or not callable(task_context):
        raise RuntimeError("Explicit current task bridge and common write support required")
    if app.extensions["db_engine"].dialect.name != "mysql":
        raise RuntimeError("Collaboration persistence requires MySQL; no alternative database fallback")
    if app._got_first_request:
        raise RuntimeError("Install during application setup")
    from .routes import bp, authorize_create, authorize_request, ACTIONS, RESULT_FIELDS
    from ...write_requests import register_write_action
    app.extensions["collaboration_task_context"] = task_context
    for action in ACTIONS:
        register_write_action(app, action=action,
            authorize=authorize_create if action == "collaboration.created" else authorize_request,
            result_fields=RESULT_FIELDS)
    app.register_blueprint(bp)
    return True
