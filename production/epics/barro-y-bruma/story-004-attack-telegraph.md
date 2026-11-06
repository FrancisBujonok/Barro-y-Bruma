# Story 004: Conos de ataque que se revelan antes del golpe

> **Epic**: barro-y-bruma
> **Status**: Ready
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: —

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 4

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From the brief (the player only sees attack cones moments before they land):*

- [ ] Cuando un enemigo inicia un ataque, dibuja el cono de ataque (forma y alcance reales del golpe) en el suelo.
- [ ] El cono solo aparece durante una ventana configurable antes del impacto (la anticipación); fuera de ella no se ve.
- [ ] El cono mostrado coincide exactamente con el área que daña: lo que se ve es lo que golpea.
- [ ] El cono se dibuja en la paleta del juego (sangre seca u óxido), legible sobre el suelo de ceniza.
- [ ] Si el enemigo está oculto por la niebla (story 002), su cono también lo está.
- [ ] La ventana de anticipación es lo bastante larga para reaccionar (valor inicial proyectado a ~0,35 s, ajustable al probar).
- [ ] Captura retenida de un cono de ataque visible en `production/qa/evidence/`.

---

## Implementation Notes

- Reutiliza la misma geometría para el daño y para el dibujo; no mantengas dos formas por separado.
- Dibuja con un `SpriteRenderer` o malla simple, sin shaders complejos; el pixel art no debe suavizarse.

---

## Out of Scope

- Story 003: decidir cuándo ataca el enemigo.
- Story 007: diferentes patrones de ataque por tipo de enemigo.

---

## Files

- `Assets/Scripts/Combat/AttackTelegraph.cs`
- `Assets/Scripts/Combat/AttackShape.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: retained screenshot + sign-off in `production/qa/evidence/attack-telegraph-evidence.md`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/barro-y-bruma/story-002-ash-fog.md`, `production/epics/barro-y-bruma/story-003-enemy-vision.md`
- Unlocks: `production/epics/barro-y-bruma/story-007-village-zone.md`
