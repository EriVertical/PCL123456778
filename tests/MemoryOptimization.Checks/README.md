# Memory optimization regression checks

Links the actual `SystemMemoryOptimization.vb` against a native Windows API
fixture on Linux. No real privilege is enabled and no real cache is flushed.
Checks privilege acquisition, ERROR_NOT_ALL_ASSIGNED even with a true API return,
SIZE_T-width flush arguments, unchanged cache-limit flags, original privilege
restoration and handle cleanup on success/failure.

From the repository root with .NET 10 SDK and a C compiler:

```sh
mkdir -p tests/MemoryOptimization.Checks/bin
cc -shared -fPIC -Wall -Wextra -Werror -o tests/MemoryOptimization.Checks/bin/libmemory-fixture.so tests/MemoryOptimization.Checks/native-memory-fixture.c
dotnet run --project tests/MemoryOptimization.Checks --framework net10.0 -- "$PWD/tests/MemoryOptimization.Checks/bin/libmemory-fixture.so"
dotnet build tests/MemoryOptimization.Checks --framework net48
```

The net48 target checks compilation against the launcher's framework only.
The production operation is documented by Microsoft:
https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-setsystemfilecachesize
It trims the **system file cache**, not all applications' working sets or every
standby page. A lower total RAM usage or any fixed reduction is not guaranteed.

Windows integration still needs verification using a newly built launcher:

1. Run the launcher normally, enable pre-launch optimization and launch a game.
   `runas --memory` should request UAC for the helper. Accepting should execute
   the cache operation and return a nonnegative available-memory delta in KiB.
2. Cancel UAC: show a skip message and continue launching the game.
3. Invoke optimization from an already elevated launcher: execute directly.
4. Run `--memory` from a non-admin console: report failure and return -1, rather
   than silently doing nothing. Native failure should also return -1.
5. Confirm game launch does not remain waiting if the optimizer throws. The main
   launcher manifest stays `asInvoker`; the child does not launch Minecraft.
