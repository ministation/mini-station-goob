// SPDX-FileCopyrightText: 2024 Piras314 <92357316+Piras314@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// From https://github.com/DeltaV-Station/Delta-v/
// Dependencies
const fs = require("fs");
const path = require("path");
const yaml = require("js-yaml");
const axios = require("axios");

const MINI_CHANGELOG_DIR = process.env.MINI_CHANGELOG_DIR ?? "Resources/Changelog/ChangelogMini.yml";
const MINI_PREFIX_REGEX = /^Mini\s*[—\-–:]\s*/i;
const MINI_HEADER = "Name: Mini\nOrder: -2\nEntries:\n";

// DRY_RUN=1: ничего не пишем на диск, только печатаем предлагаемую запись и diff.
const DRY_RUN = ["1", "true", "yes", "on"].includes(String(process.env.DRY_RUN ?? "").trim().toLowerCase());

// Use GitHub token if available. Без токена запросы идут в публичный API
// (лимит 60 запросов в час), что для одного PR вполне достаточно.
if (process.env.GITHUB_TOKEN) {
    axios.defaults.headers.common["Authorization"] = `Bearer ${process.env.GITHUB_TOKEN}`;
} else {
    console.log("GITHUB_TOKEN не задан, используем публичный GitHub API");
}

// Regexes
const HeaderRegex = /^\s*(?::cl:|🆑) *([a-z0-9_\- ,]+)?\s+/im; // :cl: or 🆑 [0] followed by optional author name [1]
const EntryRegex = /^ *[*-]? *(add|remove|tweak|fix): *([^\n\r]+)\r?$/img; // * or - followed by change type [0] and change message [1]
const CommentRegex = /<!--.*?-->/gs; // HTML comments

// Main function
async function main() {
    const repository = process.env.GITHUB_REPOSITORY;
    const prNumber = process.env.PR_NUMBER;

    if (!repository) {
        throw new Error("Не задан GITHUB_REPOSITORY (ожидается owner/repo)");
    }
    if (!prNumber) {
        throw new Error("Не задан PR_NUMBER");
    }

    if (DRY_RUN) {
        console.log("DRY_RUN=1: файл чейнджлога не изменяется");
    }

    // Get PR details
    let pr;
    try {
        pr = await axios.get(`https://api.github.com/repos/${repository}/pulls/${prNumber}`);
    } catch (err) {
        const status = err.response ? ` (HTTP ${err.response.status})` : "";
        throw new Error(`Не удалось получить PR #${prNumber}${status}: ${err.message}`);
    }

    const { merged_at, body, user } = pr.data;

    // Body can be null or empty: тогда :cl: просто нет, это не ошибка.
    const commentlessBody = (body || "").replace(CommentRegex, "");
    if (!commentlessBody.trim()) {
        console.log("Описание PR пустое, чейнджлог не обновляем");
        return;
    }

    // Get author
    const headerMatch = HeaderRegex.exec(commentlessBody);
    if (!headerMatch) {
        console.log("No changelog entry found, skipping");
        return;
    }

    let author = headerMatch[1];
    if (!author) {
        console.log("No author found, setting it to author of the PR\n");
        author = (user && user.login) || "Mini Station";
    }

    // Get all changes from the body
    const { mini } = getChanges(commentlessBody);

    if (mini.length === 0) {
        console.log("No valid changes found, skipping");
        return;
    }

    if (!merged_at) {
        console.log("Pull request was not merged, skipping");
        return;
    }

    // Time is something like 2021-08-29T20:00:00Z
    // Time should be something like 2023-02-18T00:00:00.0000000+00:00
    const time = String(merged_at).replace(/(\.\d+)?Z$/, ".0000000+00:00");

    const entry = {
        author: author,
        changes: mini,
        id: getHighestCLNumber(MINI_CHANGELOG_DIR) + 1,
        time: time,
    };

    console.log("mini entry:", JSON.stringify(entry, null, 2));

    appendChangelogEntry(MINI_CHANGELOG_DIR, entry, MINI_HEADER);

    if (DRY_RUN) {
        console.log(`DRY_RUN=1: запись для PR #${prNumber} не записана в ${MINI_CHANGELOG_DIR}`);
    } else {
        console.log(`Changelog updated with changes from PR #${prNumber}`);
    }
}

