"""Read-only consumer of member 1's private restart observation proposal.

These files describe an observation, never authorize ready(profile). No database,
HTTP, service, sensor or state-file writes occur here. Original IDs stay private.
"""
from dataclasses import dataclass, field
from datetime import datetime, timezone
import hashlib
from pathlib import Path
import re

from fingerprint_enrollment_private import ConfigurationError, _directory, _read_private
import fingerprint_identity_wire as wire

DOCUMENTS = (
    "deployment-context.private.json", "managed-key-audit.private.json",
    "restart-observation.private.json", "retired-identity-keys.private.json",
)
MANIFEST_FILES = frozenset(DOCUMENTS + ("전달_안내.txt",))
SOURCE_FILES = frozenset(("identity_host.py", "features/identity/routes.py", "features/identity/attendance.py"))


@dataclass(frozen=True)
class RestartObservation:
    cycle_id: str = field(repr=False)
    server_source: str
    identity_enabled: bool
    unresolved_challenges: int
    persisted_capture_fences: int
    record_count: int
    retired_keys: tuple = field(repr=False)
    sources_compared: bool

    def __bool__(self):
        raise TypeError("Restart observation is not readiness authorization")


def _require(condition):
    if not condition:
        raise ValueError()


def _count(value):
    _require(type(value) is int and value >= 0)
    return value


def _hash(value, size=64):
    _require(type(value) is str and re.fullmatch("[0-9a-f]{" + str(size) + "}", value) is not None)
    return value


def _time(value):
    _require(type(value) is str)
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    _require(parsed.tzinfo is not None and parsed.utcoffset() == timezone.utc.utcoffset(parsed))
    return parsed


