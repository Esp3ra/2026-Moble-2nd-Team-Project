"""Opt-in feature registration; draft models stay out of normal init-db."""
import os


def get_blueprints(config):
    blueprints = []
    if config.get("ENABLE_CHAT_DRAFT", os.getenv("REMOTEWORK_ENABLE_CHAT_DRAFT") == "1"):
        from .chat.routes import bp as chat_directory_bp
        blueprints.append(chat_directory_bp)
    if config.get("ENABLE_TASK_QUERY_DRAFT", False):
        # Preserve the existing task draft's independent activation boundary.
        from .tasks.routes import bp as task_queries_bp
        blueprints.append(task_queries_bp)
    if config.get("ENABLE_NOTICE_DRAFT", os.getenv("REMOTEWORK_ENABLE_NOTICE_DRAFT") == "1"):
        # Notice API/DB rollout remains a separately reviewable draft.
        from .notices.routes import bp as notices_bp
        blueprints.append(notices_bp)
        from .notices.web import bp as notice_web_bp
        blueprints.append(notice_web_bp)
        if config.get("ENABLE_NOTICE_POPUP_DRAFT", os.getenv("REMOTEWORK_ENABLE_NOTICE_POPUP_DRAFT") == "1"):
            from .notices.popup_routes import bp as notice_popup_bp
            blueprints.append(notice_popup_bp)
    if config.get("ENABLE_RESULTS_DRAFT", os.getenv("REMOTEWORK_ENABLE_RESULTS_DRAFT") == "1"):
        from .results import bp as results_bp
        blueprints.append(results_bp)
        from .results_web import bp as results_web_bp
        blueprints.append(results_web_bp)
    return blueprints
