import { cpSync, existsSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(scriptDir, "..");
const source = resolve(webRoot, "..", "SmartDocScan", "Resources");
const destination = resolve(webRoot, "public", "Resources");
const requiredFile = "dynamsoft.webtwain.config.js";
const destinationFile = resolve(destination, requiredFile);

if (existsSync(destinationFile)) {
  process.exit(0);
}

if (!existsSync(resolve(source, requiredFile))) {
  console.error(`Dynamsoft resources were not found at ${source}`);
  process.exit(1);
}

mkdirSync(resolve(webRoot, "public"), { recursive: true });
cpSync(source, destination, { recursive: true });
