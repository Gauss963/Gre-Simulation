#!/usr/bin/env node
// Run with NODE_PATH pointing at the bundled workspace node_modules directory.
import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const [inputPath, outputPath] = process.argv.slice(2);
if (!inputPath || !outputPath) {
  console.error("Usage: extract_authorized_workbook.mjs INPUT.xlsx OUTPUT.json");
  process.exit(2);
}

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(inputPath));
const sheets = [];
for (const sheet of workbook.worksheets.items) {
  const used = sheet.getUsedRange(true);
  sheets.push({
    name: sheet.name,
    address: used?.address ?? null,
    values: used?.values ?? [],
  });
}

await fs.mkdir(path.dirname(outputPath), { recursive: true });
await fs.writeFile(outputPath, `${JSON.stringify(sheets)}\n`);
console.log(JSON.stringify(sheets.map((sheet) => ({
  name: sheet.name,
  address: sheet.address,
  rows: sheet.values.length,
  columns: Math.max(0, ...sheet.values.map((row) => row.length)),
})), null, 2));
