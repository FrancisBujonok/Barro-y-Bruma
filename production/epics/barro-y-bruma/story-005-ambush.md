# Story 005: Emboscada por la espalda y rotura de sigilo

> **Epic**: barro-y-bruma
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: —

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 5

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From the brief (Build order step 3 — flank and ambush before being detected):*

- [ ] Un ataque del jugador cuenta como emboscada si el enemigo no lo ha detectado y el jugador está en su espalda (fuera de su cono de visión, dentro de un arco trasero configurable).
- [ ] La emboscada aplica un multiplicador de daño configurable (puede matar de un golpe a enemigos normales).
- [ ] Un ataque a un enemigo que ya detectó al jugador, o por delante, hace daño normal.
- [ ] Atacar por detrás no revela al resto: solo los enemigos que vean al jugador lo detectan.
- [ ] Si un enemigo detecta al jugador (story 003), el sigilo se rompe para ese enemigo y la emboscada deja de aplicarse a él.
- [ ] La regla de emboscada es pura y se prueba con tests: por la espalda sin detección, por la espalda ya detectado, de frente, de lado, en el borde del arco.
- [ ] El multiplicador y el arco trasero viven en un `ScriptableObject`.

---

## Implementation Notes

- Calcula el ángulo entre la dirección del enemigo y el vector enemigo→jugador; la espalda es el arco opuesto al frente.
- Reutiliza el estado "alerta" de la IA (story 003) como única fuente de verdad de si fue detectado.

---

## Out of Scope

- Story 003: la detección en sí.
- Story 004: dibujar los conos de ataque.

---

## Files

- `Assets/Scripts/Combat/AmbushRule.cs`
- `Assets/Scripts/Combat/AmbushSettings.cs` (ScriptableObject)
- `Assets/Tests/EditMode/Combat/AmbushTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/Combat/AmbushTests.cs` (advisory at `qa.level: minimal`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/barro-y-bruma/story-001-player-controller.md`, `production/epics/barro-y-bruma/story-003-enemy-vision.md`
- Unlocks: `production/epics/barro-y-bruma/story-007-village-zone.md`
