// SPDX-FileCopyrightText: 2026 Mini Station contributors
// SPDX-License-Identifier: MIT

// Порт RobustToolbox/Schemas/validate_rsis.py: проверяет целостность RSI —
// meta.json против JSON-схемы, мусор в каталогах, PNG против метаданных
// (размер кратен кадру, хватает места под кадры), согласованность delays.
// PNG декодировать целиком не нужно — достаточно заголовка IHDR.
//
// Валидатор поддерживает сабсет Draft-7, которого хватает схеме rsi.json
// (type/required/properties/items/enum). Незнакомое проверяющее ключевое
// слово — ошибка, а не молчаливый пропуск: схема устареет громко.

use std::path::{Path, PathBuf};
use std::process::ExitCode;

use serde_json::Value;

/// Файлы, которые допустимы внутри каталога .rsi помимо PNG.
const ALLOWED_RSI_DIR_GARBAGE: [&str; 4] = ["meta.json", ".DS_Store", "thumbs.db", ".directory"];

/// Ключевые слова схемы, которые только описывают её и на проверку не влияют.
const ANNOTATION_KEYWORDS: [&str; 6] = [
    "$schema",
    "$id",
    "title",
    "description",
    "default",
    "examples",
];

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    match run(&args) {
        Ok(failed) => {
            if failed {
                ExitCode::FAILURE
            } else {
                ExitCode::SUCCESS
            }
        }
        Err(message) => {
            eprintln!("mini-validate-rsi: {message}");
            ExitCode::FAILURE
        }
    }
}

fn run(args: &[String]) -> Result<bool, String> {
    // Использование: mini-validate-rsi --schema <файл> <каталог> [каталог...]
    let mut schema_path: Option<&str> = None;
    let mut directories: Vec<&str> = Vec::new();
    let mut iter = args.iter();
    while let Some(arg) = iter.next() {
        match arg.as_str() {
            "--schema" => {
                schema_path =
                    Some(iter.next().ok_or("после --schema должен идти путь к схеме")?);
            }
            other => directories.push(other),
        }
    }
    let schema_path =
        schema_path.ok_or("не задана схема: укажите --schema RobustToolbox/Schemas/rsi.json")?;
    if directories.is_empty() {
        return Err("не заданы каталоги для проверки".into());
    }

    let schema = read_json(Path::new(schema_path))
        .map_err(|e| format!("не удалось прочитать схему {schema_path}: {e}"))?;

    let mut errors: Vec<(PathBuf, String)> = Vec::new();

    for dir in directories {
        for rsi in find_rsi_dirs(Path::new(dir)) {
            check_rsi(&rsi, &schema, &mut errors);
        }
    }

    for (path, message) in &errors {
        println!("{}: {message}", path.display());
    }
    Ok(!errors.is_empty())
}

/// Рекурсивный поиск каталогов *.rsi внутри dir.
fn find_rsi_dirs(dir: &Path) -> Vec<PathBuf> {
    let mut found = Vec::new();
    find_rsi_dirs_inner(dir, &mut found);
    found
}

fn find_rsi_dirs_inner(dir: &Path, found: &mut Vec<PathBuf>) {
    let Ok(entries) = std::fs::read_dir(dir) else {
        return;
    };
    for entry in entries.flatten() {
        let path = entry.path();
        if path.is_dir() {
            if path.extension().is_some_and(|ext| ext == "rsi") {
                found.push(path);
            } else {
                find_rsi_dirs_inner(&path, found);
            }
        }
    }
}

