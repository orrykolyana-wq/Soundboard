# Soundboard ("Vicious Mockery") — Code Review Findings

- **Date:** 2026-10-14
- **Branch:** `heraldOfMechanus/10-14-polish` (at `6c9a736`, same tip as `main`)
- **Scope:** `Soundboard.cs` (the entire app, ~3,460 lines) + `LaunchpadLightTest.csproj`.
- **Out of scope:** visual/UI layout concerns (intentionally not reviewed). Everything below is logic, correctness, resources, and project hygiene.

**Severity key:** 🔴 likely bug · 🟠 leak / resource · 🟡 behavior / consistency · 🟢 cleanup / nit

## Summary

| # | Item | Severity |
|---|------|----------|
| 1 | `midiInOpen` callback flag is wrong — physical pad input likely never fires | 🔴 |
| 2 | `MIDIINCAPS.support` is `ushort` (should be `uint`) — 2-byte buffer overrun | 🔴 |
| 3 | MIDI out handle leaked when input open fails | 🟠 |
| 4 | Audio output matched by exact `FriendlyName` — silent wrong-device fallback | 🟡 |
| 5 | "Duplicate" suppression is per-file, blocks a second pad using the same sound | 🟡 |
| 6 | Worker thread blocked for entire playback; unbounded `Wait()` | 🟡 |
| 7 | Output / MIDI device lists built once at startup, never refreshed | 🟡 |
| 8 | No way to cancel a pending sound assignment | 🟡 |
| 9 | Virtual-pad vs physical-pad behavior has diverged | 🟡 |
| 10 | Hidden legacy controls + unreachable code paths | 🟢 |
| 11 | Unused types: `ProfileData`, `ProfilePadAssignment`, `profileBasePaletteValues` | 🟢 |
| 12 | Dead method `MidiSelectorButton_Click` | 🟢 |
| 13 | O(n) scans that are O(1) lookups (`GetColorForPaletteValue`, `GetPadIndexFromMidiNote`) | 🟢 |
| 14 | No `.gitignore`; `bin/`, `obj/`, `.csproj.user` pollute the repo | 🟢 |
| 15 | Stale `GPT Backup` compile exclusion; naming mismatch across csproj / app / settings | 🟢 |
| 16 | Minor: delegate lifetime, redundant auto-connect check, mixed `DeviceState` | 🟢 |

---

## 🔴 Likely bugs

### 1. `midiInOpen` callback flag — physical pads likely don't work (`Soundboard.cs:2386`)

```csharp
uint result = midiInOpen(out midiInHandle, deviceId, HandleMidiInCallback,
    GCHandle.ToIntPtr(midiInCallbackHandle), 0x00030000);
```

`0x00030000` is **`CALLBACK_WINDOW`** (a.k.a. `CALLBACK_TASK` in `mmsystem.h`). With a window-style callback, Windows treats the 4th parameter as an **HWND** and delivers input as `WM_...` messages to that window. Here the parameter is a `GCHandle` pointer, not a window handle, so messages go nowhere and the `HandleMidiInCallback` delegate (which has the *function*-callback signature) is **never invoked**. The documented value for a function callback is **`0x00090000` (`CALLBACK_FUNCTION`)**.

**Why it matters:** the whole physical-pad path (`ProcessMidiInput` → `HandlePhysicalPadPressed`) is probably dead. The status text `"(listening for Launchpad MIDI input)"` would be misleading.

**Fix:** pass `0x00090000`. Verify on hardware: if physical pads currently *do* trigger sounds, this analysis is wrong — but the flag as written matches no documented function-callback value.

### 2. `MIDIINCAPS.support` is `ushort` but the Windows field is a `DWORD` (`Soundboard.cs:264–277`)

```csharp
private struct MIDIINCAPS
{
    public ushort manufacturerId;   // 2
    public ushort productId;        // 2
    public uint driverVersion;      // 4
    public string productName;      // 64 (32 * UTF-16)
    public ushort support;          // 2  ← should be uint (4)
}
```

Windows' `MIDIINCAPS.dwSupport` is a `DWORD`. The managed struct marshals to **74 bytes** instead of **76**, so `midiInGetDevCapsW` writes **2 bytes past the end of the pinned struct buffer** — a small out-of-bounds write on every device-capability query (called from `OpenMidiInput`, lines 2311–2324). (`MIDIOUTCAPS` at line 246 does this correctly with `uint support`.)

**Fix:** change `support` to `uint` in `MIDIINCAPS`.

---

## 🟠 Resource leaks

### 3. MIDI **out** handle leaked when the input open fails (`Soundboard.cs:1386–1405`)

```csharp
midiHandle = newHandle;                       // out handle now owned by us
selectedMidiDeviceId = device.Id;
if (OpenMidiInput(device.Id, device.Name)) { midiConnected = true; }
else
{
    midiConnected = false;
    UpdateConnectionToggleAppearance();
    return;   // ← midiHandle never closed
}
```

`CloseMidiDevice()` early-returns when `midiConnected == false`, so on the next connect attempt the previously opened (and leaked) handle is never closed. Each failed connect leaks one `midiOut` handle.

