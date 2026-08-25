# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.2] - 2026-08-25

### Added

- **Object Position** offset in Transform Settings — slides the asset in the icon frame without changing camera angle.

### Fixed

- Moving **Object Position** no longer re-aims the camera at the object (that looked like a rotation change). Camera always looks at the isolation origin; position is composition-only.

## [1.0.1] - 2026-08-22

### Fixed

- Invalid `.meta` GUIDs caused Unity to ignore documentation / package.json assets.
- Added missing `.meta` for README / CHANGELOG / LICENSE.

## [1.0.0] - 2026-08-22

### Added

- Initial UPM release: Odin-based **Asset Icon Generator** editor window.
- Real-time preview with auto-update on setting changes.
- Auto-framing camera from renderer bounds.
- Transparent or solid background; optional temp directional light.
- Configurable export resolution, save directory, and file name.
- PNG capture with Project window ping on save.
- Menu: **Tools → Asset Icon Generator 📸**

[1.0.0]: https://github.com/makarGames/Unity-Asset-Icon-Generator/releases/tag/v1.0.0
