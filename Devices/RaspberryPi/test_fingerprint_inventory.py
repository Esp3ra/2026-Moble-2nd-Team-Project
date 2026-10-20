import contextlib
import io
import json
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

import fingerprint_inventory as inventory


class ReadOnlySensor:
    """Only metadata methods exist; any fingerprint capture/write fails."""

    def __init__(self):
        self.library_size = 300
        self.template_count = 3
        self.templates = [270, 1, 5]
        self.system_id = 9
        self.counts = [3, 3]
        self.calls = []
        self.fail_at = None

    def read_sysparam(self):
        self.calls.append("parameters")
        return 1 if self.fail_at == "parameters" else 0

    def count_templates(self):
        self.calls.append("count")
        self.template_count = self.counts.pop(0)
        return 1 if self.fail_at == "count" else 0

    def read_templates(self):
        self.calls.append("index")
        return 1 if self.fail_at == "index" else 0


class InventoryTests(unittest.TestCase):
    def test_metadata_only_and_no_inferred_model_or_slot_range(self):
        sensor = ReadOnlySensor()
        report = inventory.read_inventory(sensor, 0)
        self.assertEqual(sensor.calls, ["parameters", "count", "index", "count"])
        self.assertEqual(report["occupied_slots"], [1, 5, 270])
        self.assertEqual(report["reported_capacity"], 300)
        self.assertFalse(report["registration_performed"])
        self.assertIsNone(report["sensor_model"])
        self.assertIsNone(report["zero_slot_registration_supported"])
        self.assertIsNone(report["confirmed_slot_upper_bound"])

    def test_occupied_zero_is_not_proof_that_new_registration_accepts_zero(self):
        sensor = ReadOnlySensor()
        sensor.templates = [0, 1, 2]
        self.assertIsNone(inventory.read_inventory(sensor, 0)["zero_slot_registration_supported"])

    def test_missing_page_or_changed_inventory_is_inconclusive(self):
        for slots, counts in [([1], [3, 3]), ([1, 5, 270], [3, 4]), ([1, 1, 5], [3, 3])]:
            with self.subTest(slots=slots, counts=counts):
                sensor = ReadOnlySensor()
                sensor.templates, sensor.counts = slots, counts
                with self.assertRaises(inventory.InventoryError):
                    inventory.read_inventory(sensor, 0)

    def test_empty_inventory_is_valid_but_never_allocates_a_slot(self):
        sensor = ReadOnlySensor()
        sensor.templates, sensor.counts = [], [0, 0]
        self.assertEqual(inventory.read_inventory(sensor, 0)["occupied_slots"], [])

    def test_failed_commands_never_produce_a_successful_empty_inventory(self):
        for step in ("parameters", "count", "index"):
            with self.subTest(step=step):
                sensor = ReadOnlySensor()
                sensor.fail_at = step
                with self.assertRaises(inventory.InventoryError):
                    inventory.read_inventory(sensor, 0)

    def test_malformed_sensor_values_are_rejected(self):
        for field, value in [("library_size", 0), ("library_size", True), ("library_size", 65536),
                             ("templates", [1, True, 5]), ("templates", [1, -1, 5]),
                             ("templates", [1, 65536, 5]), ("templates", "1,2,3")]:
            with self.subTest(field=field, value=value):
                sensor = ReadOnlySensor()
                setattr(sensor, field, value)
                with self.assertRaises(inventory.InventoryError):
                    inventory.read_inventory(sensor, 0)

    def test_cli_refuses_uart_without_idle_confirmation(self):
        with patch.object(inventory, "_read_connected_inventory") as connect, \
             patch.object(inventory, "_ensure_service_inactive") as check, \
             contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(inventory.main([]), 1)
            connect.assert_not_called()
            check.assert_not_called()

    def test_only_explicitly_inactive_service_can_proceed(self):
        for code, state in [(0, "active"), (3, "activating"), (3, "deactivating"), (3, "failed"), (4, "unknown")]:
            with self.subTest(state=state), \
                 patch.object(inventory.subprocess, "run", return_value=SimpleNamespace(returncode=code, stdout=state)), \
                 patch.object(inventory, "_read_connected_inventory") as connect, \
                 contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(inventory.main(["--confirm-sensor-idle"]), 1)
                connect.assert_not_called()

    def test_cli_reads_metadata_after_service_preflight(self):
        output = io.StringIO()
        with patch.object(inventory.subprocess, "run", return_value=SimpleNamespace(returncode=3, stdout="inactive\n")), \
             patch.object(inventory, "_read_connected_inventory", return_value={"success": True}) as connect, \
             contextlib.redirect_stdout(output):
            self.assertEqual(inventory.main(["--confirm-sensor-idle"]), 0)
        connect.assert_called_once_with("/dev/ttyAMA0", 57600)
        self.assertTrue(json.loads(output.getvalue())["success"])

    def test_library_error_is_redacted(self):
        output = io.StringIO()
        with patch.object(inventory, "_ensure_service_inactive"), \
             patch.object(inventory, "_read_connected_inventory", side_effect=RuntimeError("private-value")), \
             contextlib.redirect_stderr(output):
            self.assertEqual(inventory.main(["--confirm-sensor-idle"]), 1)
        self.assertNotIn("private-value", output.getvalue())
        self.assertFalse(json.loads(output.getvalue())["success"])

    def test_uart_is_exclusive_and_closed_even_when_driver_fails(self):
        uart = Mock()
        uart.__enter__ = Mock(return_value=uart)
        uart.__exit__ = Mock(return_value=False)
        serial_module = SimpleNamespace(Serial=Mock(return_value=uart))
        driver_module = SimpleNamespace(OK=0, Adafruit_Fingerprint=Mock(side_effect=RuntimeError("failure")))
        with patch.dict("sys.modules", serial=serial_module, adafruit_fingerprint=driver_module):
            with self.assertRaises(RuntimeError):
                inventory._read_connected_inventory("/dev/test-uart", 57600)
        serial_module.Serial.assert_called_once_with("/dev/test-uart", 57600, timeout=1, write_timeout=1, exclusive=True)
        uart.__exit__.assert_called_once()


if __name__ == "__main__":
    unittest.main()
