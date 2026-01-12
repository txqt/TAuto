# Testing Guide for TAuto Engine

## 🧪 Manual Testing Steps

### 1. Document Verification
- [ ] Check `SECURITY.md` for correct email ([cv.hovanthanh@gmail.com](mailto:cv.hovanthanh@gmail.com)).
- [x] Verify `README.md` examples compile (visual check of signatures).
- [ ] Verify `README.md` includes both `ScriptRunner` and `StateMachine` examples.


### 2. Execution Logic (Manual)
- [ ] Verify `ScriptRunner` can load and start a sequential script.
- [ ] Verify `StateMachine` transitions trigger correctly.

## 🤖 Automated Tests
*(Coming Soon - `TAuto.Tests` project is planned)*

## ⚠️ Test Gaps
- Low coverage of edge case transitions in `StateMachine`.
- No performance stress tests for long-running scripts.
