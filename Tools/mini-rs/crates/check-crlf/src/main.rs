// SPDX-FileCopyrightText: 2026 Mini Station contributors
// SPDX-License-Identifier: MIT

// Порт Tools/check_crlf.py: падает, если в индексе git есть текстовые файлы
// с CRLF. Список файлов берётся тем же способом — `git grep --cached -Il ""`,
// то есть проверяется содержимое ИНДЕКСА, а не рабочего каталога.

use std::io::Write;
use std::process::ExitCode;

fn main() -> ExitCode {
    match run() {
        Ok(failed) => {
            let _ = std::io::stdout().flush();
            if failed {
                ExitCode::FAILURE
            } else {
                ExitCode::SUCCESS
            }
        }
        Err(message) => {
            eprintln!("mini-check-crlf: {message}");
            ExitCode::FAILURE
        }
    }
}

fn run() -> Result<bool, String> {
    let output = std::process::Command::new("git")
        .args(["grep", "--cached", "-Il", ""])
        .output()
        .map_err(|e| format!("не удалось запустить git grep: {e}"))?;

    // 0 — есть совпадения; 1 — их нет (пустой список файлов, это не ошибка).
    if !output.status.success() && output.status.code() != Some(1) {
        return Err(format!(
            "git grep завершился с кодом {}: {}",
            output.status,
            String::from_utf8_lossy(&output.stderr).trim()
        ));
    }

    let mut any_failed = false;
    let stdout = String::from_utf8_lossy(&output.stdout);
    for file in stdout.lines() {
        let file = file.trim();
        if file.is_empty() {
            continue;
        }

        if file_has_crlf(file).map_err(|e| format!("{file}: {e}"))? {
            println!(
                "::error file={file},title=File contains CRLF line endings::The file '{file}' was committed with CRLF new lines. Please make sure your git client is configured correctly and you are not uploading files directly to GitHub via the web interface."
            );
            any_failed = true;
        }
    }

    Ok(any_failed)
}

fn file_has_crlf(path: &str) -> std::io::Result<bool> {
    let bytes = std::fs::read(path)?;
    Ok(bytes.windows(2).any(|window| window == b"\r\n"))
}
