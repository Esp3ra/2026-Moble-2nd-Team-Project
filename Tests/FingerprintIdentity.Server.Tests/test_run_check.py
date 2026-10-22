"""Fault-injected runner lifecycle. No actual process, MySQL, HTTP or sensor."""
import io
from pathlib import Path
import subprocess
import unittest
from unittest.mock import Mock, patch

import run_check as runner


class Child:
    def __init__(self, mode, events):
        self.mode, self.events, self.alive = mode, events, True
        self.returncode = None
        self.stdout, self.stderr = io.StringIO(), io.StringIO()

    def poll(self): return None if self.alive else self.returncode
    def terminate(self):
        self.events.append("terminate")
        self.alive, self.returncode = False, -15
    def kill(self):
        self.events.append("kill")
        self.alive, self.returncode = False, -9
    def wait(self, timeout=None):
        self.events.append("wait")
        if self.alive: raise AssertionError("A live suspended child was not stopped")
        return self.returncode
    def communicate(self, timeout=None):
        self.events.append("communicate")
        if self.mode == "communicate_interrupt": raise KeyboardInterrupt()
        if self.mode == "timeout": raise subprocess.TimeoutExpired("owned-test", timeout, output=b"partial", stderr=b"partial")
        self.alive, self.returncode = False, 0
        return "10 passed in 0.01s\n", ""


class Job:
    def __init__(self, mode, events):
        self.mode, self.events, self.child = mode, events, None
    def assign(self, child):
        self.events.append("assign")
        if self.mode == "assign_failure": raise OSError("synthetic job registration failure")
        if self.mode == "assign_interrupt": raise KeyboardInterrupt()
        self.child = child
        if self.mode == "resume_failure": raise OSError("synthetic primary-thread resume failure")
    def close(self):
        self.events.append("job_close")
        if self.child and self.child.alive:
            self.child.alive, self.child.returncode = False, -9


class RunnerLifecycleTests(unittest.TestCase):
    def exercise(self, mode, failure=None):
        events = []
        child, job = Child(mode, events), Job(mode, events)
        def spawn(*args, **kwargs):
            events.append("spawn")
            self.assertTrue(kwargs["creationflags"] & runner.owned.CREATE_SUSPENDED)
            self.assertTrue(kwargs["creationflags"] & subprocess.CREATE_NO_WINDOW)
            if mode == "spawn_failure": raise OSError("synthetic spawn failure")
            return child
        original_stop = runner.owned.stop_owned_process
        stop = Mock(wraps=original_stop)
        with patch.object(runner.owned, "WindowsJob", return_value=job), \
                patch.object(runner.owned.subprocess, "Popen", side_effect=spawn), \
                patch.object(runner.owned, "stop_owned_process", stop):
            if failure is not None:
                with self.assertRaises(failure): runner.capture_pytest(["owned-test"], Path.cwd(), {})
            else:
                result = runner.capture_pytest(["owned-test"], Path.cwd(), {})
                self.assertEqual(result.returncode, 0)
        self.assertEqual(events.count("spawn"), 1)
        self.assertEqual(events.count("job_close"), 1)
        if mode == "spawn_failure":
            stop.assert_called_once_with(None)
            self.assertNotIn("assign", events)
        else:
            stop.assert_called_once_with(child)
            self.assertEqual(events.count("wait"), 1)
            self.assertFalse(child.alive)
            self.assertTrue(child.stdout.closed and child.stderr.closed)
            self.assertLess(events.index("job_close"), events.index("wait"))
        return events

    def test_job_assignment_failure_stops_waits_and_closes_unassigned_suspended_child(self):
        events = self.exercise("assign_failure", OSError)
        self.assertEqual(events, ["spawn", "assign", "job_close", "terminate", "wait"])
    def test_interrupt_during_job_assignment_still_reaps_unassigned_child(self):
        self.exercise("assign_interrupt", KeyboardInterrupt)
    def test_resume_failure_reaps_child_already_assigned_to_job(self):
        self.exercise("resume_failure", OSError)
    def test_interrupt_during_communication_reaps_owned_child_and_closes_pipes(self):
        self.exercise("communicate_interrupt", KeyboardInterrupt)
    def test_timeout_reaps_owned_child_before_propagating_captured_timeout(self):
        self.exercise("timeout", subprocess.TimeoutExpired)
    def test_normal_completion_waits_and_closes_pipes(self):
        self.exercise("normal")
    def test_spawn_failure_closes_job_without_touching_an_uncreated_child(self):
        self.exercise("spawn_failure", OSError)


if __name__ == "__main__": unittest.main()
