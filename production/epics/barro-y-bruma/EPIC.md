# Epic: Barro y Bruma — MVP

> **Source**: `design/game-brief.md` (no GDD at `minimal`)
> **Engine**: Unity 6000.3 · **Status**: Ready

## Goal
Un action RPG táctico, oscuro y medieval, en pixel art 2.5D, donde cazas a ciegas en una niebla de ceniza que oculta la cuadrícula y a los enemigos, y ganas flanqueándolos por la espalda antes de que te detecten.

## Scope (the brief's MVP list)
- Movimiento top-down 2.5D con ataque y esquiva.
- Niebla de ceniza: visibilidad limitada.
- IA enemiga con cono de visión y detección.
- Conos de ataque revelados un instante antes del golpe.
- Emboscada por la espalda y rotura de sigilo.
- Terreno que bloquea visión y movimiento.
- Una zona jugable (aldea) con 2–3 enemigos y jefe o salida.

## Build order
Niebla → conos y detección → flanqueo → terreno y aldea → variedad y jefe. Story 001 (movimiento) va antes porque nada se puede probar sin jugador.

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | Movimiento, ataque y esquiva del jugador | Visual/Feel | Ready | N/A |
| 002 | Niebla de ceniza y visibilidad limitada | Visual/Feel | Ready | N/A |
| 003 | IA enemiga con cono de visión y detección | Logic | Ready | N/A |
| 004 | Conos de ataque que se revelan antes del golpe | Visual/Feel | Ready | N/A |
| 005 | Emboscada por la espalda y rotura de sigilo | Logic | Ready | N/A |
| 006 | Terreno que bloquea visión y movimiento | Integration | Ready | N/A |
| 007 | Aldea jugable con 2–3 enemigos y jefe o salida | Integration | Ready | N/A |

## Out of scope
Ver `design/game-brief.md` § Out of scope.