fn check_rsi(rsi: &Path, schema: &Value, errors: &mut Vec<(PathBuf, String)>) {
    let meta_path = rsi.join("meta.json");
    let meta = match read_json(&meta_path) {
        Ok(value) => value,
        Err(e) => {
            errors.push((rsi.to_path_buf(), format!("Failed to read meta.json: {e}")));
            return;
        }
    };

    let mut schema_errors = Vec::new();
    validate_value(&meta, schema, "$", &mut schema_errors);
    if !schema_errors.is_empty() {
        for (json_path, message) in schema_errors {
            errors.push((rsi.to_path_buf(), format!("meta.json: [{json_path}] {message}")));
        }
        // meta.json может быть побит, дальнейшие проверки небезопасны.
        return;
    }

    let Some(states) = meta.get("states").and_then(Value::as_array) else {
        errors.push((rsi.to_path_buf(), "meta.json: нет массива states".into()));
        return;
    };
    let Some((frame_w, frame_h)) = meta
        .get("size")
        .and_then(|size| size.get("x").and_then(Value::as_u64).zip(size.get("y").and_then(Value::as_u64)))
    else {
        errors.push((rsi.to_path_buf(), "meta.json: нет size.x/size.y".into()));
        return;
    };

    let state_names: Vec<&str> = states
        .iter()
        .filter_map(|state| state.get("name").and_then(Value::as_str))
        .collect();

    // В каталоге RSI не должно быть мусора, все PNG обязаны быть в метаданных.
    let Ok(entries) = std::fs::read_dir(rsi) else {
        errors.push((rsi.to_path_buf(), "каталог RSI не читается".into()));
        return;
    };
    for entry in entries.flatten() {
        let name = entry.file_name().to_string_lossy().into_owned();
        if ALLOWED_RSI_DIR_GARBAGE.contains(&name.as_str()) {
            continue;
        }
        if !name.ends_with(".png") {
            errors.push((rsi.to_path_buf(), format!("Illegal file inside RSI: {name}")));
            continue;
        }
        let png_state_name = &name[..name.len() - 4];
        if !state_names.contains(&png_state_name) {
            errors.push((rsi.to_path_buf(), format!("PNG not defined in metadata: {name}")));
        }
    }

    // Проверки на каждый стейт.
    for state in states {
        let Some(state_name) = state.get("name").and_then(Value::as_str) else {
            continue;
        };

        if state_name.is_empty() {
            errors.push((rsi.to_path_buf(), "state name cannot be an empty string.".into()));
            return;
        }

        let directions = state.get("directions").and_then(Value::as_u64).unwrap_or(1);
        let delays = state.get("delays").and_then(Value::as_array).filter(|d| !d.is_empty());

        if let Some(delays) = delays {
            if directions as usize != delays.len() {
                errors.push((
                    rsi.to_path_buf(),
                    format!("{state_name}: direction count ({directions}) doesn't match delay set specified ({})", delays.len()),
                ));
                continue;
            }

            // Robust округляет суммарную длительность до миллисекунд.
            let lengths: Vec<f64> = delays
                .iter()
                .filter_map(Value::as_array)
                .map(|frames| round3(frames.iter().filter_map(Value::as_f64).sum()))
                .collect();
            if let Some(first) = lengths.first() {
                if lengths.iter().any(|l| l != first) {
                    let joined: Vec<String> = lengths.iter().map(|l| l.to_string()).collect();
                    errors.push((
                        rsi.to_path_buf(),
                        format!("{state_name}: mismatching total durations between state directions: {}", joined.join(", ")),
                    ));
                }
            }
        }

        // PNG стейта: существует, читается, размер кратен кадру, хватает места под кадры.
        let png_path = rsi.join(format!("{state_name}.png"));
        let sheet = match read_png_size(&png_path) {
            Ok(size) => size,
            Err(_) => {
                errors.push((rsi.to_path_buf(), format!("{state_name}: failed to open state {state_name}.png")));
                continue;
            }
        };

        if sheet.0 % frame_w != 0 || sheet.1 % frame_h != 0 {
            errors.push((
                rsi.to_path_buf(),
                format!("{state_name}: sprite sheet of {}x{} is not size multiple of RSI size ({frame_w}x{frame_h}).png", sheet.0, sheet.1),
            ));
            continue;
        }

        let frames_w = sheet.0 / frame_w;
        let frames_h = sheet.1 / frame_h;
        let frame_count: usize = delays
            .map(|delays| delays.iter().filter_map(Value::as_array).map(Vec::len).sum())
            .unwrap_or(directions as usize);

        let max_sheet_frames = frames_w * frames_h;
        if frame_count > max_sheet_frames as usize {
            errors.push((
                rsi.to_path_buf(),
                format!(
                    "{state_name}: sprite sheet of {}x{} is too small, metadata defines {frame_count} frames, but it can only fit {max_sheet_frames} at most",
                    sheet.0, sheet.1
                ),
            ));
        }
    }
}

/// Мини-валидатор сабсета Draft-7: type / required / properties / items / enum.
fn validate_value(instance: &Value, schema: &Value, path: &str, errors: &mut Vec<(String, String)>) {
    let Value::Object(schema) = schema else {
        return;
    };

    // Незнакомые проверяющие ключевые слова — громкое падение.
    for key in schema.keys() {
        if !ANNOTATION_KEYWORDS.contains(&key.as_str())
            && !matches!(key.as_str(), "type" | "required" | "properties" | "items" | "enum" | "additionalProperties" | "additionalItems")
        {
            errors.push((
                path.into(),
                format!("неизвестное ключевое слово схемы \"{key}\" — обновите mini-validate-rsi"),
            ));
        }
    }

    if let Some(Value::Array(enum_values)) = schema.get("enum") {
        if !enum_values.iter().any(|allowed| allowed == instance) {
            errors.push((
                path.into(),
                format!("{instance} is not one of {enum_values:?}"),
            ));
        }
    }

    if let Some(expected) = schema.get("type").and_then(Value::as_str) {
        if !type_matches(instance, expected) {
            errors.push((
                path.into(),
                format!("{instance} is not of type \"{expected}\""),
            ));
            return; // Дальнейшие проверки по свойствам бессмысленны.
        }
    }

    match instance {
        Value::Object(map) => {
            if let Some(Value::Array(required)) = schema.get("required") {
                for key in required {
                    if let Some(key) = key.as_str() {
                        if !map.contains_key(key) {
                            errors.push((
                                path.into(),
                                format!("\"{key}\" is a required property"),
                            ));
                        }
                    }
                }
            }
            if let Some(Value::Object(properties)) = schema.get("properties") {
                for (key, subschema) in properties {
                    if let Some(value) = map.get(key) {
                        validate_value(value, subschema, &format!("{path}.{key}"), errors);
                    }
                }
            }
        }
        Value::Array(items) => {
            let Some(items_schema) = schema.get("items") else {
                return;
            };
            for (index, item) in items.iter().enumerate() {
                validate_value(item, items_schema, &format!("{path}[{index}]"), errors);
            }
        }
        _ => {}
    }
}

