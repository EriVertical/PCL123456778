/* Simulated Windows token/cache APIs. Never modifies real memory or privileges. */
#include <stdint.h>
#include <stddef.h>
#include <errno.h>

typedef struct { uint32_t low; int32_t high; } luid;
typedef struct { uint32_t count; luid id; uint32_t attributes; } privileges;
static int mode, cache_calls, closes, restores, enables, restored_attributes, working_calls;
static const uintptr_t test_token = (uintptr_t)UINT64_C(0x100000001);

void ConfigureMemory(int value) {
    mode = value;
    cache_calls = closes = restores = enables = restored_attributes = working_calls = 0;
}
int MemoryCounter(int kind) {
    return kind == 0 ? cache_calls : kind == 1 ? closes : kind == 2 ? restores :
        kind == 3 ? enables : kind == 4 ? restored_attributes : working_calls;
}
void *GetCurrentProcess(void) { return (void *)(intptr_t)-1; }
int OpenProcessToken(void *process, uint32_t access, void **token) {
    errno = 0;
    if ((intptr_t)process != -1 || access != 0x28) { errno = 87; return 0; }
    if (mode == 1) { errno = 5; return 0; }
    *token = (void *)test_token;
    return 1;
}
int LookupPrivilegeValueW(const uint16_t *system, const uint16_t *name, luid *id) {
    const char *expected = "SeIncreaseQuotaPrivilege";
    errno = 0;
    if (system || !name) { errno = 87; return 0; }
    size_t n;
    for (n = 0; expected[n]; ++n) {
        if (name[n] != (unsigned char)expected[n]) { errno = 87; return 0; }
    }
    if (name[n]) { errno = 87; return 0; }
    if (mode == 2) { errno = 1313; return 0; }
    id->low = 17;
    id->high = 0;
    return 1;
}
int AdjustTokenPrivileges(void *token, int disable_all, privileges *requested,
                          uint32_t length, privileges *previous, uint32_t *returned) {
    errno = 0;
    if ((uintptr_t)token != test_token || disable_all || requested->count != 1 ||
        requested->id.low != 17 || requested->id.high != 0) { errno = 87; return 0; }
    if (previous) {
        ++enables;
        if (length != sizeof(privileges) || requested->attributes != 2 || !returned) { errno = 87; return 0; }
        if (mode == 3) { errno = 5; return 0; }
        if (mode == 4) { errno = 1300; return 1; }
        *previous = *requested;
        previous->count = mode == 7 ? 0 : 1;
        previous->attributes = mode == 8 ? 1 : 0;
        *returned = sizeof(privileges);
    } else {
        ++restores;
        restored_attributes = (int)requested->attributes;
        if (length || returned) { errno = 87; return 0; }
        if (mode == 6) { errno = 5; return 0; }
    }
    return 1;
}
int SetSystemFileCacheSize(uintptr_t minimum, uintptr_t maximum, uint32_t flags) {
    ++cache_calls;
    errno = 0;
    if (minimum != UINTPTR_MAX || maximum != UINTPTR_MAX || flags) { errno = 87; return 0; }
    if (mode == 5) { errno = 5; return 0; }
    return 1;
}
int CloseHandle(void *token) {
    ++closes;
    errno = 0;
    return (uintptr_t)token == test_token;
}
int EmptyWorkingSet(void *process) {
    ++working_calls;
    errno = 0;
    if ((uintptr_t)process != test_token) { errno = 87; return 0; }
    if (mode == 9) { errno = 5; return 0; }
    return 1;
}
