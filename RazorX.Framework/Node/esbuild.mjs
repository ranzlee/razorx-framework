import * as esbuild from "esbuild";
import fs from "fs/promises";

// Build JavaScript
await esbuild.build({
  entryPoints: ["./src/razorx.js"],
  bundle: true,
  minify: true,
  sourcemap: true,
  target: "esnext",
  format: "esm",
  platform: "browser",
  outdir: "./dist/js",
});

// Copy CSS file to dist
console.log("Copying razorx.css...");
await fs.mkdir("./dist/css", { recursive: true });
await fs.copyFile("./src/razorx.css", "./dist/css/razorx.css");
console.log("Build complete!");