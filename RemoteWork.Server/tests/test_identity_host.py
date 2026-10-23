"""Private composition and restart blockers; owned MySQL, never physical readiness."""
import base64
from copy import deepcopy
import json
import os
import subprocess
from pathlib import Path
from unittest.mock import patch
from uuid import uuid4

import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session

from remotework.identity_host import configuration, read_private, preflight, create_private_app
from remotework.features.identity.attendance import AttendanceWorkflow
from remotework.features.identity.models import Challenge
from test_device_enrollment import DID, KEYS, env
from test_identity_service import identity, pending, issue, approval, post


def encode(key):
    return base64.urlsafe_b64encode(key).rstrip(b"=").decode()


def fixture():
    profile = dict(device_id=DID, key_id="candidate", key_epoch="1", enabled=True,
                   minimum_slot=0, maximum_slot=299, capacity=300,
                   keys={k: encode(bytes([i]) * 32) for i, k in enumerate(
                       ("id_request", "id_probe", "id_approval", "id_result"), 5)})
    doc = dict(version=1, contract="identity-host-private-v1-PROPOSAL", profiles={DID: profile},
               receipt=dict(key_id="receipt", key=encode(bytes([9]) * 32)))
    base = dict(SECRET_KEY="test-only-" + "x" * 32, ENROLLMENT_DEVICE_KEYS={DID: KEYS})
    return base, doc, dict(version=1, keys=[])


def test_decode_and_independent_workflow():
    config = configuration(*fixture())
    assert config["IDENTITY_DEVICE_PROFILES"][DID]["keys"]["id_request"] == bytes([5]) * 32
    assert len(config["IDENTITY_WORKFLOW_PROVIDER"].schema_tables()) == 2


@pytest.mark.parametrize("change", ["retired", "registration", "receipt", "public", "duplicate",
                                   "padding", "unknown", "epoch", "bounds", "inventory"])
def test_reject_reused_invalid_keys_or_configuration(change):
    base, doc, inventory = fixture()
    keys = doc["profiles"][DID]["keys"]
    if change == "retired": inventory["keys"] = [keys["id_request"]]
    if change == "registration": keys["id_request"] = encode(KEYS["request_key"])
    if change == "receipt": doc["receipt"]["key"] = keys["id_result"]
    if change == "public": keys["id_request"] = encode(bytes([1]) * 32)
    if change == "duplicate": keys["id_probe"] = keys["id_request"]
    if change == "padding": keys["id_request"] += "="
    if change == "unknown": doc["profiles"][DID]["ready"] = True
    if change == "epoch": doc["profiles"][DID]["key_epoch"] = "0"
    if change == "bounds": doc["profiles"][DID]["maximum_slot"] = 300
    if change == "inventory": inventory["keys"] = [encode(b"a" * 32)] * 2
    with pytest.raises((ValueError, TypeError)):
        configuration(base, doc, inventory)


def test_private_file_duplicate_and_acl_failure(tmp_path):
    p = tmp_path / "profile.json"
    p.write_text('{"version":1,"version":2}')
    p.chmod(0o600); tmp_path.chmod(0o700)
    with patch("remotework.identity_host.subprocess.run") as run:
        run.return_value.returncode = 0
        with pytest.raises(ValueError, match="Duplicate"):
            read_private(p)
        p.write_text('{}')
        run.return_value.returncode = 1
        import os
        if os.name == "nt":
            with pytest.raises(ValueError, match="ACL"):
                read_private(p)


def test_private_file_hardlink_rejected(tmp_path):
    p = tmp_path / "profile.json"; p.write_text('{}'); p.chmod(0o600); tmp_path.chmod(0o700)
    (tmp_path / "alias.json").hardlink_to(p)
    with pytest.raises(ValueError, match="single-link"):
        read_private(p)


