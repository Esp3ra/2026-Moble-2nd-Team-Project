"""Member 4 explicit integration. Common create_app is owned by member 1."""


def install_result_readers(app, *, task_context, read_only=True):
    """Explicit startup installation for retained W1 results, including cold OFF.

    Does not register business routes, issue keys, mutate rows or create DDL.
    The supplied bridge and authorization always check CURRENT relationships.
    """
    if not app.config.get("ENABLE_COMMON_WRITE_DRAFT") or not callable(task_context):
        raise RuntimeError("Explicit current task bridge and common write support required")
    if app.extensions["db_engine"].dialect.name != "mysql":
        raise RuntimeError("Collaboration persistence requires MySQL; no alternative database fallback")
    if app._got_first_request:
        raise RuntimeError("Install during application setup")
    if "collaboration_result_mode" in app.extensions:
        if app.extensions["collaboration_result_mode"] != read_only or app.extensions["collaboration_task_context"] is not task_context:
            raise RuntimeError("Choose one collaboration result mode/bridge during setup")
        return
    from .routes import authorize_create, authorize_request, ACTIONS
    from ...write_requests import register_write_result_reader
    app.extensions["collaboration_task_context"] = task_context
    for action in ACTIONS:
        register_write_result_reader(app, action=action, read_only=read_only,
            authorize=authorize_create if action == "collaboration.created" else authorize_request)
    app.extensions["collaboration_result_mode"] = read_only


def install(app, *, task_context):
    """Install explicitly after common W1 setup; keep results when writes are OFF.

    No invocation from normal create_app/registry or automatic schema changes.
    task_context(db, task_id, since_version=None) must lock current source data.
    """
    enabled = app.config.get("ENABLE_COLLABORATION_DRAFT", False) is True
    if not enabled and not app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        return False
    install_result_readers(app, task_context=task_context, read_only=not enabled)
    if not enabled:
        return False
    from .routes import bp, authorize_create, authorize_request, ACTIONS, RESULT_FIELDS
    from ...write_requests import register_write_action
    for action in ACTIONS:
        register_write_action(app, action=action,
            authorize=authorize_create if action == "collaboration.created" else authorize_request,
            result_fields=RESULT_FIELDS)
    app.register_blueprint(bp)
    return True