// Code chunking

// Get all changes from the PR body. Entries go to the Mini changelog by default:
// players only see that one, while Gooblog is an archive filled by upstream merges
// and batch runs. Legacy "Mini" prefixes are stripped from the message.
function getChanges(body) {
    const matches = [];
    const mini = [];

    for (const match of body.matchAll(EntryRegex)) {
        matches.push([match[1], match[2]]);
    }

    // Check change types and construct changelog entry
    matches.forEach((entry) => {
        let type;

        switch (entry[0].toLowerCase()) {
            case "add":
                type = "Add";
                break;
            case "remove":
                type = "Remove";
                break;
            case "tweak":
                type = "Tweak";
                break;
            case "fix":
                type = "Fix";
                break;
            default:
                break;
        }

        if (!type) {
            return;
        }

        mini.push({
            type: type,
            message: entry[1].trim().replace(MINI_PREFIX_REGEX, ""),
        });
    });

    return { mini };
}

// Путь к файлу считается от cwd (workflow запускает скрипт из Tools/changelog),
// как и раньше: два уровня вверх до корня репозитория.
function resolveChangelogPath(filePath) {
    return path.resolve(process.cwd(), "../../", filePath);
}

function readChangelog(filePath) {
    const fullPath = resolveChangelogPath(filePath);
    if (!fs.existsSync(fullPath)) {
        return { fullPath, exists: false, content: "", entries: [] };
    }

    const content = fs.readFileSync(fullPath, "utf8");
    const data = yaml.load(content);
    // Пустой файл, файл только с шапкой или шапка без Entries: это не ошибка.
    const entries = data && Array.isArray(data.Entries) ? data.Entries : [];
    return { fullPath, exists: true, content, entries };
}

// Get the highest changelog number from the changelogs file
function getHighestCLNumber(filePath) {
    const { entries } = readChangelog(filePath);
    const clNumbers = entries
        .map((entry) => entry && entry.id)
        .filter((id) => Number.isFinite(id));

    return Math.max(...clNumbers, 0);
}

function appendChangelogEntry(filePath, entry, header) {
    const { fullPath, exists, content, entries } = readChangelog(filePath);

    const nextEntries = [...entries, entry];
    // lineWidth: -1 сохраняет длинные сообщения одной строкой: так файл
    // ChangelogMini.yml не переформатируется целиком, и в diff попадает
    // только новая запись.
    const nextContent = header + yaml.dump(nextEntries, { indent: 2, lineWidth: -1 }).replace(/^---/, "");

    if (DRY_RUN) {
        printDiff(filePath, exists ? content : null, nextContent);
        return;
    }

    fs.writeFileSync(fullPath, nextContent);
}

// Печатает простой построчный diff: общий префикс и суффикс не показываются,
// в середине старые строки с "-", новые с "+". Для дописывания записи в конец
// этого достаточно и не тянет лишних зависимостей.
function printDiff(filePath, oldContent, newContent) {
    if (oldContent === newContent) {
        console.log(`Diff ${filePath}: изменений нет`);
        return;
    }

    console.log(`Diff ${filePath} (старое с "-", новое с "+"):`);

    if (oldContent === null) {
        console.log(`(файла не было, будет создан)`);
        newContent.split("\n").forEach((line) => console.log(`+${line}`));
        return;
    }

    const oldLines = oldContent.split("\n");
    const newLines = newContent.split("\n");

    let start = 0;
    while (start < oldLines.length && start < newLines.length && oldLines[start] === newLines[start]) {
        start++;
    }

    let end = 0;
    while (
        end < oldLines.length - start &&
        end < newLines.length - start &&
        oldLines[oldLines.length - 1 - end] === newLines[newLines.length - 1 - end]
    ) {
        end++;
    }

    const removed = oldLines.slice(start, oldLines.length - end);
    const added = newLines.slice(start, newLines.length - end);

    removed.forEach((line) => console.log(`-${line}`));
    added.forEach((line) => console.log(`+${line}`));
}

// Run main
main().catch((err) => {
    console.error("Ошибка:", err.message || err);
    process.exitCode = 1;
});