def test_private_file_actual_owner_acl_and_no_content_in_error(tmp_path):
    p = tmp_path / "profile.json"; p.write_text('{"marker":"test-only"}')
    tmp_path.chmod(0o700); p.chmod(0o600)
    if os.name == "nt":
        script = """$ErrorActionPreference='Stop'; $p=$env:RW_TEST_ACL_PATH;
        $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User;
        $acl=Get-Acl -LiteralPath $p; $acl.SetAccessRuleProtection($true,$false);
        foreach($a in @($acl.Access)){$acl.RemoveAccessRuleSpecific($a)};
        $acl.SetOwner($sid);
        $rule=New-Object Security.AccessControl.FileSystemAccessRule($sid,'FullControl','Allow');
        $acl.AddAccessRule($rule); Set-Acl -LiteralPath $p -AclObject $acl"""
        for path in (tmp_path, p):
            env = dict(os.environ, RW_TEST_ACL_PATH=str(path))
            env["PSModulePath"] = str(Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/Modules")
            result = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", script], env=env, capture_output=True)
            if result.returncode and b"PrivilegeNotHeldException" in result.stderr:
                pytest.skip("Owned temporary fixture owner ACL cannot be replaced by this Windows token")
            assert result.returncode == 0
    assert read_private(p) == {"marker": "test-only"}


def use_workflow(e):
    e[0].config["IDENTITY_WORKFLOW_PROVIDER"] = AttendanceWorkflow(b"h" * 32, "host-test")


def test_mysql_readonly_and_capture_fence_cancel(identity):
    e = identity; use_workflow(e)
    report = preflight(e[0])
    assert report["unresolved_challenges"] == 0
    assert report["pi_restart_readiness"] == "NOT_SUPPLIED"
    assert not report["retired_inventory_provenance_verified"]
    h, _ = pending(e); job, _ = issue(e, h)
    approval(e, h, job, "capture")
    with Session(e[0].extensions["db_engine"]) as db:
        before = [(c.id, c.state, c.capture_nonce) for c in db.scalars(select(Challenge))]
    report = preflight(e[0])
    assert report["unresolved_challenges"] == report["persisted_capture_fences"] == 1
    with Session(e[0].extensions["db_engine"]) as db:
        assert before == [(c.id, c.state, c.capture_nonce) for c in db.scalars(select(Challenge))]
    assert post(e, h, job, "cancel", dict(client_request_id=str(uuid4()))).status_code == 200
    report = preflight(e[0])
    assert report["unresolved_challenges"] == 0 and report["persisted_capture_fences"] == 1


def test_mysql_current_profile_mismatch_fails(identity):
    e = identity; use_workflow(e)
    config = deepcopy(e[2]); config["key_epoch"] = "2"
    e[0].config["IDENTITY_DEVICE_PROFILES"] = {DID: config}
    with pytest.raises(ValueError, match="mismatch"):
        preflight(e[0])


@pytest.mark.parametrize("enabled", [False, True])
def test_mysql_private_composition_preserves_flag_and_restart_recovery(identity, enabled):
    e = identity
    h, _ = pending(e); job, _ = issue(e, h)
    profile = dict(e[2], keys={k: encode(v) for k, v in e[2]["keys"].items()})
    document = dict(version=1, contract="identity-host-private-v1-PROPOSAL", profiles={DID: profile},
                    receipt=dict(key_id="retained-receipt", key=encode(b"r" * 32)))
    with patch("remotework.identity_host.read_private", side_effect=[document, dict(version=1, keys=[])]):
        app = create_private_app("test-only-profile", "test-only-inventory", enabled=enabled, base_overrides=e[0].config)
    try:
        report = app.extensions["identity_host_preflight"]
        assert report["identity_enabled"] is enabled and report["unresolved_challenges"] == 1
        response = app.test_client().get("/api/v1/device-identity/challenges/" + job["challenge_id"], headers=h)
        assert response.status_code == (200 if enabled else 404)
        if enabled:
            assert response.json["data"]["state"] == "issued"
    finally:
        app.extensions["db_engine"].dispose()
