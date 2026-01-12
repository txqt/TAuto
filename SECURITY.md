# Security Policy for TAuto Engine

The Engine is a sensitive component as it handles device interaction and execution of custom scripts.

## Reporting Vulnerabilities
If you discover a security flaw in the runner or the action execution logic:
1. Do not open a public issue.
2. Email your findings to [cv.hovanthanh@gmail.com](mailto:cv.hovanthanh@gmail.com).


## Focus Areas
- Code injection via script variables.
- Resource exhaustion/Memory leaks in long-running state machines.
- Unsanitized input in platform-specific drivers.
