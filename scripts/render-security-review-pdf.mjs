import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const sourcePath = path.join(root, "SCA_SURGERY_SECURITY_REVIEW.md");
const htmlPath = path.join(root, "SmartDocScan_Security_Overview.html");
const pdfPath = path.join(root, "SmartDocScan_Security_Overview.pdf");

const markdown = fs.readFileSync(sourcePath, "utf8");
const html = renderDocument(markdown);
fs.writeFileSync(htmlPath, html, "utf8");

const browser = await chromium.launch({
  executablePath: findBrowserExecutable(),
});
const page = await browser.newPage();
await page.goto(`file:///${htmlPath.replaceAll("\\", "/")}`, { waitUntil: "load" });
await page.pdf({
  path: pdfPath,
  format: "Letter",
  printBackground: true,
  margin: {
    top: "0.45in",
    right: "0.55in",
    bottom: "0.55in",
    left: "0.55in",
  },
});
await browser.close();

console.log(pdfPath);

function renderDocument(text) {
  const lines = text.replace(/\r\n/g, "\n").split("\n");
  const title = lines.find((line) => line.startsWith("# "))?.replace(/^#\s+/, "") || "Security Overview";
  const updated = lines.find((line) => line.startsWith("Last updated:")) || "";
  const bodyLines = lines.slice(lines.findIndex((line) => line.startsWith("## ")));
  const body = renderMarkdown(bodyLines.join("\n"));
  const generatedOn = new Intl.DateTimeFormat("en-US", {
    dateStyle: "long",
    timeZone: "America/Los_Angeles",
  }).format(new Date());

  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>${escapeHtml(title)}</title>
  <style>
    :root {
      --ink: #172033;
      --muted: #5a6577;
      --line: #d9e0ea;
      --panel: #f6f8fb;
      --brand: #164e63;
      --accent: #0f766e;
    }

    @page {
      size: Letter;
      margin: 0.45in 0.55in 0.55in;
    }

    * {
      box-sizing: border-box;
    }

    body {
      margin: 0;
      color: var(--ink);
      font-family: "Aptos", "Segoe UI", Arial, sans-serif;
      font-size: 10.5pt;
      line-height: 1.48;
      background: white;
    }

    .cover {
      min-height: 8.65in;
      display: flex;
      flex-direction: column;
      border: 1px solid var(--line);
      position: relative;
      overflow: hidden;
      page-break-after: always;
    }

    .cover::before {
      content: "";
      position: absolute;
      inset: 0 0 auto 0;
      height: 0.18in;
      background: linear-gradient(90deg, var(--brand), var(--accent));
    }

    .cover-inner {
      padding: 0.72in 0.62in;
      flex: 1;
      display: flex;
      flex-direction: column;
    }

    .kicker {
      color: var(--accent);
      font-size: 9pt;
      font-weight: 700;
      letter-spacing: 0.11em;
      text-transform: uppercase;
      margin-bottom: 0.28in;
    }

    h1 {
      margin: 0;
      color: var(--brand);
      font-size: 30pt;
      line-height: 1.08;
      letter-spacing: 0;
      max-width: 6.5in;
    }

    .subtitle {
      margin-top: 0.22in;
      color: var(--muted);
      font-size: 13pt;
      max-width: 5.8in;
    }

    .meta-grid {
      margin-top: auto;
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.14in;
    }

    .meta-card {
      border: 1px solid var(--line);
      background: var(--panel);
      padding: 0.18in;
      min-height: 0.72in;
    }

    .meta-label {
      color: var(--muted);
      font-size: 8.4pt;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }

    .meta-value {
      margin-top: 0.06in;
      color: var(--ink);
      font-size: 10.2pt;
      font-weight: 600;
    }

    .content {
      counter-reset: section;
    }

    h2 {
      color: var(--brand);
      font-size: 15pt;
      line-height: 1.2;
      margin: 0.22in 0 0.08in;
      padding-top: 0.1in;
      border-top: 1px solid var(--line);
      break-after: avoid;
    }

    h2:first-child {
      margin-top: 0;
      border-top: 0;
      padding-top: 0;
    }

    p {
      margin: 0 0 0.08in;
    }

    ul {
      margin: 0.04in 0 0.12in 0.22in;
      padding: 0;
    }

    li {
      margin: 0.022in 0;
    }

    code {
      color: #0f3f46;
      background: #eef7f6;
      border: 1px solid #cfe4e1;
      padding: 0.01in 0.045in;
      border-radius: 3px;
      font-family: "Cascadia Mono", Consolas, monospace;
      font-size: 9pt;
    }

    .section {
      break-inside: avoid-page;
    }

    .footer-note {
      margin-top: 0.22in;
      color: var(--muted);
      font-size: 8.6pt;
      border-top: 1px solid var(--line);
      padding-top: 0.1in;
    }
  </style>
</head>
<body>
  <section class="cover">
    <div class="cover-inner">
      <div class="kicker">Security Review Summary</div>
      <h1>${escapeHtml(title)}</h1>
      <p class="subtitle">Application security posture summary.</p>
      <div class="meta-grid">
        <div class="meta-card">
          <div class="meta-label">Application</div>
          <div class="meta-value">SmartDocScan</div>
        </div>
        <div class="meta-card">
          <div class="meta-label">Document Date</div>
          <div class="meta-value">${escapeHtml(updated.replace("Last updated: ", ""))}</div>
        </div>
        <div class="meta-card">
          <div class="meta-label">Generated</div>
          <div class="meta-value">${escapeHtml(generatedOn)}</div>
        </div>
      </div>
    </div>
  </section>
  <main class="content">
    ${body}
    <p class="footer-note">This document summarizes application security controls and technical evidence as of the stated date.</p>
  </main>
</body>
</html>`;
}

function renderMarkdown(text) {
  const lines = text.split("\n");
  let out = "";
  let inList = false;

  for (let i = 0; i < lines.length; i += 1) {
    const raw = lines[i];
    const line = raw.trim();

    if (!line) {
      if (inList) {
        out += "</ul>\n";
        inList = false;
      }
      continue;
    }

    if (line.startsWith("## ")) {
      if (inList) {
        out += "</ul>\n";
        inList = false;
      }
      out += `<h2>${inline(line.slice(3))}</h2>\n`;
      continue;
    }

    if (line.startsWith("- ")) {
      if (!inList) {
        out += "<ul>\n";
        inList = true;
      }
      out += `<li>${inline(line.slice(2))}</li>\n`;
      continue;
    }

    if (inList) {
      out += "</ul>\n";
      inList = false;
    }
    out += `<p>${inline(line)}</p>\n`;
  }

  if (inList) {
    out += "</ul>\n";
  }

  return out;
}

function inline(value) {
  return escapeHtml(value).replace(/`([^`]+)`/g, "<code>$1</code>");
}

function escapeHtml(value) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function findBrowserExecutable() {
  const candidates = [
    "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
    "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe",
    "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
    "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
  ];

  return candidates.find((candidate) => fs.existsSync(candidate));
}
