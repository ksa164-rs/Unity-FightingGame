# Project Goal

This project is a Unity prototype that attempts to reproduce the core feel and combat structure of Street Fighter 6 for research and prototyping purposes.

The priority is not visual imitation alone.

The most important goals are:

- responsive combat controls
- deterministic frame-based combat behavior
- clear separation between combat states
- reliable input buffering
- hit / block / cancel behavior
- reusable combat architecture
- easy tuning from the Unity Inspector or data assets
- maintainable code that can be iterated on quickly

Do not over-engineer systems that are still in the prototype phase.

---

# Model Routing Policy

Always use the lowest-cost / fastest model that can complete the task reliably.

Do not use Astra for routine implementation.

Classify each task into one of the following four levels.

## LEVEL 1 - Luna / Fast Model

Use for mechanical, repetitive, isolated work.

Examples:

- file search
- scene inspection
- prefab inspection
- renaming
- adding serialized fields
- Debug.Log
- formatting
- null checks
- simple component setup
- repetitive prefab changes
- small isolated fixes
- simple Inspector changes

Use this level when architectural reasoning is unnecessary.

---

## LEVEL 2 - Terra / Standard Coding Model

This is the default model for most Unity implementation.

Examples:

- MonoBehaviour implementation
- Unity Input System
- PlayerController
- character movement
- Animator integration
- UI
- HP systems
- camera logic
- audio triggering
- VFX triggering
- HitBox / HurtBox implementation
- standard attacks
- ScriptableObject data
- Editor tools
- prefab setup
- normal bug fixing
- isolated refactoring

Prefer Terra for normal production work.

Do not escalate merely because the task contains a large amount of code.

---

## LEVEL 3 - Sol / Deep Technical Reasoning

Use when a subsystem requires deeper reasoning or several gameplay systems interact.

Examples:

- combat state machine
- frame data implementation
- input buffer
- command input parser
- combo system
- cancel system
- cancel windows
- hit stop
- hit stun
- block stun
- counter hit
- throw / strike resolution
- invulnerability
- attack priority
- simultaneous hit handling
- combo scaling
- Animator / gameplay synchronization problems
- difficult bugs involving several systems
- substantial refactoring

Use Sol when the implementation requires reasoning about interactions between systems.

---

## LEVEL 4 - Astra / Architecture and End-to-End Problems

Reserve Astra for the highest-impact or hardest tasks in the project.

Astra should primarily be used for system-wide reasoning, architecture decisions, and end-to-end implementation planning.

Examples:

### Combat Architecture

- designing the complete combat architecture from scratch
- determining responsibilities between Input, Combat State, Frame Data, Hit Detection, and Animation
- redesigning a combat system that has become structurally unstable
- deciding how multiple combat subsystems should communicate

### Street Fighter-style System Integration

Use Astra when several of these systems must work correctly together:

- frame data
- input history
- command recognition
- input buffer
- state machine
- cancel rules
- hit stop
- hit stun
- block stun
- counter hit
- combo scaling
- resource systems
- animation
- hit detection

Example:

Input
-> Input History
-> Command Parser
-> Buffer
-> Combat State
-> Attack Data
-> Hit Detection
-> Hit Resolution
-> Hit Stop
-> Cancel System
-> Next Action

If the task requires reasoning about this entire chain, prefer Astra.

### Major Technical Decisions

Use Astra for decisions that would be expensive to reverse later.

Examples:

- deciding whether combat simulation should be frame-based or time-based
- deciding how attack data should be structured
- choosing ScriptableObject vs runtime data architecture
- deciding how the combat state machine should be structured
- designing a system that must support multiple playable characters
- designing training mode / frame-data debugging architecture
- designing deterministic combat behavior
- preparing for rollback networking
- large-scale performance architecture

### Difficult Cross-System Bugs

Use Astra when:

- the bug cannot be reproduced reliably
- several systems appear correct individually but fail when combined
- the root cause is unclear
- multiple previous fixes have failed
- gameplay state and Animator state disagree
- frame timing becomes inconsistent
- input behavior changes depending on frame rate
- fixing one system breaks another

Do not use Astra for ordinary compiler errors or simple bugs.

### Large Refactoring

Use Astra before modifying large portions of the project.

First analyze:

- existing architecture
- dependencies
- technical debt
- regression risk
- migration order

Then create a clear migration plan.

Implementation of individual files may then be delegated back to Terra or Sol.

---

# Preferred Model Workflow

For major features:

Astra
-> architecture / system design

Sol
-> subsystem design and difficult implementation

Terra
-> normal implementation

Luna
-> repetitive setup and simple fixes

Then:

Terra / Sol
-> testing and normal debugging

Astra
-> only if a system-wide architectural problem appears

Do not keep using Astra after the architectural question has been resolved.

---

# Important Rule for Astra

Astra is not the default coding model.

Do NOT use Astra just because:

- the task is large
- many files need editing
- the user asks for a feature
- the implementation is long

Use Astra when the task has high architectural impact, high uncertainty, or requires reasoning across the entire combat system.