fn type_matches(value: &Value, expected: &str) -> bool {
    match expected {
        "object" => value.is_object(),
        "array" => value.is_array(),
        "string" => value.is_string(),
        "boolean" => value.is_boolean(),
        "null" => value.is_null(),
        "number" => value.is_number(),
        // JSON не различает 1 и 1.0: целое проходит проверку и на "integer", и на "number".
        "integer" => value.is_i64() || value.is_u64(),
        _ => {
            // Незнакомый тип уже отловлен на уровне схемы выше; считаем совпадением,
            // чтобы не дублировать ошибку.
            true
        }
    }
}

/// Читает JSON, отрезая BOM (python открывал с encoding="utf-8-sig").
fn read_json(path: &Path) -> Result<Value, String> {
    let bytes = std::fs::read(path).map_err(|e| e.to_string())?;
    let bytes = bytes.strip_prefix(&[0xEF, 0xBB, 0xBF][..]).unwrap_or(&bytes);
    serde_json::from_slice(bytes).map_err(|e| e.to_string())
}

/// Размеры изображения из заголовка (без декодирования). Питон-оригинал
/// открывал файл через PIL, который понимает формат по содержимому — поэтому
/// принимаем PNG и WebP: в Resources/Textures встречаются только они
/// (11 файлов-переименований WebP уже есть на мастере).
fn read_png_size(path: &Path) -> Result<(u64, u64), String> {
    const PNG_SIGNATURE: [u8; 8] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    let bytes = std::fs::read(path).map_err(|e| e.to_string())?;
    if bytes.len() >= 24 && bytes[..8] == PNG_SIGNATURE {
        if &bytes[12..16] != b"IHDR" {
            return Err("IHDR не первый чанк".into());
        }
        let width = u32::from_be_bytes(bytes[16..20].try_into().unwrap());
        let height = u32::from_be_bytes(bytes[20..24].try_into().unwrap());
        return Ok((width as u64, height as u64));
    }
    read_webp_size(&bytes)
}

/// Размеры WebP (подформаты VP8 / VP8L / VP8X).
fn read_webp_size(bytes: &[u8]) -> Result<(u64, u64), String> {
    if bytes.len() < 30 || &bytes[0..4] != b"RIFF" || &bytes[8..12] != b"WEBP" {
        return Err("неизвестный формат изображения".into());
    }
    match &bytes[12..16] {
        b"VP8 " => {
            // Кадр lossy: сигнатура 0x9d 0x01 0x2a, затем ширина и высота (14 бит LE).
            if bytes.len() < 30 || bytes[23..26] != [0x9D, 0x01, 0x2A] {
                return Err("битый VP8-кадр".into());
            }
            let width = u16::from_le_bytes(bytes[26..28].try_into().unwrap()) & 0x3FFF;
            let height = u16::from_le_bytes(bytes[28..30].try_into().unwrap()) & 0x3FFF;
            Ok((width as u64, height as u64))
        }
        b"VP8L" => {
            // Lossless: байт-сигнатура 0x2f, затем 14 бит ширины-1 и 14 бит высоты-1.
            if bytes.len() < 25 || bytes[20] != 0x2F {
                return Err("битый VP8L-кадр".into());
            }
            let bits = u32::from_le_bytes(bytes[21..25].try_into().unwrap());
            let width = (bits & 0x3FFF) + 1;
            let height = ((bits >> 14) & 0x3FFF) + 1;
            Ok((width as u64, height as u64))
        }
        b"VP8X" => {
            // Extended: тело чанка с 20-го байта: флаги (1), резерв (3),
            // затем ширина-1 и высота-1 по 24 бита LE.
            if bytes.len() < 30 {
                return Err("битый VP8X-заголовок".into());
            }
            let width = u32::from_le_bytes([bytes[24], bytes[25], bytes[26], 0]) + 1;
            let height = u32::from_le_bytes([bytes[27], bytes[28], bytes[29], 0]) + 1;
            Ok((width as u64, height as u64))
        }
        other => Err(format!("неизвестный WebP-чанк {:?}", String::from_utf8_lossy(other))),
    }
}

fn round3(value: f64) -> f64 {
    (value * 1000.0).round() / 1000.0
}
