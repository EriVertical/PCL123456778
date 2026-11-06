# Watcher regression checks

This harness links the actual `ModWatcher.vb`. Launcher UI/logging dependencies
are stubbed. On Linux a native fixture supplies controlled Win32 window API
responses, including handles larger than 32 bits and UTF-16 titles. It tests
enumeration continuation, window ownership, visibility, utility window filtering,
Forge early-window recognition, and state/exit updates without further logs.
It neither launches Minecraft nor demonstrates real Windows UI behavior.

From the repository root, with .NET 10 SDK and a C compiler:

```sh
mkdir -p tests/Watcher.Checks/bin
cc -shared -fPIC -Wall -Wextra -o tests/Watcher.Checks/bin/libwatcher-fixture.so tests/Watcher.Checks/native-window-fixture.c
dotnet run --project tests/Watcher.Checks --framework net10.0 -- "$PWD/tests/Watcher.Checks/bin/libwatcher-fixture.so"
dotnet build tests/Watcher.Checks --framework net48
```

The net48 target only checks that the linked watcher compiles against the app's
framework. Windows integration still needs a real new game launch: a visible
Forge loading window should release “等待游戏窗口出现” without waiting for another
log line or the main menu. Hidden utility windows and other game processes must
not trigger success. Also verify title changes, maximization and crash handling.
