# Story 003: IA enemiga con cono de visión y detección

> **Epic**: barro-y-bruma
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: —

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 3

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From the brief (Build order step 2) and its fail state:*

- [ ] Cada enemigo tiene un cono de visión con ángulo, alcance y dirección (hacia donde mira) configurables.
- [ ] El enemigo patrulla una ruta de puntos y, si no detecta nada, no sale de ella.
- [ ] El jugador es detectado solo si está dentro del alcance Y del ángulo del cono Y sin una línea de visión bloqueada.
- [ ] La detección no es instantánea: hay un tiempo de reacción configurable; si el jugador sale del cono antes de que termine, no se detecta.
- [ ] Al detectar, el enemigo pasa a persecución y va hacia el jugador; si lo pierde, vuelve a patrullar tras un tiempo configurable.
- [ ] La lógica de detección es pura (sin `MonoBehaviour`) y se prueba con tests: dentro, fuera por ángulo, fuera por alcance, tras un obstáculo y justo en el borde.
- [ ] Los valores de IA viven en un `ScriptableObject` por tipo de enemigo.

---

## Implementation Notes

- Separa la regla (`VisionCone.CanSee(origin, facing, target, ...)`) de la escena para poder probarla en EditMode.
- Para la línea de visión usa `Physics2D` (el módulo `physics2d` ya está en el proyecto); el bloqueo real por muros se completa en la historia 006.
- Debe haber un modo de depuración que dibuje el cono con `Gizmos`.

---

## Out of Scope

- Story 004: mostrar el cono de ataque al jugador.
- Story 005: el daño por la espalda.
- Story 007: variedad de tipos de enemigo.

---

## Files

- `Assets/Scripts/EnemyAI/VisionCone.cs`
- `Assets/Scripts/EnemyAI/EnemyBrain.cs`
- `Assets/Scripts/EnemyAI/EnemyStats.cs` (ScriptableObject)
- `Assets/Tests/EditMode/EnemyAI/EnemyVisionTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/EnemyAI/EnemyVisionTests.cs` (advisory at `qa.level: minimal`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/barro-y-bruma/story-001-player-controller.md`
- Unlocks: `production/epics/barro-y-bruma/story-004-attack-telegraph.md`, `production/epics/barro-y-bruma/story-005-ambush.md`
