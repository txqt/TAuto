# Contributing to TAuto Engine

TAuto Engine is the core logic provider for the TAuto Framework. Since this is a library designed for developers, we have strict quality requirements.

## 🛠️ Development Environment
- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022 or JetBrains Rider

## 📜 Contribution Rules
1. **Thread Safety:** The engine runs in multi-threaded environments. Ensure shared resources in `ScriptContext` are handled safely.
2. **Interface First:** Always define new functionalities in `TAuto.Core` interfaces before implementing them in `TAuto.Automation`.
3. **Serialization:** Any new `IAction` must be registered in `ActionJsonConverter` and be fully serializable.
4. **Performance:** Avoid heavy allocations in the `StateMachineRunner` hot path.

## 🧪 Testing
We aim for high test coverage for the engine logic.
- Run `dotnet test` (once test projects are added).
- Manual verification via `AutoBot.App` is required for any UI-related automation changes.

## 💬 Pull Request Process
1. Fork and branch.
2. Implement feature/fix.
3. Ensure no breaking changes to the JSON script format (or provide a migration path).
4. Update `CHANGELOG.md` in the root documentation.
