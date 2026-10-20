"""Explicit local operator provisioning; no HTTP grant escalation or sensor calls."""
import json
import os
from pathlib import Path
import re
import click
from flask import current_app
from sqlalchemy import select

from ... import enrollment_protocol_draft as wire
from ...common import audit, grant_resource
from ...db import check_schema, transaction
from ...models import AdminGuard, User
from .models import EnrollmentBase, Device


def validate_keys(values):
    if type(values) is not dict:
        raise ValueError("Device key map required")
    for device_id, value in values.items():
        wire.canonical_uuid(device_id)
        if type(value) is not dict or set(value) != {"key_id", "key_epoch", "request_key", "result_key"}:
            raise ValueError("Invalid device key configuration")
        if type(value["key_id"]) is not str or re.fullmatch(r"[A-Za-z0-9_-]{1,64}", value["key_id"]) is None:
            raise ValueError("Invalid key identifier")
        if type(value["key_epoch"]) is not int or not 1 <= value["key_epoch"] <= wire.BIGINT_MAX:
            raise ValueError("Invalid key epoch")
        for direction in ("request_key", "result_key"):
            if type(value[direction]) is not bytes or len(value[direction]) != 32:
                raise ValueError("Each direction requires a 32-byte private key")
        if value["request_key"] == value["result_key"]:
            raise ValueError("Direction keys must differ")
    return values


def install(app):
    if app.extensions["db_engine"].dialect.name != "mysql":
        raise RuntimeError("Enrollment draft requires MySQL")
    app.config.setdefault("ENROLLMENT_TICKET_TTL_SECONDS", 180)
    app.config.setdefault("ENROLLMENT_RESULT_TTL_SECONDS", 86400)
    for field, maximum in (("ENROLLMENT_TICKET_TTL_SECONDS", 3600), ("ENROLLMENT_RESULT_TTL_SECONDS", 604800)):
        if type(app.config[field]) is not int or not 5 <= app.config[field] <= maximum:
            raise ValueError("Invalid enrollment development lifetime")
    values = app.config.get("ENROLLMENT_DEVICE_KEYS")
    path = os.getenv("REMOTEWORK_ENROLLMENT_KEYS_FILE")
    if values is None and path:
        document = wire.parse_json(Path(path).read_bytes(), 1024 * 1024)
        values = {}
        for device_id, entry in document.items():
            if type(entry) is not dict or set(entry) != {"key_id", "key_epoch", "request_key_b64", "result_key_b64"}:
                raise ValueError("Invalid private device key file")
            values[device_id] = {"key_id": entry["key_id"], "key_epoch": entry["key_epoch"],
                "request_key": wire.b64decode(entry["request_key_b64"], 32),
                "result_key": wire.b64decode(entry["result_key_b64"], 32)}
    app.config["ENROLLMENT_DEVICE_KEYS"] = validate_keys(values or {})

    @app.cli.command("init-enrollment-draft")
    def init_enrollment():
        """Create only enrollment draft tables after common init-db (no version bump)."""
        check_schema()
        EnrollmentBase.metadata.create_all(app.extensions["db_engine"])
        click.echo("등록 전용 4개 테이블 준비 완료. 센서·기존 계정 변경 없음.")

    @app.cli.command("add-enrollment-device")
    @click.option("--device-id", required=True)
    @click.option("--display-name", required=True)
    @click.option("--minimum-slot", type=int, required=True)
    @click.option("--maximum-slot", type=int, required=True)
    @click.option("--capacity", type=int, required=True, help="Sensor-reported total capacity; allowed slots may be a narrower range")
    @click.option("--candidate-slots", required=True, help="Operator-confirmed empty slot IDs as comma-separated integers")
    def add_device(device_id, display_name, minimum_slot, maximum_slot, capacity, candidate_slots):
        wire.canonical_uuid(device_id)
        for value in (minimum_slot, maximum_slot, capacity):
            wire._integer(value, 0)
        try:
            slots = [int(s) for s in candidate_slots.split(",")]
        except ValueError:
            raise click.ClickException("후보 슬롯을 정수로 지정해 주세요.")
        if (not 0 <= minimum_slot <= maximum_slot < capacity
                or not slots or len(slots) > capacity or len(set(slots)) != len(slots)
                or any(not minimum_slot <= s <= maximum_slot for s in slots)
                or not 1 <= len(display_name.strip()) <= 80):
            raise click.ClickException("장치 실측 프로필·비어 있는 후보 슬롯을 확인해 주세요.")
        context = current_app.config["ENROLLMENT_DEVICE_KEYS"].get(device_id)
        if context is None:
            raise click.ClickException("장치 방향별 비공개 키를 먼저 설정해 주세요.")
        with transaction() as db:
            db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update())
            if db.get(Device, device_id):
                raise click.ClickException("기존 장치를 덮어쓰지 않습니다. 이관·키 교체는 별도 협의 대상입니다.")
            db.add(Device(device_id=device_id, display_name=display_name.strip(), enabled=True,
                key_id=context["key_id"], key_epoch=context["key_epoch"], minimum_slot=minimum_slot,
                maximum_slot=maximum_slot, capacity=capacity, candidate_slots_json=json.dumps(sorted(slots))))
            audit("enrollment.device_added_cli", "enrollment_device", device_id)
        click.echo("장치 등록 완료. 명시적 관리자 권한 부여 전 API 발급 불가.")

    @app.cli.command("grant-enrollment-device")
    @click.option("--device-id", required=True)
    @click.option("--login-id", required=True)
    def grant_device(device_id, login_id):
        wire.canonical_uuid(device_id)
        with transaction() as db:
            db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update())
            user = db.scalar(select(User).where(User.login_id == login_id).with_for_update())
            if not user or not user.is_active or user.role != "admin" or not db.get(Device, device_id):
                raise click.ClickException("활성 관리자와 사전 등록 장치를 확인해 주세요.")
            grant_resource(db, user.id, "enrollment_device", device_id)
            audit("enrollment.device_granted_cli", "enrollment_device", device_id, {"grantee_id": user.id})
        click.echo("해당 관리자에게 해당 장치의 등록 권한 부여 완료.")
