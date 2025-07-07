import * as esbuild from "esbuild";

await esbuild.build({
  entryPoints: ["./src/razorx.js"],
  bundle: true,
  minify: true,
  sourcemap: true,
  target: "esnext",
  format: "esm",
  platform: "browser",
  outdir: "./dist",
});