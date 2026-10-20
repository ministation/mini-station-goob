// SPDX-FileCopyrightText: 2024 Piras314 <92357316+Piras314@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// From https://github.com/DeltaV-Station/Delta-v/
// Dependencies
const fs = require("fs");
const yaml = require("js-yaml");
const axios = require("axios");

const MINI_CHANGELOG_DIR = process.env.MINI_CHANGELOG_DIR ?? "Resources/Changelog/ChangelogMini.yml";
const MINI_PREFIX_REGEX = /^Mini\s*[—\-–:]\s*/i;

// Use GitHub token if available
if (process.env.GITHUB_TOKEN) axios.defaults.headers.common["Authorization"] = `Bearer ${process.env.GITHUB_TOKEN}`;

// Regexes
const HeaderRegex = /^\s*(?::cl:|🆑) *([a-z0-9_\- ,]+)?\s+/im; // :cl: or 🆑 [0] followed by optional author name [1]
const EntryRegex = /^ *[*-]? *(add|remove|tweak|fix): *([^\n\r]+)\r?$/img; // * or - followed by change type [0] and change message [1]
const CommentRegex = /<!--.*?-->/gs; // HTML comments

// Main function
async function main() {
    // Get PR details
    const pr = await axios.get(`https://api.github.com/repos/${process.env.GITHUB_REPOSITORY}/pulls/${process.env.PR_NUMBER}`);
    const { merged_at, body, user } = pr.data;

    // Remove comments from the body
    const commentlessBody = (body || '').replace(CommentRegex, '');

    // Get author
    const headerMatch = HeaderRegex.exec(commentlessBody);
    if (!headerMatch) {
        console.log("No changelog entry found, skipping");
        return;
    }

    let author = headerMatch[1];
    if (!author) {
        console.log("No author found, setting it to author of the PR\n");
        author = user.login;
    }

    // Get all changes from the body
    const { mini, goob } = getChanges(commentlessBody);

    if (mini.length === 0 && goob.length === 0) {
        console.log("No valid changes found, skipping");
        return;
    }

    // Time is something like 2021-08-29T20:00:00Z
    // Time should be something like 2023-02-18T00:00:00.0000000+00:00
    let time = merged_at;
    if (time)
    {
        time = time.replace("z", ".0000000+00:00").replace("Z", ".0000000+00:00");
    }
    else
    {
        console.log("Pull request was not merged, skipping");
        return;
    }

    if (mini.length > 0) {
        const miniEntry = {
            author: author,
            changes: mini,
            id: getHighestCLNumber(MINI_CHANGELOG_DIR) + 1,
            time: time,
        };

        console.log("mini entry:", miniEntry);
        appendChangelogEntry(MINI_CHANGELOG_DIR, miniEntry, "Name: Mini\nOrder: -2\nEntries:\n");
    }

    if (goob.length > 0) {
        const goobEntry = {
            author: author,
            changes: goob,
            id: getHighestCLNumber(process.env.CHANGELOG_DIR) + 1,
            time: time,
        };

        console.log("goob entry:", goobEntry);
        appendChangelogEntry(process.env.CHANGELOG_DIR, goobEntry, "Name: Gooblog\nOrder: -1\nEntries:\n");
    }

    console.log(`Changelog updated with changes from PR #${process.env.PR_NUMBER}`);
}


// Code chunking

// Get all changes from the PR body, split by Mini prefix
function getChanges(body) {
    const matches = [];
    const mini = [];
    const goob = [];

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

        const message = entry[1].trim();
        const change = {
            type: type,
            message: MINI_PREFIX_REGEX.test(message)
                ? message.replace(MINI_PREFIX_REGEX, "")
                : message,
        };

        if (MINI_PREFIX_REGEX.test(message)) {
            mini.push(change);
        } else {
            goob.push(change);
        }
    });

    return { mini, goob };
}

// Get the highest changelog number from the changelogs file
function getHighestCLNumber(path) {
    const fullPath = `../../${path}`;

    if (!fs.existsSync(fullPath)) {
        return 0;
    }

    const file = fs.readFileSync(fullPath, "utf8");
    const data = yaml.load(file);
    const entries = data && data.Entries ? Array.from(data.Entries) : [];
    const clNumbers = entries.map((entry) => entry.id);

    return Math.max(...clNumbers, 0);
}

function appendChangelogEntry(path, entry, header) {
    let data = { Entries: [] };
    const fullPath = `../../${path}`;

    if (fs.existsSync(fullPath)) {
        const file = fs.readFileSync(fullPath, "utf8");
        data = yaml.load(file);
    }

    data.Entries.push(entry);

    fs.writeFileSync(
        fullPath,
        header + yaml.dump(data.Entries, { indent: 2 }).replace(/^---/, "")
    );
}

// Run main
main();