**Fix:** on the failure branch, call `midiOutClose(newHandle)`, reset `midiHandle = IntPtr.Zero`, then return.

Related behavior question: `OpenMidiInput` returns `false` when no Launchpad-named *input* device exists, which **hard-fails the connection** — yet the status text says `Connected: <name> (no Launchpad MIDI input device was reported by Windows)` (lines 2347–2356). That message is factually wrong (nothing is connected), and it also means non-Launchpad MIDI devices can never be connected at all. Decide whether input is required, and make the status text match reality.

---

## 🟡 Behavior & consistency

### 4. Audio output matched by exact `FriendlyName`; silent fallback to default device (`Soundboard.cs:2636–2661`)

```csharp
if (device.FriendlyName == outputName)   // ordinal, case-sensitive
```

Everywhere else in the codebase device names are compared with `OrdinalIgnoreCase` (favorites list, add/remove dialogs). If the stored name's case differs from the live `FriendlyName`, the match fails and playback **silently falls back to `WaveOutEvent` (system default)** — the user thinks sound is going to the TV/headset while it goes elsewhere.

**Fix:** compare with `String.Equals(..., StringComparison.OrdinalIgnoreCase)`. Longer term, match on `MMDevice.ID` (a stable device identifier) rather than `FriendlyName`, which can change (e.g. "Headphones" → "Headphones (2)"), and store the ID in the settings JSON.

### 5. Duplicate suppression is keyed on file path only (`Soundboard.cs:81, 2610–2613`)

`activeSoundPaths` is a `HashSet<string>` of full file paths. If two pads are assigned the **same sound file**, pressing the second pad does nothing while the first is still playing. For a soundboard it's plausible to want two pads on one file (retrigger / overlap). At minimum this deserves an explicit decision; if it's intentional, document it in a comment.

### 6. Worker thread blocked for the whole playback; unbounded `Wait()` (`Soundboard.cs:2622–2704`)

`PlayPadSoundWorker` runs on the thread pool and calls `playbackFinished.Wait()` with **no timeout** for the entire duration of the sound. NAudio's `WasapiOut` already renders on its own thread — the worker only needs to *wait*. Rapid pad-mashing pins a pool thread per in-flight sound; if `PlaybackStopped` ever doesn't fire (device pulled mid-playback), the thread blocks **forever** and repeated occurrences starve the pool.

**Fix:** `async`/`await` with a `TaskCompletionSource` completed by `PlaybackStopped` (plus a safety timeout on the wait). No pool thread is held during playback.

### 7. Device lists are built once at startup (`Soundboard.cs:1234–1320, 2859–2904`)

- **MIDI:** `FindMidiDevices` runs only in the constructor. Unplug/replug the Launchpad and the list (and the auto-connect selection) is stale; there's no refresh affordance. Device IDs are enumeration-order and can shift between runs.
- **Audio:** `PopulateAudioOutputDevices` also runs once. A newly plugged-in output doesn't appear in Favourite Outputs (the "Add Output" dialog re-enumerates, so it's only half-covered).

**Fix (optional for polish):** re-enumerate on connect failure / via a "Refresh" action; for audio, use `MMDeviceEnumerator.RegisterEndpointNotificationCallback` (`MMNotificationClient`) to react to plug/unplug.

### 8. No way to cancel a pending sound assignment (`Soundboard.cs:1795–1850`)

After "Assign Sound" + file chosen, the next pad click **always** assigns it. There's no cancel path (no Escape, no "cancel" status message, no re-click to dismiss). User must assign to some pad or Clear Assignments (which wipes *everything*).

Also note `ClearAssignmentsButton_Click` (lines 2212–2223) nulls `pendingSoundFilePath` but leaves `pendingDisplayName` / `pendingLightColorValue` set — harmless (the guard checks the path) but inconsistent; clear all three.

### 9. Virtual-pad and physical-pad behavior have diverged

`PadButton_Click` (lines 1662–1768) and `HandlePhysicalPadPressed` (lines 2490–2568) implement overlapping logic that no longer matches:

- **Unassigned pad:** virtual click *toggles* the test light off/on (lines 1747–1767); physical press always *turns it on* (lines 2560–2567).
- **Colour-only pad** (colour set, no sound): virtual click falls into the "light-test toggle" branch; physical press lights it in the chosen colour (lines 2547–2558).
- **Pending assignment** and the `rainbowPageTwo` special case exist only in the virtual path.

**Fix:** extract one `PressPad(int padIndex, bool fromHardware)` method so both entry points share behavior (or consciously document the differences).

### 10. `DeviceState.All` vs `DeviceState.Active` mismatch (minor)

`PlayPadSoundWorker` enumerates endpoints with `DeviceState.All` (line 2638) while the favorites list and dialogs use `DeviceState.Active` (lines 2821, 2867, 2955). A stored but currently-disabled endpoint would play to it but never show in the list. Pick one and use it everywhere.

---

## 🟢 Dead code & cleanup

### 11. Hidden legacy controls and unreachable paths (`Soundboard.cs:811–824, 1691–1703`)

