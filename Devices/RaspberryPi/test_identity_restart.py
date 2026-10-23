"""Synthetic operator handoff; never touches a service, sensor or real account."""
import copy
import hashlib
import json
import secrets
import unittest
from uuid import uuid4

import fingerprint_identity_wire as wire
from fingerprint_identity import IdentityProfile
from fingerprint_enrollment_private import ConfigurationError
from identity_restart import DOCUMENTS, MANIFEST_FILES, SOURCE_FILES, parse_restart_bundle


class RestartObservationTests(unittest.TestCase):
    def setUp(self):
        self.profile = IdentityProfile(str(uuid4()), "synthetic-key", "1", True, 12, 12, 300,
            {kind: secrets.token_bytes(32) for kind in wire.KINDS},
            (secrets.token_bytes(32), secrets.token_bytes(32)))
        self.cycle = str(uuid4())
        self.sources = {name: "a" * 64 for name in SOURCE_FILES}
        self.docs = {
            DOCUMENTS[0]: dict(version=1, contract="member1-deployment-context-PROPOSAL", cycle_id=self.cycle,
                checked_at_utc="2026-10-23T01:00:00+00:00", server_source="b" * 40,
                identity_enabled=False, private_server_profile_loaded=True,
                observation_is_grant=False, pi_restart_readiness="NOT_SUPPLIED"),
            DOCUMENTS[1]: dict(checked_at_utc="2026-10-23T01:00:00Z", scope="synthetic managed history only",
                managed_identity_retired_key_count=0, historical_registration_exclusion_count=0,
                current_profile_pair_equal=True, independent_current_four_and_server_receipt=True,
                unknown_external_history_asserted=False, pi_readiness_authorization=False,
                evidence=[dict(path="synthetic-record", sha256="c" * 64)]),
            DOCUMENTS[2]: dict(version=1, contract="identity-restart-observation-v1-PROPOSAL", cycle_id=self.cycle,
                checked_at_server_utc="2026-10-23T01:00:00.000000Z", observation_only=True,
                pi_ready_authorization=False, pi_restart_readiness="NOT_SUPPLIED", identity_enabled=False,
                schema_checked=True, profiles_checked=1, active_mappings_checked=1,
                profiles=[{name: getattr(self.profile, name) for name in
                           ("device_id", "key_id", "key_epoch", "minimum_slot", "maximum_slot", "capacity")}],
                source_sha256=self.sources, records=[], unresolved_challenges=0,
                persisted_capture_fences=0, retired_inventory_count=0),
            DOCUMENTS[3]: dict(version=1, keys=[]),
        }

    def bundle(self, change=None):
        docs = copy.deepcopy(self.docs)
        if change:
            change(docs)
        files = {name: json.dumps(doc).encode() for name, doc in docs.items()}
        docs[DOCUMENTS[1]]["inventory_sha256"] = hashlib.sha256(files[DOCUMENTS[3]]).hexdigest()
        files[DOCUMENTS[1]] = json.dumps(docs[DOCUMENTS[1]]).encode()
        files["전달_안내.txt"] = b"Synthetic handoff is not an activation grant."
        files["SHA256SUMS.txt"] = "\n".join(hashlib.sha256(files[name]).hexdigest() + "  " + name
                                               for name in sorted(MANIFEST_FILES)).encode()
        return files

    def parse(self, change=None):
        return parse_restart_bundle(self.bundle(change), self.profile, expected_sources=self.sources)

    def record(self, state="issued", fence=None, completed=False):
        return dict(challenge_id=str(uuid4()), context_id=str(uuid4()), client_request_id=str(uuid4()),
                    device_id=self.profile.device_id, purpose="clock_in", state=state,
                    expires_at="2026-10-23T00:59:00Z", lookup_until="2026-10-24T00:58:00Z",
                    workflow_completed=completed, capture_fence_sha256=fence, result_id=None, result_sha256=None)

    def test_empty_off_observation_never_provides_ready(self):
        result = self.parse()
        self.assertFalse(result.identity_enabled)
        self.assertEqual(0, result.unresolved_challenges)
        self.assertTrue(result.sources_compared)
        self.assertFalse(hasattr(result, "ready"))
        with self.assertRaises(TypeError):
            bool(result)
        self.assertNotIn(self.cycle, repr(result))

    def test_corruption_missing_extra_and_duplicate_manifest_fail_closed(self):
        original = self.bundle()
        variants = [original | {DOCUMENTS[2]: b"{}"},
                    {k: v for k, v in original.items() if k != DOCUMENTS[3]},
                    original | {"unexpected.json": b"{}"},
                    original | {"SHA256SUMS.txt": original["SHA256SUMS.txt"] * 2},
                    original | {"SHA256SUMS.txt": original["SHA256SUMS.txt"].replace(b"  ", b"  ../", 1)}]
        for files in variants:
            with self.subTest(keys=list(files)), self.assertRaises(ConfigurationError):
                parse_restart_bundle(files, self.profile)

    def test_different_cycle_profile_epoch_sources_and_invalid_counts_fail(self):
        mutations = [lambda d: d[DOCUMENTS[0]].update(cycle_id=str(uuid4())),
                     lambda d: d[DOCUMENTS[2]]["profiles"][0].update(key_epoch="2"),
                     lambda d: d[DOCUMENTS[2]]["profiles"][0].update(maximum_slot=13),
                     lambda d: d[DOCUMENTS[2]]["source_sha256"].update({"identity_host.py": "d" * 64}),
                     lambda d: d[DOCUMENTS[2]].update(unresolved_challenges=1),
                     lambda d: d[DOCUMENTS[2]].update(profiles_checked=True),
                     lambda d: d[DOCUMENTS[2]].update(identity_enabled=0),
                     lambda d: d[DOCUMENTS[1]].update(historical_registration_exclusion_count=1)]
        for change in mutations:
            with self.subTest(change=mutations.index(change)), self.assertRaises(ConfigurationError):
                self.parse(change)

    def test_claimed_grants_are_rejected_not_consumed(self):
        for name, field, value in [(DOCUMENTS[0], "observation_is_grant", True),
                                   (DOCUMENTS[2], "pi_ready_authorization", True),
                                   (DOCUMENTS[2], "pi_restart_readiness", "READY"),
                                   (DOCUMENTS[1], "pi_readiness_authorization", True)]:
            with self.subTest(field=field), self.assertRaises(ConfigurationError):
                self.parse(lambda d: d[name].update({field: value}))

    def test_issued_in_past_and_verified_pending_remain_unresolved(self):
        for state in ("issued", "verified"):
            def change(d):
                d[DOCUMENTS[2]].update(records=[self.record(state)], unresolved_challenges=1)
            self.assertEqual(1, self.parse(change).unresolved_challenges)

    def test_cancelled_original_ids_and_retained_fence_are_not_dropped(self):
        record = self.record("cancelled", "d" * 64)
        files = self.bundle(lambda d: d[DOCUMENTS[2]].update(records=[record], persisted_capture_fences=1))
        before = dict(files)
        result = parse_restart_bundle(files, self.profile, expected_sources=self.sources)
        self.assertEqual((0, 1, 1), (result.unresolved_challenges, result.persisted_capture_fences, result.record_count))
        self.assertEqual(before, files)
        self.assertEqual(record, json.loads(files[DOCUMENTS[2]])["records"][0])

    def test_duplicate_original_ids_and_hidden_fence_count_are_rejected(self):
        record = self.record("cancelled", "e" * 64)
        for records, fences in [([record, record], 2), ([record], 0)]:
            with self.subTest(count=len(records)), self.assertRaises(ConfigurationError):
                self.parse(lambda d: d[DOCUMENTS[2]].update(records=records, persisted_capture_fences=fences))

    def test_retired_keys_cannot_reuse_current_registration_identity_or_public_keys(self):
        for key in (self.profile.keys["id_request"], self.profile.excluded_keys[0], bytes([1]) * 32):
            def change(d):
                d[DOCUMENTS[3]]["keys"] = [wire.b64encode(key)]
                d[DOCUMENTS[1]]["managed_identity_retired_key_count"] = 1
                d[DOCUMENTS[2]]["retired_inventory_count"] = 1
            with self.assertRaises(ConfigurationError):
                self.parse(change)

    def test_actual_retired_bytes_are_returned_without_printing_and_hash_is_checked(self):
        retired = secrets.token_bytes(32)
        def change(d):
            d[DOCUMENTS[3]]["keys"] = [wire.b64encode(retired)]
            d[DOCUMENTS[1]]["managed_identity_retired_key_count"] = 1
            d[DOCUMENTS[2]]["retired_inventory_count"] = 1
        result = self.parse(change)
        self.assertEqual((retired,), result.retired_keys)
        self.assertNotIn(repr(retired), repr(result))
        files = self.bundle()
        audit = json.loads(files[DOCUMENTS[1]])
        audit["inventory_sha256"] = "0" * 64
        files[DOCUMENTS[1]] = json.dumps(audit).encode()
        files["SHA256SUMS.txt"] = "\n".join(hashlib.sha256(files[n]).hexdigest() + "  " + n for n in sorted(MANIFEST_FILES)).encode()
        with self.assertRaises(ConfigurationError):
            parse_restart_bundle(files, self.profile)


if __name__ == "__main__":
    unittest.main()
