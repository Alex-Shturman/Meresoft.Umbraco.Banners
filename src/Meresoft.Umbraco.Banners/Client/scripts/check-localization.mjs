/* global console, process */
// A HEURISTIC check that UI text is not written as a literal in the client source instead of the localization files
// (FR-021). It reads the source as text with patterns, it does not parse TypeScript, so it can miss text built in
// unusual ways and, rarely, flag something that is not UI text. It catches: text between tags of html`...` templates,
// literal label, placeholder, title, headline, description, aria-label and alt attributes, and literal label, title,
// headline, description and message properties (manifests, notifications, modals). It also checks that every package
// key it can see ("meresoftBanners_x" in a string, a "#meresoftBanners_x" reference) exists in lang/en.ts, and that
// localize.term("...") is not called with literal words. Keys built at run time are not seen.
// Usage: node scripts/check-localization.mjs [source folder]   (default: src)
// Compliant: this.localize.term("key"), <umb-localize key="key">, "#key" in manifests. Files under lang/ are skipped.
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const textAttributes = 'label|placeholder|title|headline|description|aria-label|alt|ariaLabel';
const textProperties = 'label|placeholder|title|headline|description|ariaLabel|message';
const keyPrefix = 'meresoftBanners_';
const keyPattern = /["'`]#?(meresoftBanners_[A-Za-z0-9]+)["'`]/g;
const termLiteralPattern = /\.(?:term|string)\(\s*(["'`])([^"'`]*)\1/g;
const attributePattern = new RegExp(`(?:^|[\\s.?])(${textAttributes})\\s*=\\s*(["'])([^"']*)\\2`, 'g');
const propertyPattern = new RegExp(`\\b(${textProperties})\\s*:\\s*(["'\`])([^"'\`]*)\\2`, 'g');

const hasLetters = (text) => /\p{L}/u.test(text);
const lineOf = (source, index) => source.slice(0, index).split('\n').length;

// Replaces every ${...} expression (with nesting) by blanks of the same length, so offsets stay valid.
function blankExpressions(text) {
    let out = '';
    for (let i = 0; i < text.length; i++) {
        if (text[i] === '$' && text[i + 1] === '{') {
            let depth = 0;
            let j = i + 1;
            for (; j < text.length; j++) {
                if (text[j] === '{') depth++;
                else if (text[j] === '}' && --depth === 0) break;
            }
            for (let k = i; k <= j && k < text.length; k++) out += text[k] === '\n' ? '\n' : ' ';
            i = j;
        } else {
            out += text[i];
        }
    }
    return out;
}

// Finds the bodies of html`...` templates, skipping nested templates inside ${...}.
function htmlTemplates(source) {
    const found = [];
    const start = /\bhtml\s*`/g;
    let match;
    while ((match = start.exec(source))) {
        let i = match.index + match[0].length;
        const bodyStart = i;
        let depth = 0;
        for (; i < source.length; i++) {
            const c = source[i];
            if (c === '\\') i++;
            else if (depth === 0 && c === '`') break;
            else if (c === '$' && source[i + 1] === '{') { depth++; i++; }
            else if (depth > 0 && c === '{') depth++;
            else if (depth > 0 && c === '}') depth--;
        }
        found.push({ index: bodyStart, body: source.slice(bodyStart, i) });
        start.lastIndex = bodyStart;
    }
    return found;
}

export function checkSource(source, file = 'source.ts', knownKeys = undefined) {
    const problems = [];
    for (const { index, body } of htmlTemplates(source)) {
        const flat = blankExpressions(body);
        // Text nodes: everything outside tags. Attribute values stay inside the tag and are checked below.
        const textNode = /(^|>)([^<>]*)(?=<|$)/g;
        let m;
        while ((m = textNode.exec(flat))) {
            if (hasLetters(m[2])) {
                const at = m.index + m[1].length + m[2].search(/\S/);
                problems.push({ file, line: lineOf(source, index + at), text: m[2].trim() });
            }
            if (m[0] === '') textNode.lastIndex++;
        }
        let a;
        attributePattern.lastIndex = 0;
        while ((a = attributePattern.exec(flat))) {
            if (hasLetters(a[3]) && !a[3].startsWith('#')) {
                problems.push({ file, line: lineOf(source, index + a.index), text: `${a[1]}="${a[3]}"` });
            }
        }
    }
    let p;
    propertyPattern.lastIndex = 0;
    while ((p = propertyPattern.exec(source))) {
        if (hasLetters(p[3]) && !p[3].startsWith('#')) {
            problems.push({ file, line: lineOf(source, p.index), text: `${p[1]}: ${p[2]}${p[3]}${p[2]}` });
        }
    }
    if (knownKeys) {
        let k;
        keyPattern.lastIndex = 0;
        while ((k = keyPattern.exec(source))) {
            if (!knownKeys.has(k[1].slice(keyPrefix.length))) {
                problems.push({ file, line: lineOf(source, k.index), text: `unknown localization key ${k[1]}` });
            }
        }
    }
    let t;
    termLiteralPattern.lastIndex = 0;
    while ((t = termLiteralPattern.exec(source))) {
        if (/\s/.test(t[2].trim())) {
            problems.push({ file, line: lineOf(source, t.index), text: `localize call with literal text ${JSON.stringify(t[2])}` });
        }
    }
    return problems;
}

// The keys of src/lang/en.ts: every property name in the dictionary (the section name is the prefix of the full key).
export function knownKeysOf(dictionarySource) {
    return new Set([...dictionarySource.matchAll(/^\s*([A-Za-z0-9]+)\s*:/gm)].map((m) => m[1]));
}

function* sourceFiles(dir) {
    for (const name of readdirSync(dir)) {
        const path = join(dir, name);
        if (statSync(path).isDirectory()) {
            if (name !== 'lang' && name !== 'api' && name !== 'node_modules') yield* sourceFiles(path);
        } else if (name.endsWith('.ts') && !name.endsWith('.test.ts') && !name.endsWith('.d.ts')) {
            yield path;
        }
    }
}

export function checkFolder(folder) {
    let knownKeys;
    try {
        knownKeys = knownKeysOf(readFileSync(join(folder, 'lang', 'en.ts'), 'utf8'));
    } catch {
        knownKeys = undefined; // a folder without a dictionary (a test fixture) has no keys to check against
    }

    return [...sourceFiles(folder)].flatMap((f) => checkSource(readFileSync(f, 'utf8'), relative(process.cwd(), f), knownKeys));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    const folder = resolve(process.argv[2] ?? join(dirname(fileURLToPath(import.meta.url)), '../src'));
    const problems = checkFolder(folder);
    for (const p of problems) console.error(`${p.file}:${p.line}: ${p.text.startsWith('unknown') || p.text.startsWith('localize') ? p.text : 'literal UI text ' + JSON.stringify(p.text)}`);
    if (problems.length > 0) {
        console.error(`${problems.length} localization problem(s) found. Move texts to src/lang, use localize and existing keys (FR-021).`);
        process.exit(1);
    }
    console.log('No literal UI text found and all keys exist.');
}
