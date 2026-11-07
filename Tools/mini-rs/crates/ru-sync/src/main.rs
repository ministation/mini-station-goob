// SPDX-FileCopyrightText: 2026 Mini Station contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

// Порт режима --check Tools/localize/ru_sync.py: сверяет ключи en-US и ru-RU.
// Контракт для CI и ИИ-сисадмина — последняя строка stdout:
//   ИТОГ ЛОКАЛИЗАЦИИ: ok=1 missing=0 untranslated=... files=0 translated=0 ignored=...
// Коды выхода: 0 — недостающих нет, 1 — есть, 2 — ошибка.
// Режим --translate остаётся в python-скрипте (там DeepSeek API и запись файлов).

use std::collections::HashMap;
use std::collections::HashSet;
use std::path::{Path, PathBuf};
use std::process::ExitCode;

use serde::Deserialize;
use serde_yaml::Value;

const EN_LOCALE_REL: &str = "Resources/Locale/en-US";
const RU_LOCALE_REL: &str = "Resources/Locale/ru-RU";
const CONFIG_REL: &str = "Tools/localize/localize_config.yml";
const IGNORE_REL: &str = "Tools/localize/ru_sync_ignore.txt";
const SUMMARY_PREFIX: &str = "ИТОГ ЛОКАЛИЗАЦИИ:";

// ---------------------------------------------------------------- FTL-парсер

#[derive(Default, Clone)]
struct FtlEntry {
    key: String,
    value: String,
    attributes: Vec<(String, String)>,
    ignore_untranslated: bool,
}

#[derive(Default)]
struct ParsedFtl {
    entries: Vec<FtlEntry>,
}

/// Порт parse_ftl_text: многострочные значения, атрибуты (.desc и другие),
/// комментарии, маркер ignore-untranslated. Заголовок файла для check не нужен.
fn parse_ftl_text(text: &str) -> ParsedFtl {
    let mut parsed = ParsedFtl::default();
    let mut pending: Vec<String> = Vec::new();
    let mut current: Option<FtlEntry> = None;
    let mut current_attr: Option<String> = None;
    let mut entries: Vec<FtlEntry> = Vec::new();

    let normalized = text.replace("\r\n", "\n").replace('\r', "\n");
    for raw_line in normalized.split('\n') {
        let trimmed = raw_line.trim();
        if trimmed.is_empty() {
            pending.push(String::new());
            continue;
        }

        // COMMENT_RE: ^[ \t]*#
        if raw_line.trim_start().starts_with('#') {
            pending.push(raw_line.trim_end().to_string());
            // python здесь терял только ссылку: запись уже лежит в списке.
            if let Some(previous) = current.take() {
                entries.push(previous);
            }
            current_attr = None;
            continue;
        }

        if !raw_line.starts_with(' ') && !raw_line.starts_with('\t') {
            if let Some(key) = message_key(raw_line) {
                let value = match raw_line.split_once('=') {
                    Some((_, tail)) => tail.trim().to_string(),
                    None => String::new(),
                };
                let ignore_untranslated = pending
                    .iter()
                    .any(|line| line.to_lowercase().contains("ignore-untranslated"));
                // Предыдущая запись целиком уходит в результат (как ссылка в python).
                if let Some(previous) = current.take() {
                    entries.push(previous);
                }
                current = Some(FtlEntry {
                    key: key.clone(),
                    value,
                    attributes: Vec::new(),
                    ignore_untranslated,
                });
                current_attr = None;
                pending.clear();
                continue;
            }
        }

        if let Some(entry) = current.as_mut() {
            if let Some((attr_name, attr_value)) = attribute_of(raw_line) {
                current_attr = Some(attr_name.clone());
                entry.attributes.push((attr_name, attr_value));
                pending.clear();
                continue;
            }
            let piece = trimmed;
            let merged = if let Some(attr_name) = current_attr.clone() {
                let existing = entry
                    .attributes
                    .iter_mut()
                    .find(|(name, _)| name == &attr_name)
                    .map(|(_, value)| std::mem::take(value))
                    .unwrap_or_default();
                let merged = format!(
                    "{}{}{}",
                    existing,
                    "\n".repeat(pending.len() + 1),
                    piece
                );
                let merged = merged.trim_matches('\n').to_string();
                if let Some(slot) = entry
                    .attributes
                    .iter_mut()
                    .find(|(name, _)| name == &attr_name)
                {
                    slot.1 = merged.clone();
                }
                merged
            } else {
                let merged = format!(
                    "{}{}{}",
                    entry.value,
                    "\n".repeat(pending.len() + 1),
                    piece
                );
                let merged = merged.trim_matches('\n').to_string();
                entry.value = merged.clone();
                merged
            };
            let _ = merged;
            pending.clear();
            continue;
        }

        pending.clear();
    }

    // Последняя запись ещё не в списке (python держал её по ссылке).
    if let Some(previous) = current.take() {
        entries.push(previous);
    }

    // python в конце делает strip() значений и атрибутов.
    for entry in &mut entries {
        entry.value = entry.value.trim().to_string();
        for (_, value) in &mut entry.attributes {
            *value = value.trim().to_string();
        }
    }
    parsed.entries = entries;
    parsed
}

