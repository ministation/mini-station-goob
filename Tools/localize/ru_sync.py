#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Mini Station contributors
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Синхронизация русской локализации Mini Station с апстримом Goob-Station.

Инструмент решает две задачи.

1. --check: только читает файлы и считает ключи en-US, которых нет в ru-RU
   (ключи FTL глобальны: наличие ключа в любом файле ru-RU закрывает
   потребность). Учитываются ignored_paths из Tools/localize/localize_config.yml
   и собственный список исключений Tools/localize/ru_sync_ignore.txt.
   Сеть не используется, коды выхода: 0 - недостающих нет, 1 - есть, 2 - ошибка.

2. --translate: переводит только недостающие ключи через DeepSeek API
   (OpenAI-совместимый /chat/completions), батчами, и дописывает переводы
   в зеркальные файлы ru-RU. Существующие строки не меняются, повторный запуск
   идемпотентен.

Контракт для ИИ-сисадмина и CI: последняя строка stdout имеет вид
    ИТОГ ЛОКАЛИЗАЦИИ: ok=1 missing=0 untranslated=0 files=0 translated=0 ignored=0
где ok=1 только при missing=0. JSON-отчёт (--report-json) содержит поля
ok, missing, untranslated, files, translated, ignored, changed.

Переменные окружения DeepSeek:
    DEEPSEEK_API_KEY       ключ API, если не задан, читается /root/.deepseek-api-key;
    DEEPSEEK_BASE_URL      базовый URL, по умолчанию https://api.deepseek.com;
    DEEPSEEK_MODEL         модель, по умолчанию deepseek-flash;
    DEEPSEEK_API_KEY_FILE  путь к файлу с ключом.
