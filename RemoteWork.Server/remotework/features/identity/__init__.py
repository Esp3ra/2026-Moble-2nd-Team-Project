"""Opt-in RW-ID-v2 HTTP/MySQL service; no schema creation or device IO."""
from copy import deepcopy
from types import MappingProxyType

from ... import device_identity_contract as wire


def validate_configuration(config):
    """Trusted operator injection, never an HTTP body or a readiness ACK."""
    if config.get("MISSED_CLOCKOUT_POLICY", "") not in ("", "absence-v1"):
        raise ValueError("Unknown missed clock-out policy")
    values = config.get("IDENTITY_DEVICE_PROFILES")
    provider = config.get("IDENTITY_WORKFLOW_PROVIDER")
    if not values or not callable(getattr(provider, "current", None)):
        raise RuntimeError("IDENTITY_IMPLEMENTATION_REQUIRED: private profiles and actual workflow provider required")
    result = {}
    excluded = {bytes([i]) * 32 for i in range(1, 5)}
    enrollment = config.get("ENROLLMENT_DEVICE_KEYS")
    if type(enrollment) is not dict or not enrollment:
        raise ValueError("Trusted registration keys must be supplied for exclusion checks")
    for entry in enrollment.values():
        excluded.update((entry["request_key"], entry["result_key"]))
    excluded.update(config.get("IDENTITY_EXCLUDED_KEYS", ()))
    excluded.add(config.get("SECRET_KEY", "").encode())
    for device_id, source in values.items():
        wire.canonical_uuid(device_id)
        p = deepcopy(source)
        if set(p) != {"device_id", "key_id", "key_epoch", "enabled", "minimum_slot", "maximum_slot", "capacity", "keys"}:
            raise ValueError("Invalid identity private profile")
        if p["device_id"] != device_id or type(p["enabled"]) is not bool:
            raise ValueError("Invalid identity private profile")
        wire._revision(p["key_epoch"])
        for name in ("minimum_slot", "maximum_slot", "capacity"):
            wire._slot(p[name])
        if not 0 <= p["minimum_slot"] <= p["maximum_slot"] < p["capacity"]:
            raise ValueError("Invalid identity slot bounds")
        import re
        if type(p["key_id"]) is not str or re.fullmatch(r"[A-Za-z0-9_-]{1,64}", p["key_id"]) is None:
            raise ValueError("Invalid identity key identifier")
        if set(p["keys"]) != wire.KINDS:
            raise ValueError("Four separate identity direction keys required")
        for key in p["keys"].values():
            wire._key(key)
        keys = set(p["keys"].values())
        if len(keys) != 4 or keys & excluded:
            raise ValueError("Public, reused or revoked identity keys are forbidden")
        excluded.update(keys)
        p["keys"] = MappingProxyType(p["keys"])
        result[device_id] = MappingProxyType(p)
    validate_workflow_keys = getattr(provider, "validate_keys", None)
    if callable(validate_workflow_keys):
        validate_workflow_keys(excluded)
    return MappingProxyType(result)


def install(app, profiles):
    if app.extensions["db_engine"].dialect.name != "mysql":
        raise RuntimeError("Identity service requires MySQL")
    app.extensions["identity_profiles"] = profiles
    app.extensions["identity_workflow"] = app.config["IDENTITY_WORKFLOW_PROVIDER"]
    from .routes import bp
    app.register_blueprint(bp)
    from .history import bp as history_bp
    app.register_blueprint(history_bp)
