# Story 006: Terreno que bloquea visión y movimiento

> **Epic**: barro-y-bruma
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: —

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 6

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From the brief (Build order step 4 — walls, doors and alleys make flanking possible):*

- [ ] Los muros bloquean el movimiento del jugador y de los enemigos.
- [ ] Los muros bloquean la línea de visión de los enemigos (story 003) y la visión del jugador a través de la niebla (story 002).
- [ ] Las puertas se pueden abrir y cerrar; cerradas bloquean visión y paso, abiertas no.
- [ ] Hay callejones y esquinas donde el jugador puede esconderse del cono de un enemigo.
- [ ] El terreno se define en un `Tilemap` con una capa de colisión y oclusión, no objeto a objeto.
- [ ] Un enemigo que pierde al jugador detrás de un muro deja de verlo aunque esté dentro del alcance y el ángulo.
- [ ] Una prueba de integración reproduce una esquina: sin línea de visión no hay detección; al salir al descubierto, sí.

---

## Implementation Notes

- Usa `Tilemap` + `TilemapCollider2D` + `CompositeCollider2D` (módulos 2D ya presentes).
- La línea de visión debe usar la misma capa de física que el movimiento para no mantener dos definiciones de "muro".

---

## Out of Scope

- Story 007: el diseño concreto de la aldea.
- Escenario con varias zonas (fuera del MVP).

---

## Files

- `Assets/Scripts/Terrain/Door.cs`
- `Assets/Scripts/Terrain/SightBlocker.cs`
- `Assets/Tests/PlayMode/Terrain/TerrainOcclusionTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/PlayMode/Terrain/TerrainOcclusionTests.cs` OR playtest doc (advisory at `qa.level: minimal`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/barro-y-bruma/story-001-player-controller.md`
- Unlocks: `production/epics/barro-y-bruma/story-007-village-zone.md`
