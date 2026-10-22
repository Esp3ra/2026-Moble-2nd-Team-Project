"""Owner2 presentation-only attachment state; Flask request context, no DB.

The real shared-file owner/link checks remain in the public discard endpoint.
These tests check only the optional bounded form hint and its separation from
the actual final-list write body.
"""
import copy
import json

from flask import Flask
import pytest

from remotework.common import AppError
from remotework.features.tasks import web_attachments


FIRST = "00000000-0000-0000-0000-000000000001"
SECOND = "00000000-0000-0000-0000-000000000002"
SCOPE = "a" * 64


def item(attachment_id=FIRST, **changes):
    value = dict(attachment_id=attachment_id, name="업무 + 원본.txt", size=23,
                 sha256="b" * 64, temporary=True)
    value.update(changes)
    return value


def wrapper(*items, mode="create", task_id=None, **changes):
    value = dict(mode=mode, task_id=task_id, items=list(items))
    value.update(changes)
    return value


def read_display(raw=None, *, ids=(FIRST,), mode="create", task_id=None, scope=SCOPE):
    app = Flask(__name__)
    app.extensions["results_task_web"] = dict(session_scope=lambda: SCOPE)
    fields = dict(attachment_scope=scope, attachment_ids=json.dumps(list(ids)))
    if raw is not None:
        fields["attachment_display"] = raw if isinstance(raw, str) else json.dumps(raw, ensure_ascii=False)
    with app.test_request_context("/tasks/new", method="POST", data=fields):
        selection = web_attachments.form_selection()
        before = copy.deepcopy(selection)
        result = web_attachments.form_display(selection, task_id=task_id, mode=mode)
        assert selection == before, "presentation must never mutate the actual write fields"
        return result, selection


def test_omitted_display_is_backward_compatible_and_does_not_add_write_fields():
    result, selection = read_display()
    assert result == []
    assert selection == {"attachment_ids": [FIRST]}


@pytest.mark.parametrize("mode,target", [("create", None), ("edit", "101")])
def test_valid_display_subset_remains_outside_final_selection_and_keeps_exact_values(mode, target):
    display = [item(SECOND, name="<img src=x onerror=alert(1)>.txt", temporary=False), item()]
    result, selection = read_display(wrapper(*display, mode=mode, task_id=target),
                                     ids=(FIRST, SECOND), mode=mode, task_id=target)
    assert {row["attachment_id"]: row for row in result} == {row["attachment_id"]: row for row in display}
    assert selection == {"attachment_ids": [FIRST, SECOND]}
    assert "attachment_display" not in selection


def test_empty_display_is_valid_for_explicit_empty_selection():
    assert read_display(wrapper(), ids=())[0] == []


@pytest.mark.parametrize("bad", [
    "null", "[]", '"display"', "{broken", "{\"mode\":NaN}",
    wrapper(item(), extra=True),
    {"mode": "create", "items": [item()]},
    wrapper(item(), mode="read"),
    wrapper(item(), mode="edit", task_id="101"),
    wrapper(item(), task_id="101"),
    wrapper(item(), task_id=101),
    wrapper(item(), items=None),
    wrapper(item(), items={"file": item()}),
    wrapper(item(), item()),
    wrapper(item(SECOND)),
    wrapper(item(extra="unknown")),
    wrapper(item(temporary="true")),
    wrapper(item(temporary=1)),
    wrapper(item(name="")),
    wrapper(item(name="x" * 256)),
    wrapper(item(size=True)),
    wrapper(item(size=-1)),
    wrapper(item(size=25 * 1024 * 1024 + 1)),
    wrapper(item(size="23")),
    wrapper(item(sha256="B" * 64)),
    wrapper(item(sha256="b" * 63)),
    wrapper(item(attachment_id=FIRST.upper().replace("00000000", "A0000000", 1))),
    {"mode": "create", "task_id": None, "items": [item()] * 11},
    " " * (16 * 1024 + 1) + "{}",
    '{"mode":"create","mode":"create","task_id":null,"items":[]}',
    '{"mode":"create","task_id":null,"items":[{"attachment_id":"' + FIRST +
        '","name":"first","name":"second","size":23,"sha256":"' + "b" * 64 + '","temporary":true}]}',
], ids=lambda value: "raw-oversize" if isinstance(value, str) and len(value) > 1024 else None)
def test_invalid_or_cross_page_display_is_rejected_without_becoming_write_data(bad):
    with pytest.raises(AppError) as caught:
        read_display(bad)
    assert caught.value.status == 400


@pytest.mark.parametrize("target", ["202", 101, None])
def test_edit_display_must_match_exact_original_task(target):
    with pytest.raises(AppError) as caught:
        read_display(wrapper(item(), mode="edit", task_id=target), mode="edit", task_id="101")
    assert caught.value.status == 400


def test_old_login_scope_is_rejected_before_trusting_any_display_hint():
    with pytest.raises(AppError) as caught:
        read_display(wrapper(item()), scope="c" * 64)
    assert caught.value.status == 409


def test_deeply_nested_display_json_is_a_bad_request_instead_of_server_failure():
    raw = "[" * 5000 + "0" + "]" * 5000
    assert len(raw.encode("utf-8")) < 16384
    with pytest.raises(AppError) as caught:
        read_display(raw)
    assert caught.value.status == 400


def test_display_at_existing_filename_and_size_limits_remains_only_a_hint():
    value = item(name="가" * 255, size=25 * 1024 * 1024)
    result, selection = read_display(wrapper(value))
    assert result == [value]
    assert selection == {"attachment_ids": [FIRST]}
