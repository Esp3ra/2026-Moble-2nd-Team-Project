"""Normal create_app registration, isolated owned MySQL only; no deployment ACK."""
from unittest.mock import Mock

import pytest
from sqlalchemy import inspect
from remotework import create_app
from remotework.features.collaboration import install
from remotework.features.collaboration.routes import ACTIONS
from remotework.features.collaboration.task_bridge import current_task_context
from test_collaboration_requests import app, headers, send  # noqa: F401


def test_ordinary_entrypoint_registers_routes_and_same_install_is_safe(app):
    # Fixture's legacy install call is intentionally retained. Normal create_app
    # now owns the first install; an identical explicit factory does no duplicate work.
    assert "collaboration_requests" in app.blueprints
    assert set(app.extensions["write_action_contracts"]) == ACTIONS
    assert set(app.extensions["write_result_readers"]) == ACTIONS
    assert install(app, task_context=current_task_context) is True
    with pytest.raises(RuntimeError): install(app, task_context=lambda *_: None)
    app.test_client().get("/api/v1/health")
    with pytest.raises(RuntimeError): install(app, task_context=current_task_context)


def test_normal_on_requires_common_writes_before_database(monkeypatch):
    from remotework import db
    initialization = Mock(side_effect=AssertionError("Database setup must not run"))
    monkeypatch.setattr(db, "init_app", initialization)
    for flag in ("ENABLE_COLLABORATION_DRAFT", "ENABLE_COLLABORATION_RESULT_READS_DRAFT"):
        with pytest.raises(RuntimeError, match="explicit common write"):
            create_app({flag: True, "ENABLE_COMMON_WRITE_DRAFT": False})
    initialization.assert_not_called()


def test_default_off_does_not_register_feature_or_create_storage(app):
    before = set(inspect(app.extensions["db_engine"]).get_table_names())
    cold = create_app(dict(app.config) | {"ENABLE_COLLABORATION_DRAFT": False,
                                        "ENABLE_COLLABORATION_RESULT_READS_DRAFT": False})
    try:
        assert "collaboration_requests" not in cold.blueprints
        assert not ACTIONS.intersection(cold.extensions.get("write_action_contracts", {}))
        assert not ACTIONS.intersection(cold.extensions.get("write_result_readers", {}))
        assert set(inspect(cold.extensions["db_engine"]).get_table_names()) == before
    finally: cold.extensions["db_engine"].dispose()


def test_normal_off_keeps_opt_in_receipt_reads_and_denies_new_keys(app):
    client = app.test_client(); identity = headers(client)
    response, key, _ = send(client, identity)
    assert response.status_code == 201
    cold = create_app(dict(app.config) | {"ENABLE_COLLABORATION_DRAFT": False,
                                        "ENABLE_COLLABORATION_RESULT_READS_DRAFT": True})
    try:
        other = cold.test_client()
        assert other.get("/api/v1/collaboration-requests", headers=identity).status_code == 404
        result = other.post("/api/v1/write-requests/result", headers=identity | {"Idempotency-Key": key},
                            json={"action": "collaboration.created", "target": "1"})
        assert result.status_code == 200 and result.json["data"]["state"] == "completed"
        denied = other.post("/api/v1/write-requests/keys", headers=identity,
                            json={"action": "collaboration.created", "target": "1"})
        assert denied.status_code == 503 and denied.json["error"]["code"] == "WRITE_ACTION_READ_ONLY"
    finally: cold.extensions["db_engine"].dispose()


def test_task_opt_in_supplies_real_owner_reader_without_ack_or_ddl(app):
    from remotework.features.tasks.continuity import continuous_in_progress
    before = set(inspect(app.extensions["db_engine"]).get_table_names())
    combined = create_app(dict(app.config) | {"ENABLE_TASK_QUERY_DRAFT": True})
    try:
        assert combined.extensions["collaboration_continuity_reader"] is continuous_in_progress
        assert "task_lifecycle_integration_ack" not in combined.extensions
        assert set(inspect(combined.extensions["db_engine"]).get_table_names()) == before
    finally: combined.extensions["db_engine"].dispose()


def test_registered_endpoint_without_012_returns_preparation_error_not_404(app):
    from remotework.features.collaboration.models import FeatureBase
    client = app.test_client(); identity = headers(client)
    engine = app.extensions["db_engine"]
    FeatureBase.metadata.drop_all(engine)
    before = set(inspect(engine).get_table_names())
    result = client.get("/api/v1/collaboration-requests", headers=identity)
    assert result.status_code == 503 and result.json["error"]["code"] == "COLLABORATION_SCHEMA_REQUIRED"
    assert set(inspect(engine).get_table_names()) == before
