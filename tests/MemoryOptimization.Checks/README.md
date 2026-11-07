# Memory optimization regression checks

Links the actual `SystemMemoryOptimization.vb` against a native Windows API
fixture on Linux. No real privilege is enabled and no real cache is flushed.
Checks privilege acquisition, ERROR_NOT_ALL_ASSIGNED even with a true API return,
SIZE_T-width flush arguments, unchanged cache-limit flags, original privilege
restoration and handle cleanup on success/failure.
Also checks user/session/caller filtering, native working-set handle marshalling,
denied working-set calls and truthful partial/zero-delta messages. Real process
enumeration and Windows token owner lookup are not exercised on Linux.

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
Ordinary mode trims the **system file cache**. Deep mode also uses
https://learn.microsoft.com/windows/win32/api/psapi/nf-psapi-emptyworkingset
for accessible applications owned by the current user in the same interactive
session, excluding the helper and caller. It does not purge every standby page.
A lower total RAM usage or any fixed reduction is not guaranteed; applications
may fault their pages back into memory and briefly slow down.

Windows integration still needs verification using a newly built launcher:

1. Run the launcher normally, enable pre-launch optimization and launch a game.
   Default mode is deep. `runas --memory-v2 deep <caller-pid>` should request UAC;
   the helper returns step status flags, while the parent samples net available
   RAM change. Repeat in ordinary mode (`cache`) using the toolbox selector.
2. Cancel UAC: show a skip message and continue launching the game.
3. Invoke optimization from an already elevated launcher: execute directly.
4. Run `--memory` from a non-admin console: report failure and return -1, rather
   than silently doing nothing. Native failure should also return -1.
5. Confirm game launch does not remain waiting if the optimizer throws. The main
   launcher manifest stays `asInvoker`; the child does not launch Minecraft.
6. Open More → Toolbox → Memory cleanup, verify the default/persisted selection,
   run the standalone cleanup button and check it re-enables after cancellation.
   Protected processes must be skipped and partial results disclosed. Confirm
   the main launcher/helper, services and other-user processes are not trimmed.