/// MESSAGE_RE: ^(-?[A-Za-z][A-Za-z0-9_-]*)[ \t]*=
fn message_key(line: &str) -> Option<String> {
    let bytes = line.as_bytes();
    let mut index = 0usize;
    if index < bytes.len() && bytes[index] == b'-' {
        index += 1;
    }
    let start = index;
    if index >= bytes.len() || !bytes[index].is_ascii_alphabetic() {
        return None;
    }
    index += 1;
    while index < bytes.len()
        && (bytes[index].is_ascii_alphanumeric() || bytes[index] == b'_' || bytes[index] == b'-')
    {
        index += 1;
    }
    if index == start {
        return None;
    }
    let mut end = index;
    while end < bytes.len() && (bytes[end] == b' ' || bytes[end] == b'\t') {
        end += 1;
    }
    if end < bytes.len() && bytes[end] == b'=' {
        Some(line[start..index].to_string())
    } else {
        None
    }
}

/// ATTRIBUTE_RE: ^[ \t]+\.([A-Za-z][A-Za-z0-9_-]*)[ \t]*=
fn attribute_of(line: &str) -> Option<(String, String)> {
    if !line.starts_with(' ') && !line.starts_with('\t') {
        return None;
    }
    let rest = line.trim_start();
    let rest = rest.strip_prefix('.')?;
    let name_end = rest
        .find(|c: char| !(c.is_ascii_alphanumeric() || c == '_' || c == '-'))
        .unwrap_or(rest.len());
    if name_end == 0 || !rest.chars().next().unwrap().is_ascii_alphabetic() {
        return None;
    }
    let name = &rest[..name_end];
    let after = rest[name_end..].trim_start();
    let value = match after.split_once('=') {
        Some((_, tail)) => tail.trim().to_string(),
        None => String::new(),
    };
    Some((name.to_string(), value))
}

// ------------------------------------------------------------- конфигурация

#[derive(Default)]
struct Config {
    ignored_en: Vec<String>,
    ignored_ru: Vec<String>,
    untranslated_ru: Vec<String>,
    ignore_rules: Vec<(String, String)>,
}

#[derive(Deserialize)]
struct ConfigYaml {
    #[serde(default)]
    ignored_paths: Option<Vec<String>>,
    #[serde(default)]
    ignored_untranslated_paths: Option<Vec<String>>,
}