"""

from __future__ import annotations

import argparse
import fnmatch
import json
import logging
import os
import random
import re
import socket
import sys
import threading
import time
import urllib.error
import urllib.request
from collections import Counter, defaultdict
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable, Sequence

try:
    import yaml
except ImportError:  # pragma: no cover - проверяется в CI установкой pyyaml
    print(
        "Ошибка: не установлена библиотека pyyaml. Установите: pip install pyyaml",
        file=sys.stderr,
    )
    sys.exit(2)


LOG = logging.getLogger("ru_sync")

SCRIPT_DIR = Path(__file__).resolve().parent
DEFAULT_ROOT = SCRIPT_DIR.parent.parent

EN_LOCALE_REL = "Resources/Locale/en-US"
RU_LOCALE_REL = "Resources/Locale/ru-RU"
CONFIG_REL = "Tools/localize/localize_config.yml"
IGNORE_REL = "Tools/localize/ru_sync_ignore.txt"

SUMMARY_PREFIX = "ИТОГ ЛОКАЛИЗАЦИИ:"

# Функциональные литералы: именно эти символы запрещены в переводах.
DASH_CHARS = ("\u2014", "\u2013")

MESSAGE_RE = re.compile(r"^(-?[A-Za-z][A-Za-z0-9_-]*)[ \t]*=")
ATTRIBUTE_RE = re.compile(r"^[ \t]+\.([A-Za-z][A-Za-z0-9_-]*)[ \t]*=")
COMMENT_RE = re.compile(r"^[ \t]*#")
PLACEHOLDER_RE = re.compile(r"\{[^{}]*\}")
TAG_RE = re.compile(r"\[[^\]]*\]")
ANGLE_RE = re.compile(r"<[^<>]*>")
LITERAL_BREAK_RE = re.compile(r"\\n")
LATIN_RE = re.compile(r"[A-Za-z]")
CYRILLIC_RE = re.compile(r"[А-Яа-яЁё]")
FENCE = chr(96) * 3

MAX_ITEMS_PER_BATCH = 26
MAX_FEW_SHOT_PAIRS = 6
DEEPSEEK_RETRIES = 4


class SyncError(RuntimeError):
    """Фатальная ошибка конфигурации или окружения."""


class ApiError(RuntimeError):
    """Ошибка запроса к DeepSeek API."""

    def __init__(self, message: str, status: int | None = None):
        super().__init__(message)
        self.status = status


# ---------------------------------------------------------------------------
# Парсер FTL
# ---------------------------------------------------------------------------


@dataclass
class FtlEntry:
    """Одно сообщение FTL: ключ, значение и атрибуты."""

    key: str
    value: str = ""
    attributes: dict[str, str] = field(default_factory=dict)
    ignore_untranslated: bool = False


@dataclass
class ParsedFtl:
    """Результат разбора одного FTL файла."""

    entries: list[FtlEntry] = field(default_factory=list)
    header: list[str] = field(default_factory=list)


def parse_ftl_text(text: str) -> ParsedFtl:
    """Разбирает текст FTL в список сообщений.

    Понимает многострочные значения, атрибуты (.desc, .suffix и другие),
    комментарии, маркер ignore-untranslated и заголовок файла.
    """

    parsed = ParsedFtl()
    pending: list[str] = []
    current: FtlEntry | None = None
    current_attr: str | None = None
    seen_message = False

    normalized = text.replace("\r\n", "\n").replace("\r", "\n")
    for raw_line in normalized.split("\n"):
        if not raw_line.strip():
            pending.append("")
            continue

        if COMMENT_RE.match(raw_line):
            pending.append(raw_line.rstrip())
            current = None
            current_attr = None
            continue

        if not raw_line[0].isspace():
            match = MESSAGE_RE.match(raw_line)
            if match:
                if not seen_message:
                    header = list(pending)
                    while header and not header[-1].strip():
                        header.pop()
                    parsed.header = header
                    seen_message = True
                value = raw_line.split("=", 1)[1] if "=" in raw_line else ""
                current = FtlEntry(
                    key=match.group(1),
                    value=value.strip(),
                    ignore_untranslated=any(
                        "ignore-untranslated" in line.lower() for line in pending
                    ),
                )
                parsed.entries.append(current)
                current_attr = None
                pending = []
                continue

        if current is not None:
            attr_match = ATTRIBUTE_RE.match(raw_line)
            if attr_match:
                name = attr_match.group(1)
                attr_value = raw_line.split("=", 1)[1] if "=" in raw_line else ""
                current.attributes[name] = attr_value.strip()
                current_attr = name
                continue

            piece = raw_line.strip()
            if current_attr is not None:
                merged = current.attributes[current_attr] + "\n" * (len(pending) + 1) + piece
                current.attributes[current_attr] = merged.strip("\n")
            else:
                merged = current.value + "\n" * (len(pending) + 1) + piece
                current.value = merged.strip("\n")
            pending = []
            continue

        pending = []

    for entry in parsed.entries:
        entry.value = entry.value.strip()
        entry.attributes = {
            name: value.strip() for name, value in entry.attributes.items()
        }

    return parsed


def parse_ftl_file(path: Path) -> ParsedFtl:
    """Читает и разбирает FTL файл."""

    return parse_ftl_text(path.read_text(encoding="utf-8-sig", errors="replace"))


# ---------------------------------------------------------------------------
# Форматирование и запись FTL
# ---------------------------------------------------------------------------


def format_entry(key: str, value: str, attributes: dict[str, str]) -> str:
    """Сериализует сообщение FTL: ключ, значение, атрибуты."""

    value_lines = value.split("\n")
    first_line = f"{key} = {value_lines[0]}" if value_lines[0] else f"{key} ="
    lines = [first_line.rstrip()]
    for piece in value_lines[1:]:
        lines.append("    " + piece.rstrip())

    for name, attr_value in attributes.items():
        attr_lines = attr_value.split("\n")
        head = f"    .{name} = {attr_lines[0]}" if attr_lines[0] else f"    .{name} ="
        lines.append(head.rstrip())
        for piece in attr_lines[1:]:
            lines.append("        " + piece.rstrip())

    return "\n".join(lines)


def append_entries_to_file(
    path: Path,
    entries: Sequence[tuple[str, str, dict[str, str]]],
    header: Sequence[str] | None = None,
) -> None:
    """Дописывает сообщения в конец FTL файла, не трогая существующие строки.

    Если файла нет, создаёт его (при наличии header пишет заголовок).
    """

    if not entries:
        return

    path.parent.mkdir(parents=True, exist_ok=True)
    body = "\n\n".join(format_entry(key, value, attrs) for key, value, attrs in entries)
    body_bytes = body.encode("utf-8") + b"\n"

    if path.exists():
        raw = path.read_bytes()
        if not raw:
            path.write_bytes(body_bytes)
            return
        if raw.endswith(b"\n\n"):
            separator = b""
        elif raw.endswith(b"\n"):
            separator = b"\n"
        else:
            separator = b"\n\n"
        with path.open("ab") as handle:
            handle.write(separator)
            handle.write(body_bytes)
        return

    chunks: list[bytes] = []
    if header:
        header_text = "\n".join(line.rstrip() for line in header).strip("\n")
        if header_text.strip():
            chunks.append(header_text.encode("utf-8"))
    chunks.append(body_bytes)
    path.write_bytes(b"\n\n".join(chunks))


# ---------------------------------------------------------------------------
# Проверка переводов
# ---------------------------------------------------------------------------


def has_translatable_text(text: str) -> bool:
    """True, если в строке есть английский текст вне плейсхолдеров и тегов."""

    if not text:
        return False
    clean = PLACEHOLDER_RE.sub("", text)
    clean = TAG_RE.sub("", clean)
    clean = ANGLE_RE.sub("", clean)
    return bool(LATIN_RE.search(clean))


def count_line_breaks(text: str) -> int:
    """Считает переносы строк: реальные и литеральные последовательности \\n."""

    return text.count("\n") + len(LITERAL_BREAK_RE.findall(text))


def translation_errors(source: str, translated: str) -> list[str]:
    """Возвращает список нарушений формата перевода (пустой список - всё хорошо)."""

    errors: list[str] = []
    text = (translated or "").strip()
    if not text:
        errors.append("пустой перевод")
        return errors

    if any(dash in text for dash in DASH_CHARS):
        errors.append("запрещённое длинное или среднее тире")

    for title, pattern in (
        ("плейсхолдеры", PLACEHOLDER_RE),
        ("теги", TAG_RE),
        ("угловые теги", ANGLE_RE),
    ):
        if Counter(pattern.findall(source)) != Counter(pattern.findall(text)):
            errors.append(f"не совпадают {title}")

    if count_line_breaks(source) != count_line_breaks(text):
        errors.append("не совпадает число переносов строк")

    return errors


def should_translate_value(value: str) -> bool:
    """Нужно ли отправлять значение в переводчик (или скопировать как есть)."""

    return has_translatable_text(value)


# ---------------------------------------------------------------------------
# Конфигурация и пути игнорирования
# ---------------------------------------------------------------------------


def normalize_rel(value: str) -> str:
    """Приводит путь из конфигурации к виду с прямыми слэшами без ./ и /. """

    normalized = str(value).replace("\\", "/").strip()
    while normalized.startswith("./"):
        normalized = normalized[2:]
    return normalized.strip("/")


def match_ignore_pattern(rel: str, pattern: str) -> bool:
    """Сопоставляет относительный путь en-US с glob или префиксом из ignore-файла."""

    pattern = normalize_rel(pattern)
    if not pattern:
        return False
    if any(char in pattern for char in "*?["):
        return fnmatch.fnmatchcase(rel, pattern)
    prefix = pattern.rstrip("/")
    return rel == prefix or rel.startswith(prefix + "/")


def path_under(rel: str, prefix: str) -> bool:
    """True, если путь rel лежит внутри префикса prefix."""

    prefix = normalize_rel(prefix)
    if not prefix:
        return False
    return rel == prefix or rel.startswith(prefix + "/")


def en_rel_from_ru(ru_rel: str) -> str:
    """Переводит путь ru-RU в зеркальный путь en-US."""

    if ru_rel == RU_LOCALE_REL:
        return EN_LOCALE_REL
    if ru_rel.startswith(RU_LOCALE_REL + "/"):
        return EN_LOCALE_REL + ru_rel[len(RU_LOCALE_REL):]
    return ru_rel


def ru_rel_from_en(en_rel: str) -> str:
    """Переводит путь en-US в зеркальный путь ru-RU."""

    if en_rel == EN_LOCALE_REL:
        return RU_LOCALE_REL
    if en_rel.startswith(EN_LOCALE_REL + "/"):
        return RU_LOCALE_REL + en_rel[len(EN_LOCALE_REL):]
    return en_rel


# ---------------------------------------------------------------------------
# Снимок локализации и --check
# ---------------------------------------------------------------------------


@dataclass
class RuEntry:
    """Существующий ключ ru-RU."""

    rel: str
    value: str
    attributes: dict[str, str]
    ignore_untranslated: bool


@dataclass
class MissingKey:
    """Недостающий в ru-RU ключ en-US."""

    key: str
    value: str
    attributes: dict[str, str]
    source_rel: str
    ru_rel: str
    ru_path: Path
    header: list[str]


@dataclass
class CheckResult:
    """Итог проверки или перевода."""

    ok: bool
    missing: int
    untranslated: int
    files: int
    translated: int
    ignored: int
    changed: list[str]


class LocalizationSync:
    """Снимок en-US и ru-RU, расчёт недостающих ключей и перевод."""

    def __init__(
        self,
        root: Path | str = DEFAULT_ROOT,
        config_file: Path | str | None = None,
        ignore_file: Path | str | None = None,
    ):
        self.root = Path(root).resolve()
        self.en_dir = self.root / EN_LOCALE_REL
        self.ru_dir = self.root / RU_LOCALE_REL
        self.config_path = self._resolve_option(config_file, CONFIG_REL)
        self.ignore_path = self._resolve_option(ignore_file, IGNORE_REL)

        self.config_ignored_en: list[str] = []
        self.config_ignored_ru: list[str] = []
        self.config_untranslated_ru: list[str] = []
        self.ignore_rules: list[tuple[str, str]] = []

        self.en_files: dict[str, ParsedFtl] = {}
        self.ru_entries: dict[str, RuEntry] = {}
        self._ignored_missing = 0
        self._loaded = False

        self._few_shot_cache: dict[str, list[tuple[str, str]]] = {}

    def _resolve_option(self, value: Path | str | None, default_rel: str) -> Path:
        if value is None:
            return self.root / default_rel
        path = Path(value)
        return path if path.is_absolute() else self.root / path

    # -- загрузка ---------------------------------------------------------

    def load(self) -> None:
        """Читает конфигурацию, en-US и ru-RU."""

        self._load_config()
        self._load_en()
        self._load_ru()
        self._loaded = True

    def _load_config(self) -> None:
        self.config_ignored_en = []
        self.config_ignored_ru = []
        self.config_untranslated_ru = []
        self.ignore_rules = []

        if self.config_path.exists():
            try:
                data = yaml.safe_load(self.config_path.read_text(encoding="utf-8"))
            except (OSError, yaml.YAMLError) as error:
                raise SyncError(f"не удалось прочитать {self.config_path}: {error}") from error
            if isinstance(data, dict):
                for raw in data.get("ignored_paths") or []:
                    rel = normalize_rel(str(raw))
                    if not rel:
                        continue
                    self.config_ignored_ru.append(rel)
                    self.config_ignored_en.append(en_rel_from_ru(rel))
                for raw in data.get("ignored_untranslated_paths") or []:
                    rel = normalize_rel(str(raw))
                    if rel:
                        self.config_untranslated_ru.append(rel)

        if self.ignore_path.exists():
            for raw_line in self.ignore_path.read_text(encoding="utf-8").splitlines():
                line = raw_line.strip()
                if not line or line.startswith("#"):
                    continue
                pattern, _, reason = line.partition("#")
                pattern = pattern.strip()
                reason = reason.strip() or "причина не указана"
                if not pattern:
                    continue
                if "Resources/Locale/" in pattern:
                    pattern = normalize_rel(pattern)
                    if pattern.startswith(EN_LOCALE_REL + "/"):
                        pattern = pattern[len(EN_LOCALE_REL) + 1:]
                self.ignore_rules.append((pattern, reason))

    def _load_en(self) -> None:
        if not self.en_dir.is_dir():
            raise SyncError(f"не найден каталог {self.en_dir}")
        self.en_files = {}
        for path in sorted(self.en_dir.rglob("*.ftl")):
            if not path.is_file():
                continue
            rel = path.relative_to(self.root).as_posix()
            self.en_files[rel] = parse_ftl_file(path)

    def _load_ru(self) -> None:
        if not self.ru_dir.is_dir():
            raise SyncError(f"не найден каталог {self.ru_dir}")
        self.ru_entries = {}
        for path in sorted(self.ru_dir.rglob("*.ftl")):
            if not path.is_file():
                continue
            rel = path.relative_to(self.root).as_posix()
            parsed = parse_ftl_file(path)
            for entry in parsed.entries:
                self.ru_entries[entry.key] = RuEntry(
                    rel=rel,
                    value=entry.value,
                    attributes=entry.attributes,
                    ignore_untranslated=entry.ignore_untranslated,
                )

    # -- игнорирование ----------------------------------------------------

    def ignore_reason(self, en_rel: str) -> str | None:
        """Причина, по которой файл en-US не переводится, или None."""

        for prefix in self.config_ignored_en:
            if path_under(en_rel, prefix):
                return "Tools/localize/localize_config.yml: ignored_paths"

        relative_to_en = en_rel
        if en_rel.startswith(EN_LOCALE_REL + "/"):
            relative_to_en = en_rel[len(EN_LOCALE_REL) + 1:]
        for pattern, reason in self.ignore_rules:
            if match_ignore_pattern(relative_to_en, pattern):
                return f"Tools/localize/ru_sync_ignore.txt: {reason}"
        return None

    def ru_file_ignored(self, ru_rel: str) -> bool:
        """True, если файл ru-RU относится к игнорируемым."""

        for prefix in self.config_ignored_ru:
            if path_under(ru_rel, prefix):
                return True
        for prefix in self.config_untranslated_ru:
            if path_under(ru_rel, prefix):
                return True
        return self.ignore_reason(en_rel_from_ru(ru_rel)) is not None

    # -- расчёт недостающих ----------------------------------------------

    def collect_missing(self) -> list[MissingKey]:
        """Возвращает недостающие ключи en-US в порядке обхода файлов."""

        missing: list[MissingKey] = []
        ignored = 0
        for en_rel, parsed in self.en_files.items():
            reason = self.ignore_reason(en_rel)
            ru_rel = ru_rel_from_en(en_rel)
            header = parsed.header
            for entry in parsed.entries:
                if entry.key in self.ru_entries:
                    continue
                if reason is not None:
                    ignored += 1
                    continue
                missing.append(
                    MissingKey(
                        key=entry.key,
                        value=entry.value,
                        attributes=dict(entry.attributes),
                        source_rel=en_rel,
                        ru_rel=ru_rel,
                        ru_path=self.root / ru_rel,
                        header=header,
                    )
                )
        self._ignored_missing = ignored
        return missing

    @staticmethod
    def files_with_missing(missing: Iterable[MissingKey]) -> int:
        return len({item.source_rel for item in missing})

    def count_untranslated(self) -> int:
        """Оценка числа существующих строк ru-RU без кириллицы (справочно)."""

        count = 0
        for key, entry in self.ru_entries.items():
            if entry.ignore_untranslated:
                continue
            if self.ru_file_ignored(entry.rel):
                continue
            texts = [entry.value, *entry.attributes.values()]
            for text in texts:
                if is_latin_only(text):
                    count += 1
                    break
        return count

    def result(
        self,
        missing: Sequence[MissingKey] | None = None,
        translated: int = 0,
        changed: Sequence[str] | None = None,
    ) -> CheckResult:
        if missing is None:
            missing = self.collect_missing()
        return CheckResult(
            ok=not missing,
            missing=len(missing),
            untranslated=self.count_untranslated(),
            files=self.files_with_missing(missing),
            translated=translated,
            ignored=self._ignored_missing,
            changed=sorted(changed or []),
        )

    def check(self) -> CheckResult:
        """Полная проверка паритета ключей без сети и без записи."""

        self.load()
        return self.result()

    # -- few-shot ---------------------------------------------------------

    def few_shot_pairs(self, source_rel: str) -> list[tuple[str, str]]:
        """Пары en => ru из того же файла (если переводы уже есть)."""

        cached = self._few_shot_cache.get(source_rel)
        if cached is not None:
            return cached

        parsed = self.en_files.get(source_rel)
        pairs: list[tuple[str, str]] = []
        if parsed is not None:
            pairs.extend(self._pairs_from_parsed(parsed, MAX_FEW_SHOT_PAIRS))

        # Новый файл без пары в ru-RU: берём примеры из соседних файлов каталога.
        if len(pairs) < 3:
            parent = source_rel.rsplit("/", 1)[0] + "/"
            for rel, sibling in self.en_files.items():
                if rel == source_rel or not rel.startswith(parent):
                    continue
                pairs.extend(
                    self._pairs_from_parsed(sibling, MAX_FEW_SHOT_PAIRS - len(pairs))
                )
                if len(pairs) >= MAX_FEW_SHOT_PAIRS:
                    break

        self._few_shot_cache[source_rel] = pairs
        return pairs

    def _pairs_from_parsed(
        self, parsed: ParsedFtl, limit: int
    ) -> list[tuple[str, str]]:
        """Пары en => ru из разобранного файла, до limit штук."""

        pairs: list[tuple[str, str]] = []
        if limit <= 0:
            return pairs
        for entry in parsed.entries:
            if len(pairs) >= limit:
                break
            ru_entry = self.ru_entries.get(entry.key)
            if ru_entry is None:
                continue
            en_text = entry.value.strip()
            ru_text = ru_entry.value.strip()
            if not en_text or not ru_text or en_text == ru_text:
                continue
            if not should_translate_value(en_text):
                continue
            if len(en_text) > 240 or len(ru_text) > 240:
                continue
            pairs.append((en_text, ru_text))
        return pairs

    # -- перевод ----------------------------------------------------------

    def translate(
        self,
        limit: int | None = None,
        dry_run: bool = False,
        jobs: int = 3,
        batch_items: int = MAX_ITEMS_PER_BATCH,
        timeout: float = 180.0,
        client: "DeepSeekClient | None" = None,
    ) -> CheckResult:
        """Переводит недостающие ключи и дописывает их в ru-RU."""

        self.load()
        all_missing = self.collect_missing()
        total_missing = len(all_missing)
        LOG.info(
            "Недостающих ключей: %d, файлов: %d, игнорировано: %d",
            total_missing,
            self.files_with_missing(all_missing),
            self._ignored_missing,
        )

        if limit is not None and limit >= 0:
            targets = all_missing[:limit]
        else:
            targets = all_missing

        if not targets:
            LOG.info("Переводить нечего: недостающих ключей нет")
            return self.result(missing=all_missing, translated=0, changed=[])

        units = [self._build_unit(item) for item in targets]
        units = [unit for unit in units if unit is not None]
        item_count = sum(len(unit.items) for unit in units)
        LOG.info(
            "К переводу: %d ключей, %d текстовых элементов, файлов: %d",
            len(units),
            item_count,
            len({unit.source_rel for unit in units}),
        )

        if dry_run:
            self._log_dry_run(units)
            return self.result(missing=all_missing, translated=0, changed=[])

        if not units:
            LOG.warning("У выбранных ключей нет переводимого текста")
            return self.result(missing=all_missing, translated=0, changed=[])

        if client is None:
            client = DeepSeekClient(timeout=timeout)
        return self._run_translation(
            client=client,
            all_missing=all_missing,
            units=units,
            jobs=jobs,
            batch_items=max(1, batch_items),
        )

    def _build_unit(self, item: MissingKey) -> TranslationUnit | None:
        """Готовит один ключ к переводу: значение и атрибуты по отдельности."""

        items: list[TranslationItem] = []
        if should_translate_value(item.value):
            items.append(TranslationItem(item_id=item.key, text=item.value, attribute=None))

        attr_translatable: dict[str, bool] = {}
        for name, value in item.attributes.items():
            translatable = should_translate_value(value)
            attr_translatable[name] = translatable
            if translatable:
                items.append(
                    TranslationItem(
                        item_id=f"{item.key}.{name}",
                        text=value,
                        attribute=name,
                    )
                )

        if not item.value.strip() and not item.attributes:
            LOG.warning("Ключ %s (%s) пустой, пропуск", item.key, item.source_rel)
            return None

        return TranslationUnit(
            key=item.key,
            source_rel=item.source_rel,
            ru_rel=item.ru_rel,
            ru_path=item.ru_path,
            header=list(item.header),
            value=item.value,
            attributes=dict(item.attributes),
            value_translatable=should_translate_value(item.value),
            attr_translatable=attr_translatable,
            items=items,
        )

    def _log_dry_run(self, units: Sequence["TranslationUnit"]) -> None:
        by_file: dict[str, int] = defaultdict(int)
        for unit in units:
            by_file[unit.source_rel] += 1
        for rel, count in sorted(by_file.items(), key=lambda pair: (-pair[1], pair[0]))[:15]:
            LOG.info("  %5d  %s", count, rel)
        if len(by_file) > 15:
            LOG.info("  ... и ещё %d файлов", len(by_file) - 15)

    def _run_translation(
        self,
        client: "DeepSeekClient",
        all_missing: Sequence[MissingKey],
        units: Sequence["TranslationUnit"],
        jobs: int,
        batch_items: int,
    ) -> CheckResult:
        units_by_file: dict[str, list[TranslationUnit]] = defaultdict(list)
        for unit in units:
            units_by_file[unit.source_rel].append(unit)

        states: dict[str, dict] = {}
        tasks: list[tuple[str, list[TranslationUnit]]] = []
        for rel, file_units in units_by_file.items():
            batches = chunk_units(file_units, batch_items)
            states[rel] = {
                "units": file_units,
                "remaining": len(batches),
                "results": {},
            }
            for batch in batches:
                tasks.append((rel, batch))

        lock = threading.Lock()
        counter = {"translated": 0, "api_errors": 0, "skipped": 0, "chunks": 0}
        changed: list[str] = []

        LOG.info("Батчей: %d, конкурентность: %d", len(tasks), jobs)

        def worker(rel: str, batch: Sequence[TranslationUnit]) -> tuple[str, dict[str, str], int]:
            items = [item for unit in batch for item in unit.items]
            pairs = self.few_shot_pairs(rel)
            results, errors = self._translate_items_with_retry(client, items, pairs, rel)
            return rel, results, errors

        with ThreadPoolExecutor(max_workers=max(1, jobs)) as pool:
            futures = {pool.submit(worker, rel, batch): rel for rel, batch in tasks}
            for future in as_completed(futures):
                rel = futures[future]
                try:
                    result_rel, results, errors = future.result()
                except Exception as error:  # noqa: BLE001 - один батч не должен ронять прогон
                    LOG.error("Ошибка перевода для %s: %s", rel, error)
                    result_rel, results, errors = rel, {}, 0
                    with lock:
                        counter["api_errors"] += 1
                with lock:
                    state = states[result_rel]
                    state["results"].update(results)
                    state["remaining"] -= 1
                    counter["skipped"] += errors
                    counter["chunks"] += 1
                    if state["remaining"] == 0:
                        complete = [
                            unit
                            for unit in state["units"]
                            if unit_is_complete(unit, state["results"])
                        ]
                        if complete:
                            written = self._write_units(complete, state["results"])
                            if written:
                                changed.append(written)
                                counter["translated"] += len(complete)
                                LOG.info(
                                    "Файл %s: добавлено ключей %d",
                                    written,
                                    len(complete),
                                )
                        unfinished = len(state["units"]) - len(complete)
                        if unfinished:
                            LOG.warning(
                                "Файл %s: пропущено ключей %d (ошибки перевода)",
                                result_rel,
                                unfinished,
                            )

        LOG.info(
            "Получено переводов ключей: %d, ошибок API: %d, пропущено элементов: %d",
            counter["translated"],
            counter["api_errors"],
            counter["skipped"],
        )
        if counter["translated"] == 0 and units and counter["api_errors"]:
            raise SyncError(
                "не удалось получить ни одного перевода, проверьте DEEPSEEK_API_KEY и сеть"
            )

        self._load_ru()
        remaining = self.collect_missing()
        return self.result(
            missing=remaining,
            translated=counter["translated"],
            changed=changed,
        )

    def _translate_items_with_retry(
        self,
        client: "DeepSeekClient",
        items: Sequence["TranslationItem"],
        few_shot: Sequence[tuple[str, str]],
        source_rel: str,
    ) -> tuple[dict[str, str], int]:
        """Переводит элементы батча, повторяет плохие ответы один раз."""

        if not items:
            return {}, 0

        pending = {item.item_id: item.text for item in items}
        results: dict[str, str] = {}
        for attempt in range(2):
            if not pending:
                break
            messages = build_messages(pending, few_shot)
            content = client.complete(messages, max_tokens=self._max_tokens(len(pending)))
            try:
                parsed = parse_translations(content)
            except ValueError as error:
                LOG.warning(
                    "Некорректный ответ модели для %s: %s", source_rel, error
                )
                if attempt == 0:
                    continue
                break
            for item_id, text in parsed.items():
                if item_id not in pending:
                    continue
                problems = translation_errors(pending[item_id], text)
                if problems:
                    if attempt == 0:
                        LOG.warning(
                            "Повторный запрос для %s (%s): %s",
                            item_id,
                            source_rel,
                            "; ".join(problems),
                        )
                    else:
                        LOG.warning(
                            "Пропуск %s (%s): %s",
                            item_id,
                            source_rel,
                            "; ".join(problems),
                        )
                    continue
                results[item_id] = text.strip()
            pending = {
                item_id: text for item_id, text in pending.items() if item_id not in results
            }

        return results, len(pending)

    @staticmethod
    def _max_tokens(item_count: int) -> int:
        # Запас нужен, потому что reasoning-модель тратит токены на размышления.
        return 8192


    def _write_units(
        self,
        units: Sequence["TranslationUnit"],
        results: dict[str, str],
    ) -> str | None:
        """Дописывает готовые переводы в файл ru-RU, возвращает путь файла."""

        if not units:
            return None
        ru_path = units[0].ru_path
        entries: list[tuple[str, str, dict[str, str]]] = []
        for unit in units:
            value = unit.value
            if unit.value_translatable:
                value = results.get(unit.key, value)
            attributes: dict[str, str] = {}
            for name, original in unit.attributes.items():
                if unit.attr_translatable.get(name):
                    attributes[name] = results.get(f"{unit.key}.{name}", original)
                else:
                    attributes[name] = original
            entries.append((unit.key, value, attributes))

        append_entries_to_file(ru_path, entries, header=units[0].header)
        return units[0].ru_rel


# ---------------------------------------------------------------------------
# Модели перевода
# ---------------------------------------------------------------------------


@dataclass
class TranslationItem:
    """Один текстовый элемент ключа (значение или атрибут)."""

    item_id: str
    text: str
    attribute: str | None = None


@dataclass
class TranslationUnit:
    """Ключ целиком: значение и атрибуты переводятся отдельно."""

    key: str
    source_rel: str
    ru_rel: str
    ru_path: Path
    header: list[str]
    value: str
    attributes: dict[str, str]
    value_translatable: bool
    attr_translatable: dict[str, bool]
    items: list[TranslationItem]


def chunk_units(
    units: Sequence[TranslationUnit], max_items: int
) -> list[list[TranslationUnit]]:
    """Делит ключи на батчи по количеству текстовых элементов."""

    chunks: list[list[TranslationUnit]] = []
    current: list[TranslationUnit] = []
    current_items = 0
    for unit in units:
        unit_items = len(unit.items)
        if current and current_items + unit_items > max_items:
            chunks.append(current)
            current = []
            current_items = 0
        current.append(unit)
        current_items += unit_items
    if current:
        chunks.append(current)
    return chunks


def unit_is_complete(unit: TranslationUnit, results: dict[str, str]) -> bool:
    """True, если переведены все текстовые элементы ключа."""

    return all(item.item_id in results for item in unit.items)


# ---------------------------------------------------------------------------
# DeepSeek API
# ---------------------------------------------------------------------------

SYSTEM_PROMPT = (
    "Ты профессиональный переводчик игровой локализации Space Station 14 "
    "(форк Mini Station, русский язык). Ты переводишь строки интерфейса, "
    "сообщения и описания с английского на русский. Пиши так, как пишут "
    "в русской локализации SS14: естественно, кратко, без канцелярита. "
    "Обращайся к игроку на \"вы\"."
    "\nОбязательные правила:"
    "\n1. Не используй длинное или среднее типографское тире. Вместо него ставь "
    "запятую, двоеточие или обычный дефис."
    "\n2. Полностью сохраняй служебные конструкции: {...} (плейсхолдеры, функции, "
    "селекторы), теги [...] и <...>, последовательность \\n, числа, проценты и "
    "имена переменных. Внутри фигурных скобок ничего не переводи и не меняй."
    "\n3. Имена собственные и аббревиатуры оставляй как есть (OOC, AI, PAI), если "
    "для них нет устоявшегося варианта из глоссария."
    "\n4. Не добавляй пояснений, кавычек вокруг перевода и лишних пробелов."
    "\n5. Ответ: строго один JSON-объект вида "
    "{\"translations\": {\"id\": \"перевод\"}} для всех переданных id, "
    "без markdown и текста вокруг."
    "\nГлоссарий SS14: station = станция; crew = экипаж; security = СБ; "
    "security officer = офицер СБ; antagonist = антагонист; traitor = предатель; "
    "round = раунд; syndicate = Синдикат; Nanotrasen = Нанотрейзен; "
    "CentCom = Центком; station AI = ИИ станции; ghost role = роль призрака; "
    "shuttle = шаттл; uplink = аплинк; hardsuit = скафандр; PDA = КПК; "
    "lavaland = Лаваленд; salvage = утилизация; cargo = карго; "
    "research = исследовательский отдел; medbay = медбей; brig = бриг; "
    "department = отдел; access = доступ; airlock = шлюз; "
    "nuclear operatives = ядерные оперативники; changeling = генокрад; "
    "heretic = еретик; wizard = волшебник; xenobiologist = ксенобиолог; "
    "bartender = бармен; chaplain = священник; clown = клоун; mime = мим; "
    "janitor = уборщик; assistant = ассистент; captain = капитан; "
    "synth = Синтет; borg = борг; cyborg = киборг; fax = факс; "
    "station beacon = маяк станции; warp point = точка перемещения."
)


def build_messages(
    pending: dict[str, str], few_shot: Sequence[tuple[str, str]]
) -> list[dict[str, str]]:
    """Собирает сообщения для DeepSeek API."""

    parts: list[str] = []
    if few_shot:
        parts.append("Примеры перевода (английский => русский):")
        for en_text, ru_text in few_shot:
            parts.append(
                f"{json.dumps(en_text, ensure_ascii=False)} => "
                f"{json.dumps(ru_text, ensure_ascii=False)}"
            )
        parts.append("")

    parts.append("Переведи значения из JSON на русский язык.")
    parts.append(
        "Ключи сохрани без изменений. Верни JSON-объект "
        "{\"translations\": {\"id\": \"перевод\"}} только с переводами."
    )
    parts.append(json.dumps(pending, ensure_ascii=False, indent=1, sort_keys=True))

    return [
        {"role": "system", "content": SYSTEM_PROMPT},
        {"role": "user", "content": "\n".join(parts)},
    ]


def parse_translations(content: str) -> dict[str, str]:
    """Разбирает ответ модели в словарь id => перевод."""

    text = (content or "").strip()
    if not text:
        raise ValueError("пустой ответ модели")

    if text.startswith(FENCE):
        lines = text.splitlines()
        if lines and lines[0].strip().startswith(FENCE):
            lines = lines[1:]
        if lines and lines[-1].strip().startswith(FENCE):
            lines = lines[:-1]
        text = "\n".join(lines).strip()

    data: object
    try:
        data = json.loads(text)
    except json.JSONDecodeError:
        start = text.find("{")
        end = text.rfind("}")
        if start < 0 or end <= start:
            raise ValueError("ответ модели не является JSON") from None
        data = json.loads(text[start:end + 1])

    if isinstance(data, list):
        converted: dict[str, object] = {}
        for row in data:
            if isinstance(row, dict):
                item_id = row.get("id") or row.get("key")
                value = row.get("translation") or row.get("text")
                if item_id is not None and value is not None:
                    converted[str(item_id)] = value
        data = converted

    if not isinstance(data, dict):
        raise ValueError("ответ модели не является JSON-объектом")

    inner = data.get("translations")
    if isinstance(inner, dict):
        data = inner

    result: dict[str, str] = {}
    for key, value in data.items():
        if isinstance(value, str):
            result[str(key)] = value
        elif isinstance(value, (int, float)):
            result[str(key)] = str(value)
        elif isinstance(value, dict):
            for field_name in ("translation", "text", "ru", "value"):
                nested = value.get(field_name)
                if isinstance(nested, str):
                    result[str(key)] = nested
                    break
    return result


def load_api_key() -> str:
    """Ключ DeepSeek из переменной окружения или файла (не печатается)."""

    key = os.environ.get("DEEPSEEK_API_KEY", "").strip()
    if key:
        return key

    key_file = Path(os.environ.get("DEEPSEEK_API_KEY_FILE", "/root/.deepseek-api-key"))
    if key_file.exists():
        try:
            return key_file.read_text(encoding="utf-8").strip()
        except OSError as error:
            raise SyncError(f"не удалось прочитать файл с ключом API: {error}") from error
    return ""


class DeepSeekClient:
    """Минимальный клиент OpenAI-совместимого API DeepSeek."""

    def __init__(
        self,
        api_key: str | None = None,
        base_url: str | None = None,
        model: str | None = None,
        timeout: float = 180.0,
        retries: int = DEEPSEEK_RETRIES,
        temperature: float = 0.2,
    ):
        self.api_key = api_key if api_key is not None else load_api_key()
        if not self.api_key:
            raise SyncError(
                "не найден ключ DeepSeek API: задайте DEEPSEEK_API_KEY или "
                "положите ключ в /root/.deepseek-api-key"
            )

        raw_base = (
            base_url or os.environ.get("DEEPSEEK_BASE_URL") or "https://api.deepseek.com"
        )
        raw_base = raw_base.strip().rstrip("/")
        if raw_base.endswith("/chat/completions"):
            self.url = raw_base
        else:
            self.url = raw_base + "/chat/completions"

        self.model = model or os.environ.get("DEEPSEEK_MODEL") or "deepseek-flash"
        self.timeout = timeout
        self.retries = max(1, retries)
        self.temperature = temperature

    def complete(self, messages: list[dict[str, str]], max_tokens: int) -> str:
        """Отправляет запрос и возвращает content первого ответа."""

        payload = {
            "model": self.model,
            "messages": messages,
            "temperature": self.temperature,
            "max_tokens": max_tokens,
            "response_format": {"type": "json_object"},
        }
        data = self._post(payload)
        try:
            choice = data["choices"][0]
            content = choice["message"]["content"] or ""
            finish_reason = choice.get("finish_reason")
        except (KeyError, IndexError, TypeError) as error:
            raise ApiError(f"неожиданный формат ответа API: {error}") from error
        if not content.strip():
            raise ApiError(
                "API вернул пустой ответ модели (finish_reason=%s)" % finish_reason
            )
        return content

    def _post(self, payload: dict) -> dict:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        headers = {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        }

        last_error = "нет попыток"
        for attempt in range(1, self.retries + 1):
            request = urllib.request.Request(
                self.url, data=body, headers=headers, method="POST"
            )
            try:
                with urllib.request.urlopen(request, timeout=self.timeout) as response:
                    raw = response.read()
                return json.loads(raw.decode("utf-8"))
            except urllib.error.HTTPError as error:
                detail = ""
                try:
                    detail = error.read().decode("utf-8", errors="replace")[:400]
                except OSError:
                    detail = ""
                last_error = f"HTTP {error.code}: {detail}"
                if error.code in (401, 403):
                    raise ApiError(last_error, status=error.code) from error
                if error.code == 429 or 500 <= error.code < 600:
                    self._sleep(attempt)
                    continue
                raise ApiError(last_error, status=error.code) from error
            except (urllib.error.URLError, socket.timeout, TimeoutError, json.JSONDecodeError) as error:
                last_error = str(error)
                self._sleep(attempt)
                continue

        raise ApiError(f"не удалось выполнить запрос после {self.retries} попыток: {last_error}")

    @staticmethod
    def _sleep(attempt: int) -> None:
        delay = min(30.0, 2.0 ** attempt + random.random())
        time.sleep(delay)


# ---------------------------------------------------------------------------
# Дополнительные проверки
# ---------------------------------------------------------------------------


def is_latin_only(text: str) -> bool:
    """Эвристика: строка содержит латиницу и не содержит кириллицы."""

    if not text:
        return False
    clean = TAG_RE.sub("", PLACEHOLDER_RE.sub("", text))
    if not clean.strip():
        return False
    return bool(LATIN_RE.search(clean)) and not CYRILLIC_RE.search(clean)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def setup_logging(verbose: bool = False) -> None:
    logging.basicConfig(
        level=logging.DEBUG if verbose else logging.INFO,
        format="%(asctime)s [%(levelname)s] %(message)s",
        datefmt="%H:%M:%S",
        stream=sys.stderr,
    )


def summary_line(result: CheckResult) -> str:
    return (
        f"{SUMMARY_PREFIX} ok={1 if result.ok else 0} missing={result.missing} "
        f"untranslated={result.untranslated} files={result.files} "
        f"translated={result.translated} ignored={result.ignored}"
    )


def error_summary_line() -> str:
    return (
        f"{SUMMARY_PREFIX} ok=0 missing=-1 untranslated=-1 files=-1 "
        "translated=0 ignored=-1"
    )


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Проверка и перевод недостающих строк русской локализации Mini Station."
        )
    )
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument(
        "--check",
        action="store_true",
        help="Только проверить недостающие ключи, без сети и записи.",
    )
    mode.add_argument(
        "--translate",
        action="store_true",
        help="Перевести недостающие ключи через DeepSeek API.",
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=None,
        help="Ограничить число переводимых ключей за запуск.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Показать план перевода, не обращаясь к API и не изменяя файлы.",
    )
    parser.add_argument(
        "--report-json",
        type=Path,
        default=None,
        help="Записать итоговый отчёт в JSON файл.",
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=DEFAULT_ROOT,
        help="Корень репозитория (по умолчанию определяется по расположению скрипта).",
    )
    parser.add_argument(
        "--config",
        type=Path,
        default=None,
        help="Путь к localize_config.yml.",
    )
    parser.add_argument(
        "--ignore-file",
        type=Path,
        default=None,
        help="Путь к ru_sync_ignore.txt.",
    )
    parser.add_argument(
        "--jobs",
        type=int,
        default=3,
        help="Конкурентность запросов к API, по умолчанию 3.",
    )
    parser.add_argument(
        "--batch-items",
        type=int,
        default=MAX_ITEMS_PER_BATCH,
        help="Максимум текстовых элементов в одном запросе.",
    )
    parser.add_argument(
        "--timeout",
        type=float,
        default=180.0,
        help="Таймаут одного запроса к API в секундах.",
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Подробный лог.",
    )
    return parser


def write_report(path: Path, result: CheckResult) -> None:
    payload = {
        "ok": bool(result.ok),
        "missing": int(result.missing),
        "untranslated": int(result.untranslated),
        "files": int(result.files),
        "translated": int(result.translated),
        "ignored": int(result.ignored),
        "changed": list(result.changed),
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_arg_parser()
    args = parser.parse_args(argv)
    setup_logging(verbose=args.verbose)

    result: CheckResult | None = None
    exit_code = 2

    try:
        sync = LocalizationSync(
            root=args.root,
            config_file=args.config,
            ignore_file=args.ignore_file,
        )
        if args.check:
            result = sync.check()
            LOG.info(
                "Ключей en-US: %d, ru-RU: %d",
                sum(len(parsed.entries) for parsed in sync.en_files.values()),
                len(sync.ru_entries),
            )
            LOG.info(
                "Недостающих ключей: %d, файлов: %d, игнорировано: %d, "
                "потенциально непереведённых: %d",
                result.missing,
                result.files,
                result.ignored,
                result.untranslated,
            )
            if result.missing:
                LOG.warning("Найдены недостающие ключи, перевод неполный")
        else:
            result = sync.translate(
                limit=args.limit,
                dry_run=args.dry_run,
                jobs=args.jobs,
                batch_items=args.batch_items,
                timeout=args.timeout,
            )
        exit_code = 0 if result.ok else 1
    except SyncError as error:
        LOG.error("Ошибка: %s", error)
        exit_code = 2
    except KeyboardInterrupt:
        LOG.error("Прервано пользователем")
        exit_code = 2

    if result is not None:
        if args.report_json is not None:
            try:
                write_report(args.report_json, result)
            except OSError as error:
                LOG.error("Не удалось записать отчёт: %s", error)
                exit_code = 2
        print(summary_line(result))
    else:
        print(error_summary_line())

    return exit_code


if __name__ == "__main__":
    sys.exit(main())
