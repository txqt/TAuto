# Changelog

All notable changes to the **TAuto Engine** will be documented in this file.

## [Unreleased] - 2026-02-11
### Added
- **Vision Enhancements**:
    - **Otsu Thresholding**: Replaced fixed threshold with adaptive Otsu algorithm for better segmentation.
    - **Auto-Polarity**: Automatic detection of light/dark text.
    - **Multi-Scale Voting**: Improved OCR accuracy for small fonts by voting across multiple scales.

## [Unreleased] - 2026-02-08
### Added
- **Phase 1: Hybrid Polling**: Implementation of `Fast`/`Slow` intervals and `SemaphoreSlim` wake-up logic.
- **Phase 2: Event-Driven Transitions**: Added `EventTransition` and `RaiseEvent` system for <1ms response times.
- **Phase 3: Robustness**: Per-transition `TimeoutMs` and `MaxRetries`.
- **Phase 4: Composite Logic**: Support for `AND`, `OR`, `NOT` logic in state transitions.
- **Phase 5: Performance Metrics**: Execution tracing, visit counts, and polling efficiency tracking.
- **Validation Engine**: Real-time detection of unreachable states and logic errors.
- **New Actions**: Added `SwipeAction`, `TypeAction`, and `LogAction` with variable interpolation.
- **OCR Integration**: Enhanced `IVisionService` with Tesseract support.

## [Unreleased] - 2026-01-12
### Added
- Expanded `Usage Examples` in `README.md` with detailed code snippets for `ScriptRunner` and `StateMachine`.
- Initial `CHANGELOG.md`, `TODO.md`, and `TESTING.md` for project tracking.

### Modified
- Updated contact email in `SECURITY.md` to `cv.hovanthanh@gmail.com`.
- Refined `README.md` usage section to reflect actual class signatures.

### Project Maintenance
- Reset git history to "Initial commit" for project rebranding/relaunch.