fn load_config(root: &Path) -> Result<Config, String> {
    let mut config = Config::default();
    let config_path = root.join(CONFIG_REL);
    if config_path.exists() {
        let text = std::fs::read_to_string(&config_path)
            .map_err(|e| format!("не удалось прочитать {}: {e}", config_path.display()))?;
        let data: Value = serde_yaml::from_str(&text)
            .map_err(|e| format!("не удалось разобрать {}: {e}", config_path.display()))?;
        let data: ConfigYaml = serde_yaml::from_value(data)
            .map_err(|e| format!("не удалось разобрать {}: {e}", config_path.display()))?;
        for raw in data.ignored_paths.unwrap_or_default() {
            let rel = normalize_rel(&raw);
            if rel.is_empty() {
                continue;
            }
            config.ignored_ru.push(rel.clone());
            config.ignored_en.push(ru_rel_to_en(&rel));
        }
        for raw in data.ignored_untranslated_paths.unwrap_or_default() {
            let rel = normalize_rel(&raw);
            if !rel.is_empty() {
                config.untranslated_ru.push(rel);
            }
        }
    }

    let ignore_path = root.join(IGNORE_REL);
    if ignore_path.exists() {
        let text = std::fs::read_to_string(&ignore_path)
            .map_err(|e| format!("не удалось прочитать {}: {e}", ignore_path.display()))?;
        for raw_line in text.lines() {
            let line = raw_line.trim();
            if line.is_empty() || line.starts_with('#') {
                continue;
            }
            let (pattern, reason) = match line.split_once('#') {
                Some((pattern, reason)) => (
                    pattern.trim().to_string(),
                    reason.trim().to_string(),
                ),
                None => (line.to_string(), "причина не указана".to_string()),
            };
            if pattern.is_empty() {
                continue;
            }
            let mut pattern = pattern;
            if pattern.contains("Resources/Locale/") {
                pattern = normalize_rel(&pattern);
                if let Some(stripped) = pattern.strip_prefix(EN_LOCALE_REL) {
                    pattern = stripped
                        .strip_prefix('/')
                        .map(|s| s.to_string())
                        .unwrap_or(stripped.to_string());
                }
            }
            if pattern.contains('[') {
                return Err(format!(
                    "{IGNORE_REL}: glob-классы [..] не поддержаны инструментом: {pattern}"
                ));
            }
            config.ignore_rules.push((pattern, reason));
        }
    }
    Ok(config)
}

fn normalize_rel(value: &str) -> String {
    let mut normalized = value.replace('\\', "/").trim().to_string();
    while normalized.starts_with("./") {
        normalized = normalized[2..].to_string();
    }
    normalized.trim_matches('/').to_string()
}

fn path_under(rel: &str, prefix: &str) -> bool {
    let prefix = normalize_rel(prefix);
    if prefix.is_empty() {
        return false;
    }
    rel == prefix || rel.starts_with(&(prefix + "/"))
}

/// Глоб с * и ?; в ignore-файле классов [..] нет, при их появлении — ошибка выше.
fn match_ignore_pattern(rel: &str, pattern: &str) -> bool {
    let pattern = normalize_rel(pattern);
    if pattern.is_empty() {
        return false;
    }
    if pattern.contains('*') || pattern.contains('?') {
        return glob_match(&pattern, rel);
    }
    let prefix = pattern.trim_end_matches('/');
    rel == prefix || rel.starts_with(&(prefix.to_string() + "/"))
}

fn glob_match(pattern: &str, text: &str) -> bool {
    fn inner(p: &[u8], t: &[u8]) -> bool {
        if p.is_empty() {
            return t.is_empty();
        }
        match p[0] {
            b'*' => {
                for skip in 0..=t.len() {
                    if inner(&p[1..], &t[skip..]) {
                        return true;
                    }
                }
                false
            }
            b'?' => !t.is_empty() && inner(&p[1..], &t[1..]),
            c => !t.is_empty() && t[0] == c && inner(&p[1..], &t[1..]),
        }
    }
    inner(pattern.as_bytes(), text.as_bytes())
}

fn ru_rel_to_en(ru_rel: &str) -> String {
    if ru_rel == RU_LOCALE_REL {
        return EN_LOCALE_REL.into();
    }
    match ru_rel.strip_prefix(RU_LOCALE_REL) {
        Some(rest) => format!("{EN_LOCALE_REL}{rest}"),
        None => ru_rel.to_string(),
    }
}

// ------------------------------------------------------------ типы символов

/// Вырезает спаны как regex `\{[^{}]*\}`: без вложенности — спан от открывающей
/// до закрывающей, внутри которого нет других скобок; иначе оставляем как есть.
fn strip_spans_strict(text: &str, open: char, close: char) -> String {
    let chars: Vec<char> = text.chars().collect();
    let mut result = String::with_capacity(text.len());
    let mut index = 0usize;
    while index < chars.len() {
        if chars[index] == open {
            // Спан не может содержать внутренние { — ищем первую } до такого.
            let mut end = None;
            let mut j = index + 1;
            while j < chars.len() {
                if chars[j] == close {
                    end = Some(j);
                    break;
                }
                if chars[j] == open {
                    break;
                }
                j += 1;
            }
            if let Some(end) = end {
                index = end + 1;
                continue;
            }
        }
        result.push(chars[index]);
        index += 1;
    }
    result
}

