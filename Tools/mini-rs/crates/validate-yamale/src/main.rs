// SPDX-FileCopyrightText: 2026 Mini Station contributors
// SPDX-License-Identifier: MIT

// Порт проверки схем yamale (PaulRitter/yaml-schema-validator) для CI:
// schema mapfile.yml (карты) и rga.yml (attributions) + их кастомные
// валидаторы из RobustToolbox/Schemas/*_validators.py.
//
// Поддержан сабсет синтаксиса yamale, который используют эти схемы:
// int(), str(), bool(), float(), any(), list(v, min=, max=, required=),
// map(v, key=v, min=, max=), include('имя'), enum("a", "b"), а также
// кастомные comp(), license(), url(). Незнакомый валидатор — громкая
// ошибка: если движковая схема обновится, инструмент напомнит о себе.
// Как и в yamale, маппинги строгие: лишние ключи в данных — ошибка.
//
// YAML-парсер — serde_yaml (бэкенд libyaml): у него та же лояльность к
// мультистрочным flow-коллекциям и BOM/CRLF, что у pyyaml в оригинале.

use std::collections::HashMap;
use std::path::Path;
use std::process::ExitCode;

use serde::Deserialize;
use serde_yaml::{Value, Value as Y};

/// Лицензии из rga_validators.py (валидатор license()).
const RGA_LICENSES: [&str; 17] = [
    "CC-BY-3.0",
    "CC-BY-4.0",
    "CC-BY-SA-3.0",
    "CC-BY-SA-4.0",
    "CC-BY-NC-3.0",
    "CC-BY-NC-4.0",
    "CC-BY-NC-SA-3.0",
    "CC-BY-NC-SA-4.0",
    "CC-BY-ND-3.0",
    "CC-BY-ND-4.0",
    "CC-BY-NC-ND-4.0",
    "CC0-1.0",
    "MIT",
    "CC SAMPLING+ 1.0",
    "OFL-1.1",
    "Apache-2.0",
    "Custom", // лицензия описана в поле copyright
];

#[derive(Clone, Debug)]
enum Kind {
    Int,
    Str,
    Bool,
    Float,
    Any,
    List(Box<VNode>),
    Map { value: Box<VNode>, key: Box<VNode> },
    Include(String),
    Enum(Vec<Value>),
    Custom(CustomKind),
    /// Строгое отображение: ключ -> узел схемы (вложенная схема или include-тело).
    Fields(HashMap<String, VNode>),
}

#[derive(Clone, Copy, Debug)]
enum CustomKind {
    Comp,
    License,
    Url,
}

#[derive(Clone, Debug)]
struct VNode {
    kind: Kind,
    required: bool,
    min: Option<usize>,
    max: Option<usize>,
}

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
            eprintln!("mini-validate-yamale: {message}");
            ExitCode::FAILURE
        }
    }
}

fn run(args: &[String]) -> Result<bool, String> {
    let mut schema_path: Option<&str> = None;
    let mut files: Vec<&str> = Vec::new();
    let mut iter = args.iter();
    while let Some(arg) = iter.next() {
        match arg.as_str() {
            "--schema" => {
                schema_path = Some(iter.next().ok_or("после --schema должен идти путь к схеме")?);
            }
            other => files.push(other),
        }
    }
    let schema_path =
        schema_path.ok_or("не задана схема: укажите --schema RobustToolbox/Schemas/<файл>.yml")?;
    if files.is_empty() {
        return Err("не заданы файлы для проверки".into());
    }

    let schema_text = std::fs::read_to_string(Path::new(schema_path))
        .map_err(|e| format!("не удалось прочитать схему {schema_path}: {e}"))?;
    let schema = Schema::parse(&schema_text)
        .map_err(|e| format!("схема {schema_path}: {e}"))?;

    let mut failed = false;
    for file in files {
        let path = Path::new(file);
        if !path.exists() {
            // Файл мог быть удалён в diff — проверять нечего.
            eprintln!("mini-validate-yamale: {file} не существует, пропущен");
            continue;
        }
        let text = std::fs::read_to_string(path)
            .map_err(|e| format!("{file}: не удалось прочитать: {e}"))?;
        // pyyaml переваривает BOM и CRLF — serde_yaml спотыкается о BOM.
        let text = text.strip_prefix('\u{feff}').unwrap_or(&text);
        let docs: Vec<Value> = serde_yaml::Deserializer::from_str(&text)
            .map(|doc| Value::deserialize(doc))
            .collect::<Result<_, _>>()
            .map_err(|e| format!("{file}: ошибка разбора YAML: {e}"))?;
        if docs.is_empty() {
            println!("{file}: пустой YAML");
            failed = true;
            continue;
        }

        let mut errors: Vec<(String, String)> = Vec::new();
        for (index, doc) in docs.iter().enumerate() {
            let root = if docs.len() == 1 {
                String::new()
            } else {
                format!("[doc {index}]")
            };
            schema.validate(doc, &root, &mut errors);
        }
        for (node_path, message) in &errors {
            println!("{file}: {node_path}: {message}");
        }
        failed |= !errors.is_empty();
    }

    Ok(failed)
}

