"""Common extension seam only: synthetic mutations, not product submission APIs."""
from concurrent.futures import ThreadPoolExecutor
from threading import Barrier

from flask import g, request
import pytest
from sqlalchemy import func, select

from remotework import create_app
from remotework.auth import authenticated_transaction, login_required
from remotework.common import AppError, audit, payload, success
from remotework.db import get_db
from remotework.models import AuditEvent, User
from remotework.write_requests import (WriteReceipt, WriteResult, perform_once,
                                      register_write_action)
from test_write_requests import writes  # Reuse the owned-DB setup, never a shared DB.

ACTION = "submission.test_completed"


@pytest.fixture
def feature(writes):
    writes.config.update(FEATURE_ALLOWED=True, FEATURE_FAIL=False, FEATURE_EXTRA=False, FEATURE_BARRIER=None)

    def allowed(db, target):
        return writes.config["FEATURE_ALLOWED"] and g.user.role == "employee" and target == "self"

    register_write_action(writes, action=ACTION, authorize=allowed,
                          result_fields={"task_id", "submission_id", "row_version"})

    @writes.post("/api/v1/_feature_probe")
    @login_required
    def change():
        body = payload()
        if writes.config["FEATURE_BARRIER"]:
            writes.config["FEATURE_BARRIER"].wait(timeout=15)
        with authenticated_transaction() as db:
            def mutate(session):
                g.user.department = body["department"]
                audit("test.feature_once", "user", g.user.id)
                if writes.config["FEATURE_FAIL"]:
                    raise AppError(409, "TEST_ROLLBACK", "synthetic rollback")
                result = {"task_id": "1", "submission_id": "3", "row_version": "2"}
                if writes.config["FEATURE_EXTRA"]:
                    result["description"] = "must not be retained"
                return WriteResult(result, 201)
            callback = allowed if not body.get("bypass") else lambda db, target: True
            result = perform_once(db, key=request.headers.get("Idempotency-Key"), action=ACTION,
                                  target="self", body=body, authorize=callback, mutate=mutate)
        return success(result.data, result.status)

    return writes


def key(client, headers):
    result = client.post("/api/v1/write-requests/keys", headers=headers,
                         json={"action": ACTION, "target": "self"})
    assert result.status_code == 201, result.json
    return headers | {"Idempotency-Key": result.json["data"]["request_key"]}


def counts(app):
    with app.app_context():
        return (get_db().scalar(select(func.count()).select_from(WriteReceipt)),
                get_db().scalar(select(func.count()).select_from(AuditEvent)
                                .where(AuditEvent.action == "test.feature_once")))


def test_registered_feature_replay_result_and_action_binding(feature, login):
    headers, _ = login()
    with feature.test_client() as client:
        keyed = key(client, headers)
        for _ in range(2):
            response = client.post("/api/v1/_feature_probe", headers=keyed, json={"department": "one"})
            assert response.status_code == 201
            assert response.json["data"] == {"task_id": "1", "submission_id": "3", "row_version": "2"}
        assert counts(feature) == (1, 1)
        conflict = client.post("/api/v1/_feature_probe", headers=keyed, json={"department": "two"})
        assert conflict.status_code == 409 and conflict.json["error"]["code"] == "IDEMPOTENCY_KEY_REUSED"
        result = client.post("/api/v1/write-requests/result", headers=keyed,
                             json={"action": ACTION, "target": "self"})
        assert result.json["data"]["state"] == "completed"
        assert result.json["data"]["http_status"] == 201
        wrong = client.post("/api/v1/write-requests/result", headers=keyed,
                            json={"action": "task.accepted", "target": "self"})
        assert wrong.status_code == 403


