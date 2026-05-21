# NPC Physics Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make NPC shot selection, line display, and hit behavior track Advanced Physics more closely without adding Udon-unsafe code or heavy per-frame computation.

**Architecture:** Keep the work centered in `PracticeManager.cs` and reuse the same evaluated shot result for selection, logging, and drawing. Use a two-stage flow: cheap geometric coarse filters first, then a small number of Udon-safe precise checks for the remaining candidates. Avoid new allocations, recursion, LINQ, and any physics simulation loops that would duplicate the real engine.

**Tech Stack:** Unity, UdonSharp, VRChat Udon runtime, existing billiards physics modules, existing testMode / log export pipeline.

---

### Task 1: Add a shared NPC shot result cache and Udon-safe helper layer

**Files:**
- Modify: `Modules/BilliardsModule/UdonScripts/PracticeManager.cs`

- [ ] **Step 1: Add the shared evaluation fields and helpers**

Add a small internal cache in `PracticeManager` for the currently evaluated candidate so the same values can drive selection, drawing, and logs without recomputing them. Keep it primitive-field based for Udon safety.

```csharp
// Shared candidate scratch values for NPC evaluation.
private int _evalBall = -1;
private int _evalPocket = -1;
private int _evalShotType = 0;
private int _evalKickCushion = -1;
private int _evalKickCushion2 = -1;
private Vector3 _evalAimDir = Vector3.forward;
private Vector3 _evalFirstImpactPos = Vector3.zero;
private float _evalShotDist = 0f;
private float _evalScore = -1f;
private float _evalSpin = 0f;
private float _evalCutAngle = 0f;
```

Add or keep small geometry helpers in the same file only if they are pure and cheap:

```csharp
private Vector3 _ReflectPoint(Vector3 point, int cushion)
private Vector3 _GetCushionBouncePoint(Vector3 fromPoint, Vector3 reflectedTarget, int cushion)
private bool _IsCushionBouncePointValid(Vector3 point, int cushion)
private bool _IsPathClearBallsOnly(Vector3 start, Vector3 end, int excludeBall)
private bool _IsPathCrossesCushion(Vector3 a, Vector3 b)
private bool _BallApproachBadAngle(Vector3 ballPos, Vector3 pocketPos, Vector3 pocketCenter, float cutAngle)
```

- [ ] **Step 2: Normalize the way candidate results are committed**

Create one local helper path for score comparison so each pass writes into the same cache shape before copying to the final NPC fields.

```csharp
private void _CommitBestNpcCandidate()
{
    bestBall = _evalBall;
    bestPocket = _evalPocket;
    bestAimDir = _evalAimDir;
    bestShotDist = _evalShotDist;
    bestSpin = _evalSpin;
    bestShotType = _evalShotType;
    bestKickCushion = _evalKickCushion;
    bestKickCushion2 = _evalKickCushion2;
    bestScore = _evalScore;
}
```

Keep this logic simple and local to `PracticeManager.cs` so no other script needs to know about the evaluation scratch state.

- [ ] **Step 3: Verify the file still compiles conceptually under Udon constraints**

Check that the helper additions use only simple vectors, ints, floats, and bools. Do not introduce:
- `List<T>`
- `async/await`
- `yield`
- lambdas
- new custom object allocations per candidate

Expected result: the new cache fields and helpers are plain Udon-friendly members only.

---

### Task 2: Refactor direct shots and bank shots to use one scoring flow

**Files:**
- Modify: `Modules/BilliardsModule/UdonScripts/PracticeManager.cs`

- [ ] **Step 1: Rework PASS 1 to fill the shared cache directly**

Keep the existing direct-shot geometry, but stop scattering “best” values across the method. Instead, compute each candidate into the scratch cache, then commit only when it beats the current best score.

Use the existing direct-shot pattern:

```csharp
Vector3 ghostBall = ballPos - t2pDir * BALL_DIAMETER;
Vector3 cueToGhost = ghostBall - cuePos;
float shotDist = cueToGhost.magnitude;
Vector3 aimDir = cueToGhost / shotDist;
float alignment = Vector3.Dot(aimDir, t2pDir);
```

Then write the candidate into the shared scratch fields, calculate the score once, and compare once.

- [ ] **Step 2: Make single-bank and two-cushion bank share the same geometry helpers**

Keep the existing single-bank path, but make sure the reflection correction and bounce-point validation are applied in the same order as the final draw code.

For two-cushion bank, use the same ordered pair of cushions for both selection and rendering:

```csharp
Vector3 reflected1 = _ReflectPocket(pocketPos, c2);
Vector3 reflected2 = _ReflectPocket(reflected1, c1);
Vector3 bounce1 = _GetCushionBouncePoint(ballPos, reflected2, c1);
Vector3 bounce2 = _GetCushionBouncePoint(bounce1, reflected1, c2);
```

Then validate:
- bounce points are not invalid (`float.MaxValue`)
- both bounce points sit on valid rail segments
- ball-to-rail and rail-to-pocket segments are not blocked
- the final pocket-center deviation stays inside the pocket radius

- [ ] **Step 3: Restore cut-angle calculations so logs and score use the same sign convention**

Keep the direct and bank cut-angle diagnostics aligned with the same direction vector convention. The earlier sign inversion fix should remain in place so logging cannot drift back to 180° for straight shots.

