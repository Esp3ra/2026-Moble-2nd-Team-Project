"""Opt-in feature registration; draft models stay out of normal init-db."""


def get_blueprints(config):
    if not config.get("ENABLE_TASK_QUERY_DRAFT", False):
        return []
    # Importing the draft is an explicit development/test choice. A fresh common
    # server process without the flag does not add its tables to Base.metadata.
    from .tasks.routes import bp as task_queries_bp
    return [task_queries_bp]