/// Схема yamale: корневой узел + набор include-ов из последующих документов.
struct Schema {
    root: VNode,
    includes: HashMap<String, VNode>,
}

impl Schema {
    fn parse(text: &str) -> Result<Schema, String> {
        let docs: Vec<Value> = serde_yaml::Deserializer::from_str(text)
            .map(|doc| Value::deserialize(doc))
            .collect::<Result<_, _>>()
            .map_err(|e| format!("ошибка разбора YAML схемы: {e}"))?;
        if docs.is_empty() {
            return Err("схема пуста".into());
        }

        let mut includes = HashMap::new();
        for doc in docs.iter().skip(1) {
            let Y::Mapping(mapping) = doc else {
                return Err("include-документ схемы должен быть отображением".into());
            };
            for (name, body) in mapping {
                let name = as_string(name).ok_or("имя include должно быть строкой")?;
                let node = Self::parse_node(body)?;
                includes.insert(name, node);
            }
        }

        let root = Self::parse_node(&docs[0])?;
        Ok(Schema { root, includes })
    }

    /// Узел схемы: либо строка-валидатор ("list(int(), min=1)"), либо
    /// вложенное отображение ключей-валидаторов.
    fn parse_node(value: &Value) -> Result<VNode, String> {
        match value {
            Y::Mapping(_) => {
                let fields = Self::parse_fields(value)?;
                Ok(VNode {
                    kind: Kind::Fields(fields),
                    required: true,
                    min: None,
                    max: None,
                })
            }
            other => Self::parse_validator_string(&as_string(other).ok_or(
                "узел схемы должен быть строкой-валидатором или отображением",
            )?),
        }
    }

    fn parse_fields(mapping: &Value) -> Result<HashMap<String, VNode>, String> {
        let Y::Mapping(fields) = mapping else {
            return Err("ожидалось отображение".into());
        };
        let mut result = HashMap::new();
        for (key, node) in fields {
            let key = as_string(key).ok_or("ключ схемы должен быть строкой")?;
            result.insert(key, Self::parse_node(node)?);
        }
        Ok(result)
    }

    /// Разбор строки-валидатора: имя(аргументы, kw=знач).
    fn parse_validator_string(text: &str) -> Result<VNode, String> {
        let text = text.trim();
        let (name, args_text) = match (text.find('('), text.rfind(')')) {
            (Some(open), Some(close)) if close == text.len() - 1 && open > 0 => {
                (&text[..open], &text[open + 1..close])
            }
            _ => return Err(format!("не похоже на валидатор yamale: \"{text}\"")),
        };
        let name = name.trim();

        // Верхнеуровневое разбиение аргументов по запятым (с учётом кавычек и скобок).
        let mut args: Vec<String> = Vec::new();
        let mut kwargs: HashMap<String, String> = HashMap::new();
        let mut depth = 0usize;
        let mut in_quotes: Option<char> = None;
        let mut current = String::new();
        for ch in args_text.chars() {
            match (in_quotes, ch) {
                (None, '"' | '\'') => {
                    in_quotes = Some(ch);
                    current.push(ch);
                }
                (Some(q), ch) if ch == q => {
                    in_quotes = None;
                    current.push(ch);
                }
                (None, '(') => {
                    depth += 1;
                    current.push(ch);
                }
                (None, ')') => {
                    depth = depth.saturating_sub(1);
                    current.push(ch);
                }
                (None, ',') if depth == 0 => {
                    push_arg(&mut args, &mut kwargs, &current);
                    current.clear();
                }
                _ => current.push(ch),
            }
        }
        push_arg(&mut args, &mut kwargs, &current);

        let required = kwargs
            .get("required")
            .map(|v| v == "True" || v == "true")
            .unwrap_or(true);
        let min = kwargs.get("min").and_then(|v| v.parse().ok());
        let max = kwargs.get("max").and_then(|v| v.parse().ok());

        let kind = match name {
            "int" => Kind::Int,
            "str" | "string" => Kind::Str,
            "bool" => Kind::Bool,
            "float" | "num" | "number" => Kind::Float,
            "any" => Kind::Any,
            "include" => {
                let target = args
                    .first()
                    .ok_or("include() требует имя включения")?
                    .trim()
                    .trim_matches('\'')
                    .trim_matches('"')
                    .to_string();
                Kind::Include(target)
            }
            "enum" => {
                if args.is_empty() {
                    return Err("enum() требует хотя бы одно значение".into());
                }
                Kind::Enum(args.iter().map(|a| yaml_scalar(a)).collect())
            }
            "list" => {
                let inner = args.first().ok_or("list() требует валидатор элемента")?;
                Kind::List(Box::new(Self::parse_validator_string(inner)?))
            }
            "map" => {
                let value = args.first().ok_or("map() требует валидатор значения")?;
                let key = kwargs
                    .get("key")
                    .ok_or("map() требует key=<валидатор>")?;
                Kind::Map {
                    value: Box::new(Self::parse_validator_string(value)?),
                    key: Box::new(Self::parse_validator_string(key)?),
                }
            }
            "comp" => Kind::Custom(CustomKind::Comp),
            "license" => Kind::Custom(CustomKind::License),
            "url" => Kind::Custom(CustomKind::Url),
            other => {
                return Err(format!(
                    "неизвестный валидатор \"{other}()\" — схема устарела относительно инструмента"
                ))
            }
        };

        Ok(VNode {
            kind,
            required,
            min,
            max,
        })
    }

