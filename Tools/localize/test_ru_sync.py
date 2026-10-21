#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Mini Station contributors
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Юнит-тесты Tools/localize/ru_sync.py (stdlib unittest, без сети)."""

import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent))

import ru_sync


EN = ru_sync.EN_LOCALE_REL
RU = ru_sync.RU_LOCALE_REL
FENCE = chr(96) * 3


def write_file(root: Path, rel: str, text: str) -> Path:
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


def make_tree(tmp: Path) -> Path:
    (tmp / EN).mkdir(parents=True, exist_ok=True)
    (tmp / RU).mkdir(parents=True, exist_ok=True)
    (tmp / "Tools" / "localize").mkdir(parents=True, exist_ok=True)
    return tmp


class ParseTests(unittest.TestCase):
    def test_simple_messages_and_header(self):
        parsed = ru_sync.parse_ftl_text(
            "# SPDX-License-Identifier: AGPL-3.0-or-later\n"
            "# вторая строка\n"
            "\n"
            "first = Hello\n"
            "second-key = World\n"
        )
        self.assertEqual([entry.key for entry in parsed.entries], ["first", "second-key"])
        self.assertEqual(parsed.entries[0].value, "Hello")
        self.assertEqual(parsed.entries[1].value, "World")
        self.assertEqual(len(parsed.header), 2)
        self.assertIn("SPDX", parsed.header[0])

    def test_multiline_value(self):
        parsed = ru_sync.parse_ftl_text(
            "message = First line\n"
            "    Second line\n"
            "    \n"
            "    Third line\n"
            "next = Value\n"
        )
        self.assertEqual(parsed.entries[0].value, "First line\nSecond line\n\nThird line")
        self.assertEqual(parsed.entries[1].key, "next")

    def test_attributes(self):
        parsed = ru_sync.parse_ftl_text(
            "ent-Item = Item name\n"
            "    .desc = Item description\n"
            "    .suffix = DO NOT MAP\n"
        )
        entry = parsed.entries[0]
        self.assertEqual(entry.value, "Item name")
        self.assertEqual(entry.attributes["desc"], "Item description")
        self.assertEqual(entry.attributes["suffix"], "DO NOT MAP")

    def test_ignore_untranslated_marker(self):
        parsed = ru_sync.parse_ftl_text(
            "# ignore-untranslated\n"
            "kept-english = Keep me\n"
            "normal = Normal\n"
        )
        self.assertTrue(parsed.entries[0].ignore_untranslated)
        self.assertFalse(parsed.entries[1].ignore_untranslated)

    def test_term_key(self):
        parsed = ru_sync.parse_ftl_text("-brand-name = Nanotrasen\n")
        self.assertEqual(parsed.entries[0].key, "-brand-name")


class ValidationTests(unittest.TestCase):
    def test_good_translation(self):
        self.assertEqual(
            ru_sync.translation_errors(
                "You have {$count} items in {THE($target)}.",
                "У вас {$count} предметов в {THE($target)}.",
            ),
            [],
        )

    def test_placeholder_mismatch(self):
        errors = ru_sync.translation_errors("Hello {$name}", "Привет { $name }")
        self.assertTrue(any("плейсхолдер" in error for error in errors))

    def test_tag_mismatch(self):
        errors = ru_sync.translation_errors(
            "[color=red]Danger[/color]", "[color=green]Опасно[/color]"
        )
        self.assertTrue(any("тег" in error for error in errors))

    def test_angle_tag_mismatch(self):
        errors = ru_sync.translation_errors("Usage: <uid>", "Использование: uid")
        self.assertTrue(any("угловые" in error for error in errors))

    def test_forbidden_dashes(self):
        errors = ru_sync.translation_errors("Some text", "Текст \u2014 с тире")
        self.assertTrue(any("тире" in error for error in errors))
        errors = ru_sync.translation_errors("Some text", "Текст \u2013 с тире")
        self.assertTrue(any("тире" in error for error in errors))

    def test_empty_translation(self):
        self.assertTrue(ru_sync.translation_errors("Some text", "   "))

    def test_line_breaks_preserved(self):
        self.assertEqual(
            ru_sync.translation_errors("First\nSecond", "Первая\nВторая"), []
        )
        errors = ru_sync.translation_errors("First\nSecond", "Первая Вторая")
        self.assertTrue(any("перенос" in error for error in errors))

    def test_has_translatable_text(self):
        self.assertTrue(ru_sync.has_translatable_text("Station crew"))
        self.assertFalse(ru_sync.has_translatable_text("{ ent-Crate }"))
        self.assertFalse(ru_sync.has_translatable_text("[bold]{$item}[/bold]"))
        self.assertFalse(ru_sync.has_translatable_text("Блоб"))
        self.assertFalse(ru_sync.has_translatable_text(""))

    def test_is_latin_only(self):
        self.assertTrue(ru_sync.is_latin_only("Station crew"))
        self.assertFalse(ru_sync.is_latin_only("Экипаж станции"))
        self.assertFalse(ru_sync.is_latin_only("[bold]{$item}[/bold]"))
        self.assertFalse(ru_sync.is_latin_only(""))


class FormattingTests(unittest.TestCase):
    def test_format_and_parse_roundtrip(self):
        text = ru_sync.format_entry(
            "key",
            "Первая строка\nВторая строка",
            {"desc": "Описание", "suffix": "DO NOT MAP"},
        )
        parsed = ru_sync.parse_ftl_text(text)
        self.assertEqual(parsed.entries[0].key, "key")
        self.assertEqual(parsed.entries[0].value, "Первая строка\nВторая строка")
        self.assertEqual(parsed.entries[0].attributes["desc"], "Описание")
        self.assertEqual(parsed.entries[0].attributes["suffix"], "DO NOT MAP")

    def test_append_keeps_existing_content(self):
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            path = write_file(root, RU + "/target.ftl", "old-key = Старое значение\n")
            ru_sync.append_entries_to_file(
                path, [("new-key", "Новое значение", {})]
            )
            text = path.read_text(encoding="utf-8")
            self.assertTrue(text.startswith("old-key = Старое значение\n"))
            self.assertIn("new-key = Новое значение", text)
            parsed = ru_sync.parse_ftl_text(text)
            self.assertEqual([entry.key for entry in parsed.entries], ["old-key", "new-key"])

    def test_create_new_file_with_header(self):
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            path = root / RU / "new" / "file.ftl"
            ru_sync.append_entries_to_file(
                path,
                [("new-key", "Новое значение", {"desc": "Описание"})],
                header=["# SPDX-License-Identifier: AGPL-3.0-or-later"],
            )
            text = path.read_text(encoding="utf-8")
            self.assertIn("SPDX-License-Identifier", text)
            parsed = ru_sync.parse_ftl_text(text)
            self.assertEqual(len(parsed.entries), 1)
            self.assertEqual(parsed.entries[0].attributes["desc"], "Описание")
            self.assertEqual(len(parsed.header), 1)


class IgnoreTests(unittest.TestCase):
    def test_match_ignore_pattern(self):
        self.assertTrue(
            ru_sync.match_ignore_pattern("_Goobstation/patrons/patrons.ftl",
                                         "_Goobstation/patrons/patrons.ftl")
        )
        self.assertTrue(
            ru_sync.match_ignore_pattern("_Trauma/datasets/names/carp.ftl",
                                         "_Trauma/datasets/names/")
        )
        self.assertTrue(
            ru_sync.match_ignore_pattern("_Trauma/datasets/names/carp.ftl",
                                         "_Trauma/datasets/names/*")
        )
        self.assertFalse(
            ru_sync.match_ignore_pattern("_Trauma/datasets/toys.ftl",
                                         "_Trauma/datasets/names/")
        )

    def test_ru_en_path_mapping(self):
        en_rel = "Resources/Locale/en-US/_Goobstation/foo.ftl"
        ru_rel = "Resources/Locale/ru-RU/_Goobstation/foo.ftl"
        self.assertEqual(ru_sync.ru_rel_from_en(en_rel), ru_rel)
        self.assertEqual(ru_sync.en_rel_from_ru(ru_rel), en_rel)

    def test_check_respects_config_and_ignore_file(self):
        with tempfile.TemporaryDirectory() as raw:
            root = make_tree(Path(raw))
            write_file(root, EN + "/foo/a.ftl", "key-a = Alpha\nkey-b = Beta\n")
            write_file(root, EN + "/skip/b.ftl", "key-c = Gamma\n")
            write_file(root, EN + "/ign/c.ftl", "key-d = Delta\n")
            write_file(root, RU + "/bar.ftl", "key-a = Альфа\n")
            write_file(
                root,
                "Tools/localize/localize_config.yml",
                "ignored_paths:\n  - \"Resources/Locale/ru-RU/ign\"\n",
            )
            write_file(
                root,
                "Tools/localize/ru_sync_ignore.txt",
                "skip/  # тестовое исключение\n",
            )

            sync = ru_sync.LocalizationSync(root=root)
            result = sync.check()
            self.assertEqual(result.missing, 1)
            self.assertEqual(result.files, 1)
            self.assertEqual(result.ignored, 2)
            self.assertFalse(result.ok)

            write_file(root, RU + "/bar.ftl", "key-a = Альфа\nkey-b = Бета\n")
            sync = ru_sync.LocalizationSync(root=root)
            result = sync.check()
            self.assertEqual(result.missing, 0)
            self.assertEqual(result.files, 0)
            self.assertEqual(result.ignored, 2)
            self.assertTrue(result.ok)


class CheckCliTests(unittest.TestCase):
    def test_main_check_exit_codes_and_report(self):
        with tempfile.TemporaryDirectory() as raw:
            root = make_tree(Path(raw))
            write_file(root, EN + "/a.ftl", "alpha = Alpha\n")
            write_file(root, RU + "/a.ftl", "alpha = Альфа\n")
            report = root / "report.json"

            stdout = io.StringIO()
            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(io.StringIO()):
                code = ru_sync.main(
                    ["--check", "--root", str(root), "--report-json", str(report)]
                )
            self.assertEqual(code, 0)
            last_line = stdout.getvalue().strip().splitlines()[-1]
            self.assertTrue(last_line.startswith("ИТОГ ЛОКАЛИЗАЦИИ: "))
            self.assertIn("ok=1", last_line)
            self.assertIn("missing=0", last_line)
            payload = json.loads(report.read_text(encoding="utf-8"))
            self.assertEqual(
                set(payload),
                {"ok", "missing", "untranslated", "files", "translated", "ignored", "changed"},
            )
            self.assertTrue(payload["ok"])

            write_file(root, EN + "/b.ftl", "beta = Beta\n")
            stdout = io.StringIO()
            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(io.StringIO()):
                code = ru_sync.main(["--check", "--root", str(root)])
            self.assertEqual(code, 1)
            self.assertIn("missing=1", stdout.getvalue().splitlines()[-1])


class TranslationParsingTests(unittest.TestCase):
    def test_parse_plain_json(self):
        parsed = ru_sync.parse_translations('{"a": "А", "b": "Б"}')
        self.assertEqual(parsed, {"a": "А", "b": "Б"})

    def test_parse_wrapped_translations(self):
        parsed = ru_sync.parse_translations(
            json.dumps({"translations": {"key": "значение"}}, ensure_ascii=False)
        )
        self.assertEqual(parsed, {"key": "значение"})

    def test_parse_fenced_json(self):
        content = FENCE + 'json\n{"key": "значение"}\n' + FENCE
        self.assertEqual(ru_sync.parse_translations(content), {"key": "значение"})

    def test_parse_list_of_objects(self):
        parsed = ru_sync.parse_translations(
            '[{"id": "key", "translation": "значение"}]'
        )
        self.assertEqual(parsed, {"key": "значение"})

    def test_parse_rejects_garbage(self):
        with self.assertRaises(ValueError):
            ru_sync.parse_translations("not a json at all")


class ApiKeyTests(unittest.TestCase):
    def test_key_from_environment(self):
        with mock.patch.dict(os.environ, {"DEEPSEEK_API_KEY": "test-key"}, clear=False):
            self.assertEqual(ru_sync.load_api_key(), "test-key")

    def test_key_from_file(self):
        with tempfile.TemporaryDirectory() as raw:
            key_file = Path(raw) / "key"
            key_file.write_text("file-key\n", encoding="utf-8")
            environment = {
                "DEEPSEEK_API_KEY_FILE": str(key_file),
            }
            with mock.patch.dict(os.environ, environment, clear=False):
                os.environ.pop("DEEPSEEK_API_KEY", None)
                self.assertEqual(ru_sync.load_api_key(), "file-key")

    def test_default_client_settings(self):
        with mock.patch.dict(os.environ, {}, clear=True):
            client = ru_sync.DeepSeekClient(api_key="test-key")
            self.assertEqual(client.url, "https://api.deepseek.com/chat/completions")
            self.assertEqual(client.model, "deepseek-flash")


class FakeClient:
    def __init__(self, translations):
        self.translations = translations
        self.calls = 0

    def complete(self, messages, max_tokens):
        self.calls += 1
        return json.dumps({"translations": self.translations}, ensure_ascii=False)


class TranslateTests(unittest.TestCase):
    def _make_root(self, raw: str) -> Path:
        root = make_tree(Path(raw))
        write_file(
            root,
            EN + "/a.ftl",
            "# SPDX-License-Identifier: AGPL-3.0-or-later\n"
            "first-key = Station crew\n"
            "second-key = Bad string\n",
        )
        write_file(root, RU + "/other.ftl", "other-key = Другое\n")
        return root

    def test_translate_writes_valid_and_skips_invalid(self):
        with tempfile.TemporaryDirectory() as raw:
            root = self._make_root(raw)
            client = FakeClient(
                {
                    "first-key": "Экипаж станции",
                    "second-key": "Плохое \u2014 тире",
                }
            )
            sync = ru_sync.LocalizationSync(root=root)
            result = sync.translate(client=client)

            self.assertEqual(result.translated, 1)
            self.assertEqual(result.missing, 1)
            self.assertEqual(result.files, 1)
            self.assertFalse(result.ok)
            self.assertEqual(result.changed, [RU + "/a.ftl"])

            path = root / RU / "a.ftl"
            text = path.read_text(encoding="utf-8")
            self.assertIn("first-key = Экипаж станции", text)
            self.assertNotIn("second-key", text)
            self.assertIn("SPDX-License-Identifier", text)
            self.assertNotIn("\u2014", text)
            self.assertGreaterEqual(client.calls, 2)

            # Идемпотентность: первый ключ уже есть, повторно не переводится.
            sync = ru_sync.LocalizationSync(root=root)
            client = FakeClient(
                {"first-key": "Экипаж станции", "second-key": "Плохое \u2014 тире"}
            )
            result = sync.translate(client=client)
            self.assertEqual(result.translated, 0)
            self.assertEqual(result.missing, 1)
            self.assertEqual(path.read_text(encoding="utf-8").count("first-key"), 1)

            # После исправления перевода остаток закрывается.
            sync = ru_sync.LocalizationSync(root=root)
            client = FakeClient(
                {"first-key": "Экипаж станции", "second-key": "Плохая строка"}
            )
            result = sync.translate(client=client)
            self.assertEqual(result.translated, 1)
            self.assertEqual(result.missing, 0)
            self.assertTrue(result.ok)
            text = path.read_text(encoding="utf-8")
            self.assertIn("second-key = Плохая строка", text)
            self.assertEqual(text.count("first-key ="), 1)

    def test_translate_limit(self):
        with tempfile.TemporaryDirectory() as raw:
            root = self._make_root(raw)
            client = FakeClient(
                {"first-key": "Экипаж станции", "second-key": "Плохая строка"}
            )
            sync = ru_sync.LocalizationSync(root=root)
            result = sync.translate(limit=1, client=client)
            self.assertEqual(result.translated, 1)
            self.assertEqual(result.missing, 1)

    def test_dry_run_does_not_write(self):
        with tempfile.TemporaryDirectory() as raw:
            root = self._make_root(raw)
            sync = ru_sync.LocalizationSync(root=root)
            result = sync.translate(dry_run=True)
            self.assertEqual(result.translated, 0)
            self.assertEqual(result.missing, 2)
            self.assertFalse((root / RU / "a.ftl").exists())

    def test_few_shot_pairs_from_same_file(self):
        with tempfile.TemporaryDirectory() as raw:
            root = make_tree(Path(raw))
            write_file(
                root,
                EN + "/a.ftl",
                "known = Station crew\nmissing = Station AI\n",
            )
            write_file(root, RU + "/a.ftl", "known = Экипаж станции\n")
            sync = ru_sync.LocalizationSync(root=root)
            sync.load()
            pairs = sync.few_shot_pairs(EN + "/a.ftl")
            self.assertEqual(pairs, [("Station crew", "Экипаж станции")])

    def test_chunk_units_and_complete(self):
        unit_one = ru_sync.TranslationUnit(
            key="one",
            source_rel="a.ftl",
            ru_rel="b.ftl",
            ru_path=Path("b.ftl"),
            header=[],
            value="One",
            attributes={},
            value_translatable=True,
            attr_translatable={},
            items=[ru_sync.TranslationItem("one", "One")],
        )
        unit_two = ru_sync.TranslationUnit(
            key="two",
            source_rel="a.ftl",
            ru_rel="b.ftl",
            ru_path=Path("b.ftl"),
            header=[],
            value="Two",
            attributes={},
            value_translatable=True,
            attr_translatable={},
            items=[
                ru_sync.TranslationItem("two", "Two"),
                ru_sync.TranslationItem("two.desc", "Two desc"),
            ],
        )
        chunks = ru_sync.chunk_units([unit_one, unit_two], max_items=1)
        self.assertEqual([len(chunk) for chunk in chunks], [1, 1])
        self.assertTrue(ru_sync.unit_is_complete(unit_one, {"one": "Один"}))
        self.assertFalse(ru_sync.unit_is_complete(unit_two, {"two": "Два"}))


if __name__ == "__main__":
    unittest.main(verbosity=2)
