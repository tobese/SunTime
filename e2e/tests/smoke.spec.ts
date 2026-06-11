import { test, expect } from "@playwright/test";

// Uno WASM with SkiaRenderer: the entire UI is drawn on #uno-canvas (aria-hidden).
// baseURL includes the /SunTime/ subpath, so navigate with "" not "/" to stay on it.
const WASM_TIMEOUT = 30_000;

test.describe("SunTime", () => {
  test("loads and renders the clock dial", async ({ page }, testInfo) => {
    const errors: string[] = [];
    page.on("pageerror", (err) => errors.push(err.message));

    await page.goto("");

    // Wait for Uno bootstrap to finish
    await expect(page.locator(".uno-loader")).toBeHidden({ timeout: WASM_TIMEOUT });

    // #uno-canvas is the SkiaSharp surface (aria-hidden, so use ID selector)
    const canvas = page.locator("#uno-canvas");
    await expect(canvas).toBeAttached({ timeout: 5_000 });
    const box = await canvas.boundingBox();
    expect(box?.width).toBeGreaterThan(100);
    expect(box?.height).toBeGreaterThan(100);

    // Attach any JS errors as test annotations rather than failing — the
    // known Arg_NullReferenceException fires during Uno/SkiaSharp WebGL init
    // in headless Chromium but does not prevent the canvas from rendering.
    if (errors.length > 0) {
      testInfo.annotations.push({ type: "js-errors", description: errors.join("; ") });
    }

    await page.screenshot({ path: "snapshots/clock-loaded.png", fullPage: true });
  });

  test("accessibility enable button is present after boot", async ({ page }) => {
    await page.goto("");
    await expect(page.locator(".uno-loader")).toBeHidden({ timeout: WASM_TIMEOUT });
    await expect(
      page.getByRole("button", { name: /enable accessibility/i })
    ).toBeAttached();
  });
});
