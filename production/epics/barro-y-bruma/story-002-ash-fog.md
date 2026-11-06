# Story 002: Niebla de ceniza y visibilidad limitada

> **Epic**: barro-y-bruma
> **Status**: Ready
> **Layer**: Core
> **Type**: Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: —

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 2

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From the brief (Build order step 1 — the riskiest, least proven part):*

- [ ] La niebla cubre toda la zona jugable y oculta el suelo y los enemigos fuera de un radio de visión alrededor del jugador.
- [ ] El radio de visión, su borde (suavidad) y la densidad de la niebla son valores configurables.
- [ ] Los enemigos fuera del radio no se dibujan; al entrar se revelan sin saltos bruscos.
- [ ] La niebla se mueve o respira con lentitud (efecto de ceniza), sin parpadear ni romper el pixel art (sin bilinear sobre sprites).
- [ ] El efecto funciona con la cámara Pixel Perfect (640x360, 32 PPU) y con el renderer 2D de URP.
- [ ] El coste cabe en el presupuesto de 16,6 ms (60 fps) en la escena de prueba.
- [ ] Captura retenida con y sin niebla sobre `AshTestScene` en `production/qa/evidence/`.

---

## Implementation Notes

- Es la apuesta más incierta del juego: prueba primero un prototipo mínimo (máscara de visión + capa de niebla) antes de pulir.
- Candidatos a evaluar en URP 2D (verifica en la documentación del 6000.3, no de memoria): `Light2D` con sombras, o una máscara en `RenderTexture` sobre una capa de niebla.
- La visibilidad de enemigos debe poder consultarse por código (`IsVisibleToPlayer`) para que la IA y la historia 003 no dependan de lo que se dibuja.

---

## Out of Scope

- Story 003: percepción de los enemigos (su propio cono de visión).
- Story 006: oclusión de la visión del jugador por muros.

---

## Files

- `Assets/Scripts/Fog/AshFogController.cs`
- `Assets/Scripts/Fog/FogSettings.cs` (ScriptableObject)
- `Assets/Art/Shaders/AshFog` (shader o material)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: retained screenshot + sign-off in `production/qa/evidence/ash-fog-evidence.md`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/barro-y-bruma/story-001-player-controller.md`
- Unlocks: `production/epics/barro-y-bruma/story-004-attack-telegraph.md`