Expected result: direct shots, single-bank shots, and two-cushion bank shots all feed the same best-shot update path and no longer diverge between display and selection.

---

### Task 3: Refactor kick shots and display so the shown path equals the evaluated path

**Files:**
- Modify: `Modules/BilliardsModule/UdonScripts/PracticeManager.cs`
- Modify if needed: `Modules/BilliardsModule/UdonScripts/DesktopManager.cs`

- [ ] **Step 1: Rebuild single-kick and two-kick evaluation around the same reflected target chain**

Keep kick evaluation in `PracticeManager`, but compute the first and second cushion points from the same reflected target that the draw layer will use.

Use the same ordered reflection sequence in both selection and drawing:

```csharp
Vector3 reflected1 = _ReflectPoint(ballPos, c2);
Vector3 reflected2 = _ReflectPoint(reflected1, c1);
Vector3 bounce1 = _GetCushionPoint(cuePos, reflected2, c1);
Vector3 bounce2 = _GetCushionPoint(bounce1, reflected1, c2);
```

Populate:
- `npcKickCushion`
- `npcKickCushion2`
- `npcFirstImpactPos`
- `npcAimDir`
- `npcShotType`

from that same candidate result.

- [ ] **Step 2: Make the draw code reuse the saved kick geometry instead of recomputing a different path**

In the draw branch, use `npcKickCushion` / `npcKickCushion2` / `npcFirstImpactPos` to render the same route that was selected.

Expected rendering order:
- cue ball to first cushion
- first cushion to second cushion, if present
- last cushion to target ball

If the candidate is invalid, fall back to the single-cushion or direct fallback path, but do not compute a separate alternate route for display.

- [ ] **Step 3: Keep `DesktopManager` changes minimal**

Only touch `DesktopManager.cs` if the NPC animation needs to read the already-computed shot result more directly. Do not introduce a second geometry model there.

Expected result: two-cushion kick lines shown in the UI match the exact same line family used by NPC selection and firing.

---

### Task 4: Tighten scoring, power mapping, and risk filtering to better match Advanced Physics

**Files:**
- Modify: `Modules/BilliardsModule/UdonScripts/PracticeManager.cs`

- [ ] **Step 1: Simplify the power model so it stays conservative under long paths**

Keep the existing power clamp and the current `MAX_POWER` / `MIN_POWER` bounds, but make the following rule explicit in the scoring path:
- long bank or kick paths should receive a lower power ceiling
- near-pocket direct shots should favor stop-shot or low-draw behavior
- risky scratch lines should penalize score before power is finalized

Use the current post-selection adjustment block, not a second independent power solver.

- [ ] **Step 2: Fold the scratch and roadblock corrections into one cheap pass**

Keep the existing line-tracing correction, but make it a single pass that can lower score or reduce power after the best candidate is chosen.

The correction should only use:
- a short fixed number of sample steps
- segment checks to pockets and rails
- squared distance comparisons where possible

Do not add any new frame-by-frame simulation.

- [ ] **Step 3: Keep the logging useful but compact**

Keep the test logs, but make them reflect the final evaluated result rather than an intermediate guess.

Log fields should include:
- shot type
- ball and pocket ids
- cue direction
- first impact point
- cut angle
- power
- spin
- key reject reason for skipped candidates

Expected result: NPC becomes more conservative where the real physics would punish the shot, without adding heavy CPU cost.

---

### Task 5: Verify with NPC test mode and compare against Advanced Physics behavior

**Files:**
- Modify if needed: `Modules/BilliardsModule/UdonScripts/Editor/NpcLogExporter.cs`
- Modify if needed: `README.md`
- No new runtime files unless logging output needs a small formatting tweak.

- [ ] **Step 1: Run the NPC test flow and capture the log output**

Use the existing `testMode` flow to generate a fresh sample set and export the logs.

Expected log signals to inspect:
- no `切角=180.0°` on straight shots
- two-cushion routes print the same cushion order in selection and draw logs
- rejected candidates have a clear reason
- final shot type matches the displayed line family

- [ ] **Step 2: Compare behavior on at least three representative shot families**

Validate these cases specifically:
- straight direct shot
- single-bank or single-kick near a cushion
- two-cushion bank or two-cushion kick

Check that the selected shot, the drawn line, and the actual fired cue direction stay aligned.

- [ ] **Step 3: Commit the implementation once the log evidence is clean**

Only commit after the log shows the NPC is using one consistent geometry path for selection and display.

Expected result: the NPC behaves more like the advanced physics table without introducing noticeable Udon stalls.

---

## Coverage check

- Spec goal: align NPC with Advanced Physics while staying Udon-safe → Tasks 1-4.
- Unified result structure for selection/display/logging → Tasks 1-3.
- Direct, bank, two-bank, kick, two-kick, safety coverage → Tasks 2-4.
- Performance constraints and Udon compatibility → Task 1 and Task 4.
- Validation against logs and representative shot families → Task 5.

## Self-review notes

- No TODO/TBD placeholders remain.
- Helper names match the current `PracticeManager.cs` naming style.
- The plan keeps the main refactor inside one file unless `DesktopManager` needs a tiny animation-only adjustment.
- The plan does not rely on unsupported Udon features such as LINQ, recursion, or async code.