    fn validate(&self, data: &Value, path: &str, errors: &mut Vec<(String, String)>) {
        Self::validate_node(data, &self.root, path, &self.includes, errors, 0);
    }

    fn validate_node(
        data: &Value,
        node: &VNode,
        path: &str,
        includes: &HashMap<String, VNode>,
        errors: &mut Vec<(String, String)>,
        depth: usize,
    ) {
        if depth > 128 {
            errors.push((path.into(), "слишком глубокая вложенность схемы".into()));
            return;
        }

        match &node.kind {
            Kind::Fields(fields) => {
                let Y::Mapping(mapping) = data else {
                    errors.push((path.into(), type_message(data, "map")));
                    return;
                };
                // Лишние ключи — ошибка (как в yamale).
                for key in mapping.keys() {
                    let key_str = as_string(key);
                    let known = key_str.is_some_and(|k| fields.contains_key(&k));
                    if !known {
                        errors.push((
                            format!("{path}.{}", describe(key)),
                            "unexpected key".into(),
                        ));
                    }
                }
                for (key, subnode) in fields {
                    match mapping.get(Value::String(key.clone())) {
                        Some(value) => {
                            // yamale-семантика: явный null у необязательного поля
                            // равен отсутствию ключа (forkId: null в мапах).
                            if value.is_null() && !subnode.required {
                                continue;
                            }
                            Self::validate_node(
                                value,
                                subnode,
                                &format!("{path}.{key}"),
                                includes,
                                errors,
                                depth + 1,
                            );
                        }
                        None => {
                            if subnode.required {
                                errors.push((
                                    format!("{path}.{key}"),
                                    format!("required key '{key}' not found"),
                                ));
                            }
                        }
                    }
                }
            }
            Kind::List(item) => {
                let Y::Sequence(array) = data else {
                    errors.push((path.into(), type_message(data, "list")));
                    return;
                };
                if let Some(min) = node.min {
                    if array.len() < min {
                        errors.push((
                            path.into(),
                            format!("list length ({}) is less than min ({min})", array.len()),
                        ));
                    }
                }
                if let Some(max) = node.max {
                    if array.len() > max {
                        errors.push((
                            path.into(),
                            format!("list length ({}) is greater than max ({max})", array.len()),
                        ));
                    }
                }
                for (index, element) in array.iter().enumerate() {
                    Self::validate_node(
                        element,
                        item,
                        &format!("{path}[{index}]"),
                        includes,
                        errors,
                        depth + 1,
                    );
                }
            }
            Kind::Map { value, key } => {
                let Y::Mapping(mapping) = data else {
                    errors.push((path.into(), type_message(data, "map")));
                    return;
                };
                if let Some(min) = node.min {
                    if mapping.len() < min {
                        errors.push((
                            path.into(),
                            format!("map length ({}) is less than min ({min})", mapping.len()),
                        ));
                    }
                }
                if let Some(max) = node.max {
                    if mapping.len() > max {
                        errors.push((
                            path.into(),
                            format!("map length ({}) is greater than max ({max})", mapping.len()),
                        ));
                    }
                }
                for (data_key, data_value) in mapping {
                    Self::validate_node(
                        data_key,
                        key,
                        &format!("{path}.key({})", describe(data_key)),
                        includes,
                        errors,
                        depth + 1,
                    );
                    Self::validate_node(
                        data_value,
                        value,
                        &format!("{path}.{}", describe(data_key)),
                        includes,
                        errors,
                        depth + 1,
                    );
                }
            }
            Kind::Include(name) => match includes.get(name) {
                Some(target) => {
                    Self::validate_node(data, target, path, includes, errors, depth + 1);
                }
                None => errors.push((path.into(), format!("include \"{name}\" не найден"))),
            },
            Kind::Int => {
                // python-семантика yamale: bool — подкласс int.
                if !matches!(data, Y::Number(n) if n.is_i64() || n.is_u64()) && !matches!(data, Y::Bool(_)) {
                    errors.push((path.into(), type_message(data, "int")));
                }
            }
            Kind::Str => {
                if !matches!(data, Y::String(_)) {
                    errors.push((path.into(), type_message(data, "str")));
                }
            }
            Kind::Bool => {
                if !matches!(data, Y::Bool(_)) {
                    errors.push((path.into(), type_message(data, "bool")));
                }
            }
            Kind::Float => {
                // python float() принимает и int.
                if !matches!(data, Y::Number(_)) {
                    errors.push((path.into(), type_message(data, "float")));
                }
            }
            Kind::Any => {}
            Kind::Enum(values) => {
                if !values.iter().any(|allowed| yaml_equal(allowed, data)) {
                    errors.push((
                        path.into(),
                        format!("{} is not one of {}", describe(data), enum_list(values)),
                    ));
                }
            }
            Kind::Custom(kind) => match kind {
                CustomKind::Comp => {
                    let Y::Mapping(mapping) = data else {
                        errors.push((path.into(), "is not a component (map expected)".into()));
                        return;
                    };
                    if !mapping.contains_key(Value::String("type".into())) {
                        errors.push((path.into(), "component без поля 'type'".into()));
                    }
                }
                CustomKind::License => {
                    let Some(license) = as_string(data) else {
                        errors.push((path.into(), type_message(data, "license (str)")));
                        return;
                    };
                    if !RGA_LICENSES.contains(&license.as_str()) {
                        errors.push((
                            path.into(),
                            format!("\"{license}\" is not a valid license"),
                        ));
                    }
                }
                CustomKind::Url => {
                    // Source обязателен, но может быть "NA" — ссылки может не быть.
                    let Some(url) = as_string(data) else {
                        errors.push((path.into(), type_message(data, "url (str)")));
                        return;
                    };
                    if url == "NA" {
                        return;
                    }
                    if !is_url(&url) {
                        errors.push((path.into(), format!("\"{url}\" is not a valid url")));
                    }
                }
            },
        }
    }
}

