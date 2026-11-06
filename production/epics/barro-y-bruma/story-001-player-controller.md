# Story 001: Movimiento, ataque y esquiva del jugador

> **Epic**: barro-y-bruma
> **Status**: In Progress
> **Layer**: Core
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-05

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 1

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From the brief's Player goal & fail state and MVP feature 1:*

- [ ] El jugador se mueve en 8 direcciones con teclado y con gamepad (Input System), a una velocidad configurable.
- [ ] El movimiento es top-down 2.5D: el sprite se ordena por su Y para pasar por delante o por detrás de otros objetos.
- [ ] El ataque cuerpo a cuerpo golpea en un arco frontal con alcance, daño y recuperación configurables.
- [ ] La esquiva da un impulso corto en la dirección del movimiento, con enfriamiento configurable.
- [ ] El jugador tiene vida; al llegar a 0 se dispara el estado de derrota (fail state) que la historia 007 consumirá.
- [ ] Todos los valores de gameplay (velocidad, daño, alcance, enfriamientos, vida) viven en un `ScriptableObject`, no en el código.
- [ ] Ninguna acción depende de hover; gamepad parcial funciona sin ratón.
- [ ] Captura retenida del jugador moviéndose y atacando sobre `AshTestScene` guardada en `production/qa/evidence/`.

---

## Implementation Notes

- Reutiliza `Assets/Art/Sprites/Characters/Player.png` (32 PPU) y su sombra `PlayerShadow.png`.
- Usa Input System (ya está en el manifest) con un `InputActionAsset` propio; no uses el Input Manager antiguo.
- Mantén la lógica de movimiento separada del `MonoBehaviour` para poder probarla sin escena.
- Con la cámara Pixel Perfect, mueve por posiciones que se ajusten al píxel (`1/32` de unidad) para evitar temblor.

---

## Out of Scope

- Story 002: la niebla de ceniza.
- Story 005: bonus de daño por la espalda.
- Story 006: colisión con muros y puertas.

---

## Files

- Assets/Scripts/Player/PlayerController.cs
- Assets/Scripts/Player/PlayerStats.cs
- Assets/Scripts/Player/DodgeTimer.cs
- Assets/Scripts/Combat/IDamageable.cs
- Assets/Scripts/Combat/ArcMath.cs
- Assets/Scripts/Combat/AttackTimer.cs
- Assets/Settings/PlayerInput.inputactions
- Assets/Editor/AshTestSceneBuilder.cs
- Assets/Data/PlayerStats.asset

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: retained screenshot + sign-off in `production/qa/evidence/player-controller-evidence.md`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None
- Unlocks: `production/epics/barro-y-bruma/story-002-ash-fog.md`, `production/epics/barro-y-bruma/story-003-enemy-vision.md`
