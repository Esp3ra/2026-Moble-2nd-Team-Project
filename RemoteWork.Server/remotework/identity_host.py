"""Explicit private server composition; never a Pi restart-readiness authority.

No keys, DDL, accounts, contexts or sensor operations are generated here.
"""
import base64
import json
import os
from pathlib import Path
import stat
import subprocess

from flask import Flask
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from .features.identity import validate_configuration
from .features.identity.attendance import AttendanceWorkflow
from .features.identity.models import Challenge
from .features.enrollment.models import Device, Mapping


def _object(pairs):
    result = {}
    for name, value in pairs:
        if name in result:
            raise ValueError("Duplicate private configuration field")
        result[name] = value
    return result


def _key(value):
    if type(value) is not str:
        raise ValueError("Invalid private key")
    result = base64.b64decode(value + "=" * (-len(value) % 4), altchars=b"-_", validate=True)
    if len(result) != 32 or base64.urlsafe_b64encode(result).rstrip(b"=").decode() != value:
        raise ValueError("Invalid private key")
    return result


def read_private(path):
    """Read bounded regular private files; permissions are never repaired."""
    path = Path(path).absolute()
    for item in (path.parent, path):
        info = item.lstat()
        if item.is_symlink() or getattr(info, "st_file_attributes", 0) & 0x400:
            raise ValueError("Linked private configuration is forbidden")
        if os.name == "posix":
            if info.st_uid != os.geteuid() or info.st_mode & 0o077:
                raise ValueError("Private configuration owner/mode required")
    if not stat.S_ISREG(path.stat().st_mode) or path.stat().st_nlink != 1:
        raise ValueError("Private regular single-link file required")
    if os.name == "nt":
        # Compare SIDs, not localized account names. Never print ACLs or contents.
        script = """$ErrorActionPreference='Stop';
        $me=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;
        $allowed=@($me,'S-1-5-18','S-1-5-32-544');
        foreach($p in @($env:RW_PRIVATE_INPUT,(Split-Path -LiteralPath $env:RW_PRIVATE_INPUT))) {
          $acl=Get-Acl -LiteralPath $p;
          if($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -ne $me){exit 2};
          foreach($a in $acl.Access){
            if($a.AccessControlType -eq 'Allow' -and
              $a.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value -notin $allowed){exit 3}
          }
        };exit 0"""
        env = dict(os.environ, RW_PRIVATE_INPUT=str(path))
        # A PowerShell 7 parent can export incompatible module paths to Windows
        # PowerShell 5. Use its own built-in security module, not caller modules.
        env["PSModulePath"] = str(Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/Modules")
        result = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", script],
                                env=env, capture_output=True, timeout=15)
        if result.returncode:
            raise ValueError("Private configuration ACL required (check " + str(result.returncode) + ")")
    with path.open("rb") as stream:
        raw = stream.read(65537)
    if len(raw) > 65536:
        raise ValueError("Private configuration too large")
    return json.loads(raw.decode("utf-8"), object_pairs_hook=_object)


def configuration(base, document, inventory):
    """Decode an operator-supplied inventory; an empty list is not audit evidence."""
    if (type(document) is not dict or set(document) != {"version", "contract", "profiles", "receipt"}
            or type(document["version"]) is not int or document["version"] != 1
            or document["contract"] != "identity-host-private-v1-PROPOSAL"):
        raise ValueError("Invalid private server configuration")
    if (type(inventory) is not dict or set(inventory) != {"version", "keys"}
            or type(inventory["version"]) is not int or inventory["version"] != 1
            or type(inventory["keys"]) is not list or len(inventory["keys"]) > 256):
        raise ValueError("Explicit retired-key inventory required")
    excluded = tuple(_key(v) for v in inventory["keys"])
    if len(set(excluded)) != len(excluded):
        raise ValueError("Duplicate retired key")
    profiles = document["profiles"]
    if type(profiles) is not dict or not 1 <= len(profiles) <= 64:
        raise ValueError("Invalid private profiles")
    decoded = {}
    for device_id, source in profiles.items():
        if type(source) is not dict or type(source.get("keys")) is not dict:
            raise ValueError("Invalid private profile")
        decoded[device_id] = dict(source, keys={k: _key(v) for k, v in source["keys"].items()})
    receipt = document["receipt"]
    if type(receipt) is not dict or set(receipt) != {"key_id", "key"}:
        raise ValueError("Invalid private receipt configuration")
    config = dict(base, IDENTITY_DEVICE_PROFILES=decoded, IDENTITY_EXCLUDED_KEYS=excluded,
                  IDENTITY_WORKFLOW_PROVIDER=AttendanceWorkflow(_key(receipt["key"]), receipt["key_id"]))
    validate_configuration(config)
    return config