fn push_arg(args: &mut Vec<String>, kwargs: &mut HashMap<String, String>, raw: &str) {
    let raw = raw.trim();
    if raw.is_empty() {
        return;
    }
    if let Some((key, value)) = raw.split_once('=') {
        kwargs.insert(key.trim().to_string(), value.trim().to_string());
    } else {
        args.push(raw.to_string());
    }
}

/// Литерал аргумента -> Value (для enum()).
fn yaml_scalar(text: &str) -> Value {
    let trimmed = text.trim();
    if (trimmed.starts_with('"') && trimmed.ends_with('"') && trimmed.len() >= 2)
        || (trimmed.starts_with('\'') && trimmed.ends_with('\'') && trimmed.len() >= 2)
    {
        return Value::String(trimmed[1..trimmed.len() - 1].to_string());
    }
    if let Ok(int) = trimmed.parse::<i64>() {
        return Value::Number(int.into());
    }
    if trimmed == "true" {
        return Value::Bool(true);
    }
    if trimmed == "false" {
        return Value::Bool(false);
    }
    Value::String(trimmed.to_string())
}

fn as_string(value: &Value) -> Option<String> {
    match value {
        Y::String(s) => Some(s.clone()),
        _ => None,
    }
}

fn yaml_equal(a: &Value, b: &Value) -> bool {
    a == b
}

fn describe(value: &Value) -> String {
    match value {
        Y::String(s) => s.clone(),
        Y::Number(n) => n.to_string(),
        Y::Bool(b) => b.to_string(),
        Y::Null => "null".into(),
        other => format!("{other:?}"),
    }
}

fn enum_list(values: &[Value]) -> String {
    let joined: Vec<String> = values.iter().map(describe).collect();
    joined.join(", ")
}

fn type_message(data: &Value, expected: &str) -> String {
    format!("{} is not a {}", describe(data), expected)
}

/// Приблизительный аналог validators.url: схема://непустой-хост...
fn is_url(url: &str) -> bool {
    if url.contains(char::is_whitespace) {
        return false;
    }
    let Some((scheme, rest)) = url.split_once("://") else {
        return false;
    };
    if scheme.is_empty() || !scheme.chars().next().is_some_and(|c| c.is_ascii_alphabetic()) {
        return false;
    }
    if !scheme.chars().all(|c| c.is_ascii_alphanumeric() || c == '+' || c == '-' || c == '.') {
        return false;
    }
    let host = rest.split(['/', '?', '#']).next().unwrap_or("");
    !host.is_empty()
}
