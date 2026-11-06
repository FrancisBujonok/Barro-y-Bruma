# Story 007: Aldea jugable con 2–3 enemigos y jefe o salida

> **Epic**: barro-y-bruma
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: —

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 7

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From the brief's Player goal & fail state and Build order step 5:*

- [ ] Existe una escena jugable "aldea" con suelo de adoquines con ceniza (variantes), muros, puertas y callejones.
- [ ] La zona tiene entre 2 y 3 tipos de enemigo distintos, con estadísticas y patrón de ataque propios.
- [ ] La zona incluye un jefe o una salida que cierra la partida.
- [ ] Victoria: limpiar la aldea de enemigos y llegar a la salida (o derrotar al jefe).
- [ ] Derrota: la vida del jugador llega a 0, y se pierde el progreso de la zona actual (reinicio de la aldea).
- [ ] La partida funciona de principio a fin sin errores en consola.
- [ ] Un jugador puede sobrevivir a un grupo de enemigos usando solo posicionamiento y terreno; avanzar a ciegas por la niebla se castiga.
- [ ] Captura retenida de la aldea con niebla y enemigos en `production/qa/evidence/`.

---

## Implementation Notes

- Construye la aldea con el suelo de 4 variantes ya generado (`Assets/Art/Tiles/FloorAshCobble_0..3.asset`) y el `Tilemap` de la historia 006.
- Reutiliza `AshTestSceneBuilder` como referencia de cámara pixel-perfect y luz global.
- El arte nuevo (enemigos, muros, puertas) sale de `tools/art/barro_y_bruma.px.js` con la misma paleta de 16 colores.

---

## Out of Scope

- Más zonas, mundo abierto, historia y diálogos (ver `design/game-brief.md` § Out of scope).

---

## Files

- `Assets/Scenes/Village.unity`
- `Assets/Scripts/Zone/ZoneController.cs`
- `Assets/Tests/PlayMode/Zone/VillageClearTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/PlayMode/Zone/VillageClearTests.cs` OR playtest doc (advisory at `qa.level: minimal`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/barro-y-bruma/story-004-attack-telegraph.md`, `production/epics/barro-y-bruma/story-005-ambush.md`, `production/epics/barro-y-bruma/story-006-terrain-occlusion.md`
- Unlocks: None