/// Вырезает спаны как regex `\[[^\]]*\]`: от открывающей до ПЕРВОЙ закрывающей,
/// внутренние открывающие допустимы ([[a] → матчится "[[a]").
fn strip_spans_first_close(text: &str, open: char, close: char) -> String {
    let chars: Vec<char> = text.chars().collect();
    let mut result = String::with_capacity(text.len());
    let mut index = 0usize;
    while index < chars.len() {
        if chars[index] == open {
            if let Some(end) = (index + 1..chars.len()).find(|&i| chars[i] == close) {
                index = end + 1;
                continue;
            }
        }
        result.push(chars[index]);
        index += 1;
    }
    result
}

fn contains_latin(text: &str) -> bool {
    text.chars().any(|c| c.is_ascii_alphabetic())
}

/// CYRILLIC_RE [А-Яа-яЁё]
fn contains_cyrillic(text: &str) -> bool {
    text.chars()
        .any(|c| matches!(c, 'А'..='я' | 'Ё' | 'ё'))
}

fn is_latin_only(text: &str) -> bool {
    if text.is_empty() {
        return false;
    }
    // порядок как в python: сначала PLACEHOLDER_RE {...}, потом TAG_RE [...]
    let clean = strip_spans_first_close(&strip_spans_strict(text, '{', '}'), '[', ']');
    if clean.trim().is_empty() {
        return false;
    }
    contains_latin(&clean) && !contains_cyrillic(&clean)
}

// ------------------------------------------------------------------- check

struct CheckResult {
    ok: bool,
    missing: usize,
    untranslated: usize,
    files: usize,
    ignored: usize,
    en_keys: usize,
    ru_keys: usize,
}

fn run(root: &Path) -> Result<CheckResult, String> {
    let config = load_config(root)?;

    let en_dir = root.join(EN_LOCALE_REL);
    let ru_dir = root.join(RU_LOCALE_REL);
    if !en_dir.is_dir() {
        return Err(format!("не найден каталог {}", en_dir.display()));
    }
    if !ru_dir.is_dir() {
        return Err(format!("не найден каталог {}", ru_dir.display()));
    }

    // en-US: разбираем каждый файл (rglob, сортировка как в python).
    let mut en_files: Vec<(String, ParsedFtl)> = Vec::new();
    for path in collect_ftl(&en_dir)? {
        let rel = path
            .strip_prefix(root)
            .unwrap_or(&path)
            .to_string_lossy()
            .replace('\\', "/");
        let text = std::fs::read_to_string(&path)
            .map_err(|e| format!("{}: {e}", path.display()))?;
        // python читал с encoding="utf-8-sig": BOM нужно снять.
        let text = text.strip_prefix('\u{feff}').unwrap_or(&text);
        en_files.push((rel, parse_ftl_text(text)));
    }
    en_files.sort_by(|a, b| a.0.cmp(&b.0));

    // ru-RU: плоская карта ключей (последний встреченный выигрывает — как dict).
    let mut ru_entries: HashMap<String, (String, FtlEntry)> = HashMap::new();
    for path in collect_ftl(&ru_dir)? {
        let rel = path
            .strip_prefix(root)
            .unwrap_or(&path)
            .to_string_lossy()
            .replace('\\', "/");
        let text = std::fs::read_to_string(&path)
            .map_err(|e| format!("{}: {e}", path.display()))?;
        let text = text.strip_prefix('\u{feff}').unwrap_or(&text);
        for entry in parse_ftl_text(text).entries {
            ru_entries.insert(entry.key.clone(), (rel.clone(), entry));
        }
    }

    let ignore_reason = |en_rel: &str| -> Option<String> {
        for prefix in &config.ignored_en {
            if path_under(en_rel, prefix) {
                return Some("Tools/localize/localize_config.yml: ignored_paths".into());
            }
        }
        let relative_to_en = en_rel
            .strip_prefix(EN_LOCALE_REL)
            .and_then(|rest| rest.strip_prefix('/'))
            .unwrap_or(en_rel);
        for (pattern, reason) in &config.ignore_rules {
            if match_ignore_pattern(relative_to_en, pattern) {
                return Some(format!("Tools/localize/ru_sync_ignore.txt: {reason}"));
            }
        }
        None
    };

    let ru_file_ignored = |ru_rel: &str| -> bool {
        for prefix in config
            .ignored_ru
            .iter()
            .chain(config.untranslated_ru.iter())
        {
            if path_under(ru_rel, prefix) {
                return true;
            }
        }
        ignore_reason(&ru_rel_to_en(ru_rel)).is_some()
    };

    // collect_missing
    let mut missing = 0usize;
    let mut ignored = 0usize;
    let mut files_with_missing: HashSet<String> = HashSet::new();
    let mut en_keys = 0usize;
    for (en_rel, parsed) in &en_files {
        let reason = ignore_reason(en_rel);
        for entry in &parsed.entries {
            en_keys += 1;
            if ru_entries.contains_key(&entry.key) {
                continue;
            }
            if reason.is_some() {
                ignored += 1;
                continue;
            }
            missing += 1;
            files_with_missing.insert(en_rel.clone());
        }
    }

    // count_untranslated
    let mut untranslated = 0usize;
    for (key, (ru_rel, entry)) in ru_entries.iter() {
        if entry.ignore_untranslated || ru_file_ignored(ru_rel) {
            continue;
        }
        let texts = std::iter::once(&entry.value).chain(entry.attributes.iter().map(|(_, v)| v));
        if texts.clone().any(|text| is_latin_only(text)) {
            untranslated += 1;
            if std::env::var_os("MINI_RU_SYNC_DUMP").is_some() {
                eprintln!("{ru_rel}\t{key}\t{:?}", entry.value);
            }
        }
    }

    Ok(CheckResult {
        ok: missing == 0,
        missing,
        untranslated,
        files: files_with_missing.len(),
        ignored,
        en_keys,
        ru_keys: ru_entries.len(),
    })
}