def preflight(app):
    """Read-only same-DB checks; NOT a ready(profile) callback or approval.

    Even expired issued challenges stay blockers: use the existing authenticated
    GET/explicit cancel path. Do not rewrite states or clear capture fences.
    """
    from .features.identity.routes import storage
    engine = app.extensions["db_engine"]
    if engine.dialect.name != "mysql":
        raise ValueError("Identity host requires MySQL")
    checker = Flask("identity-host-preflight")
    checker.extensions.update(db_engine=engine, identity_workflow=app.config["IDENTITY_WORKFLOW_PROVIDER"])
    with checker.app_context():
        storage()
    profiles = validate_configuration(app.config)
    blockers, captured, mappings = 0, 0, 0
    with Session(engine) as db:
        for device_id, profile in profiles.items():
            device = db.get(Device, device_id)
            if (not device or not device.enabled or not profile["enabled"]
                    or str(device.key_epoch) != profile["key_epoch"]
                    or any(getattr(device, k) != profile[k] for k in ("capacity", "minimum_slot", "maximum_slot"))):
                raise ValueError("Current device profile mismatch")
            rows = db.scalars(select(Mapping).where(Mapping.device_id == device_id, Mapping.active.is_(True))).all()
            if not rows or any(not profile["minimum_slot"] <= m.slot <= profile["maximum_slot"] for m in rows):
                raise ValueError("Current active mapping required")
            mappings += len(rows)
            blockers += db.scalar(select(func.count()).select_from(Challenge).where(
                Challenge.device_id == device_id,
                (Challenge.state == "issued") | ((Challenge.state == "verified") & Challenge.workflow_completed.is_(False))))
            captured += db.scalar(select(func.count()).select_from(Challenge).where(
                Challenge.device_id == device_id, Challenge.capture_nonce.is_not(None)))
    return {"schema_checked": True, "profiles_checked": len(profiles), "active_mappings_checked": mappings,
            "unresolved_challenges": blockers, "persisted_capture_fences": captured,
            "identity_enabled": bool(app.config["ENABLE_DEVICE_IDENTITY_DRAFT"]),
            "retired_inventory_checked": True, "retired_inventory_provenance_verified": False,
            "pi_restart_readiness": "NOT_SUPPLIED", "physical_test": "NOT_RUN"}


def create_private_app(profile_path, retired_path, *, enabled, base_overrides=None):
    from . import create_app
    # Enrollment setup loads the existing registration keys later in create_app.
    # First assemble that configuration without installing identity routes.
    base = create_app(dict(base_overrides or {}, ENABLE_DEVICE_IDENTITY_DRAFT=False))
    try:
        config = configuration(base.config, read_private(profile_path), read_private(retired_path))
    finally:
        base.extensions["db_engine"].dispose()
    config["ENABLE_DEVICE_IDENTITY_DRAFT"] = enabled
    app = create_app(config)
    try:
        report = preflight(app)
        app.extensions["identity_host_preflight"] = report
        return app
    except Exception:
        app.extensions["db_engine"].dispose()
        raise
