from flask import g
import pytest
from sqlalchemy import select

from remotework.auth import authenticated_transaction, create_user, login_required
from remotework.common import success
from remotework.models import User
from remotework.db import transaction
from remotework.user_candidates import CandidateScope, list_user_candidates


def install(app, allowed):
    @app.get("/api/v1/_candidate_probe/<kind>")
    @login_required
    def probe(kind):
        from flask import request
        with authenticated_transaction() as db:
            # Explicit synthetic test policy, not an adopted task authorization rule.
            result = list_user_candidates(db, authorize=lambda _: allowed(g.user), kind=kind,
                q=request.args.get("q", ""), page=int(request.args.get("page", 1)),
                limit=int(request.args.get("limit", 20)))
        return success(result)


def seed(app):
    with app.app_context(), transaction() as db:
        one = db.scalar(select(User).where(User.login_id == "staff1"))
        one.display_name = "동명이인"
        second = create_user(db, {"login_id": "staff2", "password": "Candidate-test-2026!",
            "display_name": "동명이인", "employee_no": "E002", "department": "개발팀", "role": "employee"})
        disabled = create_user(db, {"login_id": "inactive", "password": "Candidate-test-2026!",
            "display_name": "비활성", "employee_no": "E003", "department": "개발팀", "role": "employee"})
        disabled.is_active = False
        return one.id, second.id, disabled.id


def test_candidates_minimal_active_scoped_paged_and_literal_search(app, login):
    first, second, disabled = seed(app)
    install(app, lambda actor: CandidateScope(frozenset({first, second, disabled})))
    headers, _ = login()
    with app.test_client() as client:
        def get(query="", kind="assignment"):
            r = client.get('/api/v1/_candidate_probe/'+kind+query, headers=headers)
            assert r.status_code == 200
            return r.json['data']
        data = get('?limit=1')
        assert data['total'] == 2 and len(data['items']) == 1
        other = get('?limit=1&page=2')['items'][0]
        assert data['items'][0]['id'] == first and other['id'] == second
        assert data['items'][0]['selection_label'] != other['selection_label']
        assert set(other) == {'id', 'display_name', 'selection_label'}
        assert get('?q=%25')['items'] == []  # '%' is not a SQL wildcard.
        assert get('?q=开发')['items'] == []
        assert get(kind='manager')['items'] == []
        assert get('?page=3&limit=1')['items'] == []


def test_candidates_deny_and_do_not_open_product_routes(app, login):
    install(app, lambda actor: None)
    headers, _ = login()
    with app.test_client() as client:
        r=client.get('/api/v1/_candidate_probe/assignment', headers=headers)
        assert r.status_code == 403
        # Member 2's public action routes remain unregistered pending policy.
        assert client.get('/api/v1/tasks/assignment-candidates', headers=headers).status_code == 404
