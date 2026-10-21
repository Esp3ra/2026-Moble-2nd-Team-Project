"""Explicit candidate integration entry point; does not change common create_app.

Member 1 must choose and review the actual deployment setup. Merely importing
this factory neither creates storage nor enables routes in the existing server.
"""
from ... import create_app
from . import install
from .task_bridge import current_task_context


def create_collaboration_app(config=None, *, continuity_reader=None):
    config = dict(config or {})
    if type(config.get("ENABLE_COLLABORATION_DRAFT")) is not bool:
        raise RuntimeError("Explicit collaboration draft configuration required")
    if config.get("ENABLE_COMMON_WRITE_DRAFT") is not True:
        raise RuntimeError("Explicit common write support required")
    app = create_app(config)
    if continuity_reader is not None:
        if not callable(continuity_reader):
            raise TypeError("Owner reviewed continuity reader required")
        app.extensions["collaboration_continuity_reader"] = continuity_reader
    install(app, task_context=current_task_context)
    return app
