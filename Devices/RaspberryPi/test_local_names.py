import json
import tempfile
import unittest
from pathlib import Path

from pi_api import USER_NAMES, load_user_names


class LocalNamesTests(unittest.TestCase):
    def test_repository_contains_no_registered_names(self):
        self.assertEqual(USER_NAMES, {})

    def test_missing_file_keeps_generic_display_labels(self):
        with tempfile.TemporaryDirectory() as folder:
            self.assertEqual(load_user_names(Path(folder) / 'missing.json'), {})

    def test_local_json_converts_slots_without_changing_account_identity(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'names.json'
            path.write_text(json.dumps({'5': '테스트 사용자'}), encoding='utf-8')
            self.assertEqual(load_user_names(path), {5: '테스트 사용자'})

    def test_invalid_or_ambiguous_labels_are_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'names.json'
            for value in ([], {'05': 'test'}, {'-1': 'test'}, {'5': ''},
                          {'5': 'name\nline'}, {'65536': 'test'}, {'5': 3}):
                with self.subTest(value=value):
                    path.write_text(json.dumps(value), encoding='utf-8')
                    with self.assertRaises(ValueError):
                        load_user_names(path)


if __name__ == '__main__':
    unittest.main()
