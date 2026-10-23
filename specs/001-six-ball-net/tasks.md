# Implementation tasks

Status: M1 implemented, AC-01 partial (left controller hardware). M2 complete (AC-02 passed). Later milestones pending. This is a task specification, not evidence of a built game.

Use `[ ]` for incomplete and `[x]` only for the named completed work. Keep device acceptance pending in `validation.md` when unavailable. Dependencies apply in the order shown. Automated preparatory work may continue while waiting for a physical check, but do not represent untested prerequisites as accepted.

## M1 — First Quest scene

Requirements: REQ-001. Acceptance: AC-01.

- [x] T001 Inspect repository instructions, Git state, Mac/macOS, Unity installations, Android modules, and current packages without disturbing existing work.
- [x] T002 Record exact compatible Editor/package versions and prerequisites; install/configure available tooling and provide owner-controlled account/device steps.
- [x] T003 Create/open the URP project; configure Android/Quest ARM64, OpenXR, controller profile, Input System, and XRI.
- [x] T004 Add a single XR Origin, floor, reference objects, and visible tracked controllers. Validate required scene dependencies.
- [x] T005 Run compilation/project validation and produce/install a development build using available tools. Record actual commands/results. *(2026-10-23: E-001–E-003.)*
- [ ] T006 Perform AC-01 on Quest or leave it pending with exact instructions. Add reproducible project setup/build notes to README.

## M2 — Practice net

Dependency: M1 implementation; headset scale acceptance requires device access. Requirements: REQ-002, REQ-015. Acceptance: AC-02.

- [x] T007 Establish metre scale, axes, batting/launcher anchors, and floor-origin conventions.
- [x] T008 Add primitive pitch, crease, striker stumps, inexpensive net boundaries, simple lighting, and scoreboard placement.
- [x] T009 Verify AC-02 at the real batting stance and record any alignment changes.

## M3 — Tracked bat

Dependency: M2. Requirements: REQ-003, REQ-007. Acceptance: AC-03.

- [ ] T010 Add selected-controller binding, grip offsets, handedness selection, bat visual, and kinematic physics proxy.
- [ ] T011 Implement timestamped pose history, contact-point motion calculation, validity checks, and history reset after discontinuities.
- [ ] T012 Add alignment/debug controls and verify AC-03 for both hands on Quest.

## M4 — Bowling machine

Dependency: M3 setup. Requirements: REQ-004, REQ-005. Acceptance: AC-04.

- [ ] T013 Add DeliveryConfig, speed conversion, initial-velocity calculation, and unreachable-configuration handling.
- [ ] T014 Implement one reusable ball with gravity/bounce and complete reset of velocity, trail, flags, and timers.
- [ ] T015 Add repeatable debug launch controls and bounce measurement; run AC-04's ten-delivery check and record configuration/tolerance results.

## M5 — Bat–ball contact

Dependency: M4. Requirements: REQ-006, REQ-007, REQ-015. Acceptance: AC-05 plus relevant AC-03 checks.

- [ ] T016 Configure primitive colliders, layers, supported CCD, and one contact-resolution authority.
- [ ] T017 Implement bounded, orientation-aware response using relative/contact-point velocity and off-centre tuning.
- [ ] T018 Add focused sweep/substep handling if measured CCD gaps require it; handle overlaps and duplicate callback/sweep encounters.
- [ ] T019 Add impact audio, modest selected-controller haptics, and optional contact debug markers.
- [ ] T020 Run scripted contact matrix and owner headset checks; record speeds, angles, counts, failures, and feel adjustments.

## M6 — Outcomes and scoring

Dependency: M5. Requirements: REQ-008, REQ-009, REQ-010. Acceptance: AC-06.

- [ ] T021 Implement typed DeliveryResult and identity-based single finalization, terminal rules, timeout, and stale-event rejection.
- [ ] T022 Define nonoverlapping practice zones, priority, no-contact rules, wicket zero-score behavior, and zero-run fallback.
- [ ] T023 Record measured carry only on a real post-contact ground landing; expose net-first/N/A state.
- [ ] T024 Add meaningful scoring/outcome tests and run AC-06, including contact-then-bowled and competing terminal signals.

## M7 — Six-ball session

Dependency: M6. Requirements: REQ-011, REQ-012, REQ-013, REQ-014. Acceptance: AC-07, AC-08, AC-09.

- [ ] T025 Implement guarded session transitions, countdown, intervals, six-result limit, and cancellation generation/token.
- [ ] T026 Add Start, live scoreboard, six-row results, derived totals, and Restart.
- [ ] T027 Handle focus/tracking loss, interrupted-ball cancellation without consumption, explicit readiness recovery, and motion-history reset.
- [ ] T028 Test a full session, five restarts across phases, and interruptions; verify no seventh ball, stale callback, or corrupted total.

## M8 — Polish and device acceptance

Dependency: M7. Requirements: REQ-015, REQ-016 plus regression of all previous requirements. Acceptance: AC-10 and remaining open scenarios.

- [ ] T029 Improve visibility, menu readability, audio/haptics balance, and actionable error messages without expanding scope.
- [ ] T030 Profile on Quest against the selected rendering target; document CPU/GPU measurements and optimize demonstrated bottlenecks.
- [ ] T031 Complete three sessions, both-handedness checks, restart/recovery, and regression scenarios on the identified build.
- [ ] T032 Update README, validation status, known limitations, and requirement coverage. Claim MVP completion only when evidence supports it.

## Scope/change rule

Before adding work outside these tasks, update the affected requirement/scenario and plan, then add a mapped task. Preserve IDs; retire rather than silently renumber requirements. A bug fix within existing behavior can use an appended task with its requirement mapping.
