# AJAI visual identity

The visual identity uses the supplied purple shield, a midnight background, violet light, and restrained cyan accents. The banner identifies the original project's first-place hackathon award; it does not claim the replacement model was the original award-winning model.

## Assets

- `docs/assets/ajai-banner.png`: wide banner for the README and repository social preview.
- `docs/assets/ajai-logo.png`: original supplied shield logo, preserved.
- `docs/assets/ajai-icon.svg`: small, scalable shield/check icon for repository documentation.
- `src/AJAI.Server/wwwroot/branding/`: the matching banner, logo, favicon, and stylesheet consumed by the development API documentation.

The API documentation uses these local assets in Development only. Production Swagger visibility is unchanged. The custom stylesheet is decorative and does not change authentication, API behavior, or detection results.

The banner was generated with the built-in image tool using the supplied shield as a reference. It is brand artwork, not a screenshot of a functioning dashboard. The favicon is a small code-native SVG drawn to match the same palette. The original Vue dashboard remains absent.

The banner generation prompt is preserved in `docs/assets/banner-prompt.txt` for reproducibility of the design brief. Generation is not guaranteed to produce identical pixels.

## Verification

The solution built with zero warnings and errors. All 10 existing integration tests passed. The live development page loaded its OpenAPI schema, custom stylesheet, banner, and SVG icon. The schema endpoint was explicitly configured to work when Swagger is served at the site root. `api-workspace-preview.png` is a real local screenshot; the banner itself is generated artwork.