- `paletteSelector` — a hidden `ComboBox` with 128 items, randomly initialized (`Random.Shared.Next(34, 38)`) and used only as "an internal default-colour source." This is a leftover; a plain `byte` field would do.
- `allOffButton`, `allOnButton`, `rainbow1Button`, `rainbow2Button` — created with `Visible = false` and never surfaced. Consequently `rainbowPageTwo` can never be `true`, making the branch at lines 1691–1703 unreachable.
- Reachable-via-dead-button code: `ShowPalette`, `Rainbow1Button_Click`, `Rainbow2Button_Click`, `AllOffButton_Click`, `AllOnButton_Click`, `ResetPcGrid`, `InitializePaletteCycle`/`StartPaletteCycle`/`StopPaletteCycle`/`PaletteCycleTimer_Tick` (the timer is only startable through the dead branch).
- `AllOnButton_Click` / `ResetPcGrid` paint pads with `SystemColors.Highlight` / `SystemColors.Control`, which would fight the dark theme if ever re-enabled.

**Fix:** delete what's not coming back; if the palette scanner is still wanted, keep just `StartPaletteCycle`/`StopPaletteCycle` and wire them to real (visible) controls.

### 12. Unused types (`Soundboard.cs:84, 3413–3427`)

`ProfileData`, `ProfilePadAssignment`, and the `profileBasePaletteValues` dictionary are declared but never referenced — a profile save/load feature that was never built. Delete or implement.

### 13. Dead method

`MidiSelectorButton_Click` (lines 1528–1561) builds a context menu but is never wired to any control. Delete.

### 14. O(n) lookups that are O(1)

- `GetColorForPaletteValue` (lines 2166–2181) linearly scans all 128 entries to find the closest palette value — the table **is** the complete 0–127 range, so it's just `PadColorPalette[value]` (with a bounds clamp if you want to keep it defensive). It runs on every pad-button visual update.
- `GetPadIndexFromMidiNote` (lines 2571–2588) loops 64 times per incoming MIDI note. An O(1) formula or a 128-entry lookup table built once would be cleaner (the note mapping `81 + col − row·10` matches the MK1 layout — worth a one-time verification against the actual hardware, given the comments reference the MK2).

### 15. Minor / defensive

- The `MidiInProc` delegate (line 2384) is kept alive only by the P/Invoke marshaller's pinning of the callback. Holding an explicit field reference (`private MidiInProc? midiInCallback;`) is the conventional, self-documenting pattern.
- The constructor's auto-connect check (lines 404–412) duplicates the Launchpad preference already done in `FindMidiDevices` (lines 1300–1319).
- `MainForm_FormClosing` calls `StopAllSounds()` then `CloseMidiDevice()`; in-flight workers may still be tearing down at process exit. Harmless (OS cleanup), but an `Application.ApplicationExit` or form-`Closed` dispose hook would be tidier.

---

## 🟢 Project hygiene (repo / csproj)

### 16. No `.gitignore`

`bin/` and `obj/` are untracked but show up in every `git status`, and `LaunchpadLightTest.csproj.user` (per-user IDE settings) **is tracked**. Add a standard .NET `.gitignore` (`bin/`, `obj/`, `.vs/`, `*.user`, `*.suo`) and `git rm --cached` the `.user` file.

### 17. Stale csproj bits

- `<Compile Remove="GPT Backup\**\*.cs" />` (lines 11–13) — the folder doesn't exist in the repo; leftover from an earlier iteration. Remove.
- Naming drift: project/namespace is `LaunchpadLightTest`, the app title is **"Vicious Mockery"**, and the settings folder is `ViciousMockery`. `bin/` even contains a stale `Soundboard.dll` from the old project name. Consider setting `<AssemblyName>` explicitly (e.g. `ViciousMockery`) and aligning the namespace + project file name so the output matches what the app calls itself.

### 18. Optional: structure

The entire app is one 3,460-line file. If the polish branch wants a structural win, the natural seams are: MIDI P/Invoke + connection logic, audio output management (devices/modes/settings), pad state (actions/lights/disabled), and the form. Splitting into partial classes or small services would make the file reviewable without changing behavior. Flag as optional — big diff on a polish branch.

---

## ✅ Verified OK (no action)

- `activeSoundPaths` check-then-add is done under `activeAudioPlayersLock` — no race.
- `StopAllSounds` generation counter correctly discards players still being prepared.
- `ProcessMidiInput` marshals to the UI thread via `BeginInvoke` with dispose/handle guards — correct pattern (aside from bug #1, the callback never arrives).
- `GCHandle` used for the callback instance is allocated/freed on all paths in `TryOpenMidiInput` / `CloseMidiDevice`.
- `SetStatusText` handles cross-thread updates with `InvokeRequired` + dispose guards.
- MIDI note-on with velocity 0 is correctly treated as "off" (line 2461).
- Sound file existence is checked before playback (line 2599).
- All `padActions` / `manualPadLights` / `disabledPads` state is touched only from the UI thread (MIDI is marshaled) — no cross-thread state races.