After Astra defines the architecture, prefer implementing individual components with Terra or Sol.

---

# Combat Architecture Rules

Gameplay logic must not depend entirely on animation timing.

Animator state should primarily represent visuals.

Combat state should be controlled by gameplay code or explicit combat data.

Preferred direction:

Combat State
-> Frame Progress
-> Attack Data
-> Hit Detection
-> Hit Resolution
-> Animation / VFX / Audio

Avoid:

Animator
-> Animation Event
-> Gameplay Logic
-> Combat State

Animation Events may be used for presentation events such as:

- sound
- VFX
- footsteps
- secondary visual timing

Critical combat behavior should remain deterministic.

---

# Frame-Based Combat

Treat fighting-game timing as frame-based data.

Each attack should be able to define:

- Startup Frames
- Active Frames
- Recovery Frames
- Hit Stop
- Hit Stun
- Block Stun
- Cancel Start Frame
- Cancel End Frame
- Input Buffer Window
- Damage
- Guard behavior
- Hit level
- movement
- knockback
- special flags

Avoid scattering these values across multiple MonoBehaviours.

Prefer centralized attack data such as ScriptableObjects or equivalent structured data.

---

# Input Rules

Input should be separated from action execution.

Preferred structure:

Raw Input
-> Input History
-> Command Detection
-> Input Buffer
-> Action Request
-> Combat State Validation
-> Action Execution

Do not directly execute attacks from InputAction callbacks if doing so makes buffering or command detection difficult.

Input history should support frame-based command recognition.

---

# State Machine Rules

Combat actions should use explicit states.

Examples:

- Idle
- Walk
- Jump
- Attack
- Recovery
- HitStun
- BlockStun
- Knockdown
- Throw
- Special
- Super

Avoid large collections of unrelated boolean flags such as:

isAttacking
isHit
isBlocking
isRecovering
canMove
canAttack
canCancel

Prefer a clear state machine with explicit transition rules.

---

# Hit Detection

Separate:

HitBox
and
HurtBox.

Do not treat normal character colliders as combat hitboxes unless specifically required.

Hit detection should report a hit candidate.

Damage and combat resolution should be handled by a separate combat resolution layer.

Preferred flow:

HitBox
-> Hit Candidate
-> Combat Resolver
-> Damage / HitStop / HitStun / BlockStun
-> State Transition

---

# Cancel System

Do not hard-code combo transitions directly inside individual attacks.

Prefer data-driven cancel rules.

Example:

Attack A

Normal Cancel:
- Attack B
- Attack C

Special Cancel:
- Hadoken
- Shoryuken

Super Cancel:
- Level 1 Super

Cancel conditions may include:

- on hit
- on block
- on whiff
- frame window
- resource requirement
- state requirement

---

# Prototype Priority

Prioritize gameplay feel over production-level architecture.

However, do not introduce temporary architecture that will obviously prevent:

- frame data tuning
- input buffering
- cancel rules
- multiple characters
- CPU opponents
- training mode
- frame-data debugging

Prototype systems should remain inspectable and tunable.

---

# Unity Inspector / Debugging

Expose important gameplay values so they can be tuned without modifying code.

Where useful, provide debug visualization for:

- current combat state
- current attack
- current frame
- startup / active / recovery phase
- input history
- buffered command
- hitboxes
- hurtboxes
- cancel window
- hit stop
- hit stun

Prefer tools that make combat behavior visible rather than debugging only through logs.

---

# Implementation Rules

Before modifying an existing system:

1. inspect the related scripts
2. identify existing architecture
3. identify dependencies
4. determine whether the requested change is local or architectural
5. modify the minimum necessary area

Do not create duplicate systems when an existing system can be extended.

Do not replace working systems without a clear technical reason.

If the existing implementation is structurally incorrect, explain the problem briefly before changing it.

---

# Bug Fixing

When fixing bugs:

1. reproduce or identify the actual cause
2. distinguish symptom from root cause
3. make the smallest safe fix
4. check related systems for regression
5. avoid unrelated refactoring

Escalation:

Local bug
-> Terra

Complex subsystem bug
-> Sol

Unknown cross-system / architectural bug
-> Astra

---

# Decision Priority

When multiple implementations are possible, prioritize in this order:

1. correct combat behavior
2. responsiveness
3. deterministic timing
4. debuggability
5. maintainability
6. implementation speed
7. visual convenience

Do not sacrifice gameplay correctness simply to make Animator setup easier.

---

# Scope Discipline

Do not implement features that were not requested unless they are required for the requested feature to function correctly.

Do not add large frameworks or third-party dependencies without a strong reason.

For prototype work, prefer small understandable systems over generic abstractions.

---

# Final Model Selection Rule

Default:
Terra

Simple / repetitive task:
Luna

Complex subsystem or difficult technical problem:
Sol

Project-wide architecture, major redesign, or extremely difficult cross-system problem:
Astra

Use the lowest-capability model that can solve the task reliably.

Astra should be used deliberately for high-value reasoning rather than routine coding.