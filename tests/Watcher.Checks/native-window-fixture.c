/* Deterministic Win32 substitute for Linux tests, not a real window detector. */
#include <stdint.h>
#include <stddef.h>
#include <string.h>

static int scenario, visits;
static uint32_t game_pid;
static const uintptr_t base_handle = (uintptr_t)UINT64_C(0x100000000);

void ConfigureWindows(int value, uint32_t pid) {
    scenario = value;
    game_pid = pid;
    visits = 0;
}

int WindowVisits(void) { return visits; }

int EnumWindows(int (*callback)(uintptr_t, intptr_t), intptr_t context) {
    int count = scenario == 0 ? 8 : scenario == 1 ? 6 : 1;
    for (int n = 1; n <= count; ++n) {
        ++visits;
        if (!callback(base_handle + n, context)) return 0;
    }
    return 1;
}

uint32_t GetWindowThreadProcessId(uintptr_t window, uint32_t *pid) {
    int n = (int)(window - base_handle);
    *pid = scenario <= 1 && n == 1 ? game_pid + 1 : game_pid;
    return 1;
}

int IsWindowVisible(uintptr_t window) {
    return !(scenario <= 1 && window - base_handle == 3);
}

static int copy_ascii(uint16_t *buffer, int capacity, const char *text) {
    int i = 0;
    for (; i < capacity - 1 && text[i]; ++i) buffer[i] = (unsigned char)text[i];
    buffer[i] = 0;
    return i;
}

int GetClassNameW(uintptr_t window, uint16_t *buffer, int capacity) {
    int n = (int)(window - base_handle);
    const char *name = scenario <= 1 && n == 2 ? "OtherWindow" :
        scenario == 3 || scenario == 6 ? "SDL_app" :
        scenario == 4 ? "LWJGL" : scenario == 5 ? "SunAwtFrame" : "GLFW30";
    return copy_ascii(buffer, capacity, name);
}

int GetWindowTextW(uintptr_t window, uint16_t *buffer, int capacity) {
    int n = (int)(window - base_handle);
    const char *title = "Minecraft Forge 1.20.1";
    if (scenario <= 1 && n == 4) title = "";
    if (scenario <= 1 && n == 5) title = "GLFW message window";
    if (scenario <= 1 && n == 6) title = "Minecraft Hidden Utility Window";
    if (scenario == 2) title = "FML early loading";
    if (scenario == 6) title = "SDL_app";
    int size = copy_ascii(buffer, capacity, title);
    if (scenario == 0 && n == 7 && capacity > size + 2) {
        buffer[size++] = 0x4e16; /* 世 */
        buffer[size++] = 0x754c; /* 界 */
        buffer[size] = 0;
    }
    return size;
}

int SetWindowTextW(uintptr_t window, const uint16_t *text) {
    return window > base_handle && text != NULL;
}
int ShowWindow(uintptr_t window, uint32_t command) {
    return window != 0 && command == 3;
}
