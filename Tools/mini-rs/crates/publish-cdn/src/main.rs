// SPDX-FileCopyrightText: 2026 Mini Station contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

// Порт Tools/publish_multi_request.py: публикация собранных пакетов в
// Robust.CDN. Протокол и порядок те же:
//   publish/start (409 "Version already exists" = уже опубликован, no-op)
//   → publish/file для каждого zip из release/ (строго по одному: CDN держит
//     транзакцию SQLite, параллельные заливки ловят "database is locked")
//   → publish/finish.
// Версия движка: Robust.Engine.Version.props, иначе git describe в сабмодуле.

use std::io::Read;
use std::path::Path;
use std::process::ExitCode;
use std::time::Duration;

use serde_json::json;

const RELEASE_DIR: &str = "release";
const UPLOAD_TIMEOUT: Duration = Duration::from_secs(300);

struct Cdn {
    base_url: String,
    token: String,
}

fn main() -> ExitCode {
    match run() {
        Ok(()) => {
            println!("SUCCESS!");
            ExitCode::SUCCESS
        }
        Err(message) => {
            eprintln!("mini-publish: {message}");
            ExitCode::FAILURE
        }
    }
}

fn run() -> Result<(), String> {
    let token = env_trim("PUBLISH_TOKEN")?;
    let version = env_trim("GITHUB_SHA")?;
    let fork_id = env_trim("FORK_ID")?;
    let base = std::env::var("ROBUST_CDN_URL").unwrap_or_default();
    let mut base = if base.is_empty() {
        "https://cdn.ministation.ru/".to_string()
    } else {
        base
    };
    if !base.ends_with('/') {
        base.push('/');
    }
    let cdn = Cdn {
        base_url: base,
        token,
    };

    let agent = ureq::AgentBuilder::new()
        .timeout_read(UPLOAD_TIMEOUT)
        .timeout_write(UPLOAD_TIMEOUT)
        .build();

    let engine_version = engine_version()?;

    println!(
        "Starting publish on Robust.Cdn for version {version} (engine {engine_version})"
    );
    println!("CDN: {}fork/{fork_id}/", cdn.base_url);

    // publish/start
    let start = json!({ "version": version, "engineVersion": engine_version });
    let response = agent
        .post(&format!(
            "{}fork/{fork_id}/publish/start",
            cdn.base_url
        ))
        .set("Authorization", &format!("Bearer {}", cdn.token))
        .set("Content-Type", "application/json")
        .send_json(start)
        .map_err(|e| http_error(e, "publish/start"))?;
    let status = response.status();
    // CDN отвечает 409, когда этот коммит уже полностью опубликован: no-op.
    if status == 409 && response.into_string().unwrap_or_default().contains("Version already exists") {
        println!("Version {version} is already published, nothing to do.");
        return Ok(());
    }
    check_status(status, "publish/start")?;
    println!("Publish successfully started, adding files...");

    // publish/file — по одному, отсортировано по размеру по убыванию.
    let mut files: Vec<(String, u64)> = std::fs::read_dir(RELEASE_DIR)
        .map_err(|e| format!("не удалось открыть {RELEASE_DIR}: {e}"))?
        .flatten()
        .map(|entry| entry.path())
        .filter(|path| path.is_file())
        .map(|path| {
            let size = path.metadata().map(|m| m.len()).unwrap_or(0);
            (path, size)
        })
        .map(|(path, size)| (path.to_string_lossy().into_owned(), size))
        .collect();
    files.sort_by(|a, b| b.1.cmp(&a.1));
    println!("Publishing {} files in 1 parallel uploads...", files.len());

    for (file, _) in &files {
        let name = Path::new(file)
            .file_name()
            .map(|n| n.to_string_lossy().into_owned())
            .unwrap_or_else(|| file.clone());
        let file_handle = std::fs::File::open(file)
            .map_err(|e| format!("publish/file ({name}): {e}"))?;
        let response = agent
            .post(&format!("{}fork/{fork_id}/publish/file", cdn.base_url))
            .set("Authorization", &format!("Bearer {}", cdn.token))
            .set("Content-Type", "application/octet-stream")
            .set("Robust-Cdn-Publish-File", &name)
            .set("Robust-Cdn-Publish-Version", &version)
            .send(file_handle)
            .map_err(|e| http_error(e, &format!("publish/file ({name})")))?;
        check_status(response.status(), &format!("publish/file ({name})"))?;
        println!("Published {name}");
    }

    // publish/finish
    let finish = json!({ "version": version });
    let response = agent
        .post(&format!(
            "{}fork/{fork_id}/publish/finish",
            cdn.base_url
        ))
        .set("Authorization", &format!("Bearer {}", cdn.token))
        .set("Content-Type", "application/json")
        .send_json(finish)
        .map_err(|e| http_error(e, "publish/finish"))?;
    check_status(response.status(), "publish/finish")?;

    Ok(())
}

fn check_status(status: u16, step: &str) -> Result<(), String> {
    if (200..300).contains(&status) {
        return Ok(());
    }
    Err(format!("{step} failed: HTTP {status}"))
}

fn http_error(error: ureq::Error, step: &str) -> String {
    match error {
        ureq::Error::Status(code, response) => {
            let mut body = String::new();
            let _ = response.into_reader().take(800).read_to_string(&mut body);
            format!("{step} failed: HTTP {code}\nBody: {body}")
        }
        other => format!("{step} failed: {other}"),
    }
}

fn env_trim(name: &str) -> Result<String, String> {
    std::env::var(name)
        .map(|v| v.trim().to_string())
        .map_err(|_| format!("не задана переменная окружения {name}"))
}

/// Версия движка: <Version> из Robust.Engine.Version.props, иначе git describe.
fn engine_version() -> Result<String, String> {
    let props = Path::new("RobustToolbox")
        .join("MSBuild")
        .join("Robust.Engine.Version.props");
    if props.is_file() {
        if let Ok(text) = std::fs::read_to_string(&props) {
            if let Some(start) = text.find("<Version>") {
                let rest = &text[start + "<Version>".len()..];
                if let Some(end) = rest.find("</Version>") {
                    return Ok(rest[..end].trim().to_string());
                }
            }
        }
    }
    let output = std::process::Command::new("git")
        .args(["describe", "--tags", "--abbrev=0"])
        .current_dir("RobustToolbox")
        .output()
        .map_err(|e| format!("git describe: {e}"))?;
    if !output.status.success() {
        return Err(
            "Engine version: no Robust.Engine.Version.props and git describe failed (need tags or props file)."
                .into(),
        );
    }
    let tag = String::from_utf8_lossy(&output.stdout).trim().to_string();
    let Some(stripped) = tag.strip_prefix('v') else {
        return Err(format!("Unexpected engine tag (expected v-prefix): {tag:?}"));
    };
    Ok(stripped.to_string())
}