fn collect_ftl(dir: &Path) -> Result<Vec<PathBuf>, String> {
    let mut found = Vec::new();
    collect_ftl_inner(dir, &mut found)?;
    found.sort();
    Ok(found)
}

fn collect_ftl_inner(dir: &Path, found: &mut Vec<PathBuf>) -> Result<(), String> {
    let entries = std::fs::read_dir(dir).map_err(|e| format!("{}: {e}", dir.display()))?;
    for entry in entries {
        let entry = entry.map_err(|e| format!("{}: {e}", dir.display()))?;
        let path = entry.path();
        if path.is_dir() {
            collect_ftl_inner(&path, found)?;
        } else if path.extension().is_some_and(|ext| ext == "ftl") {
            found.push(path);
        }
    }
    Ok(())
}

fn summary_line(result: &CheckResult) -> String {
    format!(
        "{} ok={} missing={} untranslated={} files={} translated=0 ignored={}",
        SUMMARY_PREFIX,
        if result.ok { 1 } else { 0 },
        result.missing,
        result.untranslated,
        result.files,
        result.ignored
    )
}

fn error_summary_line() -> String {
    format!("{SUMMARY_PREFIX} ok=0 missing=-1 untranslated=-1 files=-1 translated=0 ignored=-1")
}

fn main() -> ExitCode {
    let mut root = std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."));
    let mut args = std::env::args().skip(1);
    while let Some(arg) = args.next() {
        match arg.as_str() {
            "--check" => {}
            "--root" => {
                if let Some(value) = args.next() {
                    root = PathBuf::from(value);
                }
            }
            other => {
                eprintln!("mini-ru-sync: неизвестный аргумент {other} (поддержаны --check, --root)");
                return ExitCode::from(2);
            }
        }
    }

    match run(&root) {
        Ok(result) => {
            eprintln!(
                "Ключей en-US: {}, ru-RU: {}",
                result.en_keys, result.ru_keys
            );
            eprintln!(
                "Недостающих ключей: {}, файлов: {}, игнорировано: {}, потенциально непереведённых: {}",
                result.missing, result.files, result.ignored, result.untranslated
            );
            println!("{}", summary_line(&result));
            if result.ok {
                ExitCode::SUCCESS
            } else {
                ExitCode::FAILURE
            }
        }
        Err(message) => {
            eprintln!("Ошибка: {message}");
            println!("{}", error_summary_line());
            ExitCode::from(2)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn placeholder_only_is_not_latin() {
        assert!(!is_latin_only("{ job-name-inteq-vanguard }"));
    }

    #[test]
    fn pure_latin_counts() {
        assert!(is_latin_only("Some latin"));
    }

    #[test]
    fn cyrillic_not_counted() {
        assert!(!is_latin_only("Интек"));
    }
}