def parse_restart_bundle(files, profile, *, expected_sources=None):
    """Validate exact received bytes and current profile; still observation-only.

    expected_sources, when supplied, must come from independently reviewed server
    sources. Manifest hashes alone establish consistency, not sender authenticity.
    The caller retains the bytes/IDs for original-context reconciliation. Counts
    do not expire/cancel an issued challenge or resolve a verified pending workday.
    """
    try:
        _require(type(files) is dict and set(files) == MANIFEST_FILES | {"SHA256SUMS.txt"})
        _require(all(type(raw) is bytes and len(raw) <= 65536 for raw in files.values()))
        entries = {}
        for line in files["SHA256SUMS.txt"].decode("utf-8-sig").splitlines():
            if not line.strip():
                continue
            match = re.fullmatch(r"([0-9a-f]{64})  (.+)", line)
            _require(match is not None)
            digest, name = match.groups()
            _require(name in MANIFEST_FILES and name not in entries)
            entries[name] = digest
        _require(set(entries) == MANIFEST_FILES)
        _require(all(hashlib.sha256(files[name]).hexdigest() == digest for name, digest in entries.items()))
        deploy, audit, observation, retired = (wire.parse_json(files[name], 65536) for name in DOCUMENTS)
        _require(type(deploy["version"]) is int and deploy["version"] == 1
                 and deploy["contract"] == "member1-deployment-context-PROPOSAL")
        _require(type(observation["version"]) is int and observation["version"] == 1
                 and observation["contract"] == "identity-restart-observation-v1-PROPOSAL")
        wire.canonical_uuid(deploy["cycle_id"])
        _require(deploy["cycle_id"] == observation["cycle_id"])
        _time(deploy["checked_at_utc"])
        _time(observation["checked_at_server_utc"])
        _time(audit["checked_at_utc"])
        _require(deploy["observation_is_grant"] is False and observation["observation_only"] is True
                 and observation["pi_ready_authorization"] is False and audit["pi_readiness_authorization"] is False
                 and deploy["pi_restart_readiness"] == observation["pi_restart_readiness"] == "NOT_SUPPLIED")
        _require(type(deploy["identity_enabled"]) is bool and type(observation["identity_enabled"]) is bool
                 and deploy["identity_enabled"] == observation["identity_enabled"])
        _require(deploy["private_server_profile_loaded"] is True and observation["schema_checked"] is True)
        _hash(deploy["server_source"], 40)
        sources = observation["source_sha256"]
        _require(type(sources) is dict and set(sources) == SOURCE_FILES)
        for digest in sources.values():
            _hash(digest)
        if expected_sources is not None:
            _require(type(expected_sources) is dict and sources == expected_sources)

        _require(type(retired["version"]) is int and retired["version"] == 1 and set(retired) == {"version", "keys"})
        _require(type(retired["keys"]) is list)
        keys = tuple(wire.b64decode(key, 32) for key in retired["keys"])
        _require(len(set(keys)) == len(keys) and all(len(key) == 32 for key in keys))
        # identity_private places the two current registration keys first; any
        # remaining exclusions may already be this same audited retired tuple.
        excluded = tuple(profile.keys.values()) + profile.excluded_keys[:2] + tuple(bytes([n]) * 32 for n in range(1, 5))
        _require(not set(keys).intersection(excluded))
        _require(audit["inventory_sha256"] == hashlib.sha256(files[DOCUMENTS[3]]).hexdigest())
        _require(_count(audit["managed_identity_retired_key_count"]) == len(keys)
                 and _count(observation["retired_inventory_count"]) == len(keys))
        # This proposal supplies no separate historical registration-key bytes.
        # Reject such a claimed exclusion rather than silently dropping it.
        _require(_count(audit["historical_registration_exclusion_count"]) == 0)
        _require(audit["unknown_external_history_asserted"] is False
                 and audit["current_profile_pair_equal"] is True
                 and audit["independent_current_four_and_server_receipt"] is True)
        _require(type(audit["scope"]) is str and bool(audit["scope"].strip())
                 and type(audit["evidence"]) is list and bool(audit["evidence"]))
        for evidence in audit["evidence"]:
            _require(type(evidence["path"]) is str and bool(evidence["path"]))
            _hash(evidence["sha256"])

        expected_profile = {name: getattr(profile, name) for name in
                            ("device_id", "key_id", "key_epoch", "minimum_slot", "maximum_slot", "capacity")}
        profiles = observation["profiles"]
        _require(type(profiles) is list and len(profiles) == 1 and profiles[0] == expected_profile)
        _require(all(type(profiles[0][name]) is int for name in ("minimum_slot", "maximum_slot", "capacity")))
        _require(_count(observation["profiles_checked"]) == 1 and _count(observation["active_mappings_checked"]) > 0)
        records = observation["records"]
        _require(type(records) is list and len(records) <= 1000)
        seen = set()
        unresolved = fences = 0
        fields = set("challenge_id context_id client_request_id device_id purpose state expires_at lookup_until workflow_completed capture_fence_sha256 result_id result_sha256".split())
        for record in records:
            _require(type(record) is dict and set(record) == fields)
            for name in ("challenge_id", "context_id", "client_request_id", "device_id"):
                wire.canonical_uuid(record[name])
            _require(record["challenge_id"] not in seen and record["device_id"] == profile.device_id)
            seen.add(record["challenge_id"])
            _require(record["purpose"] in wire.PURPOSES
                     and record["state"] in {"issued", "verified", "rejected", "cancelled", "expired", "revoked"}
                     and type(record["workflow_completed"]) is bool)
            _require(_time(record["expires_at"]) <= _time(record["lookup_until"]))
            unresolved += record["state"] == "issued" or (record["state"] == "verified" and not record["workflow_completed"])
            if record["capture_fence_sha256"] is not None:
                _hash(record["capture_fence_sha256"])
                fences += 1
            if record["result_id"] is not None:
                wire.canonical_uuid(record["result_id"])
            if record["result_sha256"] is not None:
                _hash(record["result_sha256"])
        _require(_count(observation["unresolved_challenges"]) == unresolved
                 and _count(observation["persisted_capture_fences"]) == fences)
        return RestartObservation(deploy["cycle_id"], deploy["server_source"], deploy["identity_enabled"],
                                  unresolved, fences, len(records), keys, expected_sources is not None)
    except Exception:
        raise ConfigurationError("identity_restart_observation_invalid") from None


def load_restart_bundle(directory, profile, *, expected_sources=None):
    try:
        directory = Path(directory)
        _directory(directory)
        files = {name: _read_private(directory / name, 65536) for name in MANIFEST_FILES | {"SHA256SUMS.txt"}}
        return parse_restart_bundle(files, profile, expected_sources=expected_sources)
    except Exception:
        raise ConfigurationError("identity_restart_observation_unavailable") from None