@pytest.mark.parametrize("failure", ["FEATURE_FAIL", "FEATURE_EXTRA"])
def test_feature_failure_or_undeclared_result_rolls_back_all(feature, login, failure):
    headers, identity = login()
    with feature.test_client() as client:
        keyed = key(client, headers)
        feature.config[failure] = True
        result = client.post("/api/v1/_feature_probe", headers=keyed, json={"department": "rollback"})
        assert result.status_code == (409 if failure == "FEATURE_FAIL" else 500)
        assert counts(feature) == (0, 0)
        with feature.app_context():
            assert get_db().get(User, identity["user"]["id"]).department == "개발팀"
        feature.config[failure] = False
        assert client.post("/api/v1/_feature_probe", headers=keyed, json={"department": "rollback"}).status_code == 201
        assert counts(feature) == (1, 1)


def test_feature_replay_and_result_recheck_current_permission(feature, login):
    headers, _ = login()
    with feature.test_client() as client:
        keyed = key(client, headers)
        assert client.post("/api/v1/_feature_probe", headers=keyed, json={"department": "one"}).status_code == 201
        feature.config["FEATURE_ALLOWED"] = False
        assert client.post("/api/v1/_feature_probe", headers=keyed, json={"department": "one"}).status_code == 403
        assert client.post("/api/v1/write-requests/result", headers=keyed,
                           json={"action": ACTION, "target": "self"}).status_code == 403
        assert counts(feature) == (1, 1)


def test_feature_service_cannot_substitute_looser_authorizer(feature, login):
    headers, _ = login()
    with feature.test_client() as client:
        keyed = key(client, headers)
        feature.config["FEATURE_ALLOWED"] = False
        assert client.post("/api/v1/_feature_probe", headers=keyed,
                           json={"department": "bypass", "bypass": True}).status_code == 500
        assert counts(feature) == (0, 0)


def test_feature_contract_registration_is_explicit_local_and_startup_only(feature, login):
    allowed = lambda db, target: True
    for name, fields, callback in [("task.accepted", {"task_id"}, allowed),
                                   (ACTION, {"task_id"}, allowed),
                                   ("bad/action", {"task_id"}, allowed),
                                   ("custom.changed", set(), allowed),
                                   ("custom.changed", "task_id", allowed),
                                   ("custom.changed", {"Bad Field"}, allowed),
                                   ("custom.changed", {"task_id"}, None)]:
        with pytest.raises(ValueError):
            register_write_action(feature, action=name, authorize=callback, result_fields=fields)
    other = create_app({"TESTING": True, "SECRET_KEY": "test-" + "x" * 40,
                        "DATABASE_URL": "mysql+pymysql://unused@127.0.0.1/unused",
                        "ENABLE_COMMON_WRITE_DRAFT": False, "ENABLE_TASK_QUERY_DRAFT": False})
    assert ACTION not in other.extensions.get("write_action_contracts", {})
    assert not any("write-requests" in str(rule) for rule in other.url_map.iter_rules())
    headers, _ = login()
    with feature.test_client() as client:
        unknown = client.post("/api/v1/write-requests/keys", headers=headers,
                              json={"action": "submission.unregistered", "target": "self"})
        assert unknown.status_code == 400
    with pytest.raises(RuntimeError):
        register_write_action(feature, action="custom.changed", authorize=allowed, result_fields={"task_id"})


def test_mysql_registered_feature_concurrent_single_effect_and_lost_response(feature, login):
    if feature.extensions["db_engine"].dialect.name != "mysql":
        pytest.skip("Actual MySQL current locking is required")
    headers, _ = login()
    with feature.test_client() as client:
        keyed = key(client, headers)
    feature.config["FEATURE_BARRIER"] = Barrier(2)

    def send():
        with feature.test_client() as client:
            result = client.post("/api/v1/_feature_probe", headers=keyed, json={"department": "once"})
            return result.status_code, result.json["data"]

    with ThreadPoolExecutor(max_workers=2) as pool:
        futures = [pool.submit(send) for _ in range(2)]
        results = [future.result(timeout=25) for future in futures]
    assert results[0] == results[1] == (201, {"task_id": "1", "submission_id": "3", "row_version": "2"})
    assert counts(feature) == (1, 1)
    with feature.test_client() as client:
        result = client.post("/api/v1/write-requests/result", headers=keyed,
                             json={"action": ACTION, "target": "self"})
        assert result.json["data"]["state"] == "completed"
