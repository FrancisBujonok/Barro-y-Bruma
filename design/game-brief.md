# Game Brief: Barro y Bruma

**One-sentence pitch:** Un action RPG táctico, oscuro y medieval, en pixel art 2.5D, donde cazas a ciegas en una niebla de ceniza que oculta la cuadrícula y a los enemigos, y ganas flanqueándolos por la espalda antes de que te detecten.

## Core loop
- Explorar aldeas y ruinas envueltas en niebla de ceniza, con visión muy limitada.
- Leer al enemigo: sus conos de ataque solo se ven un instante antes del golpe.
- Usar el terreno (muros, puertas, callejones) para flanquear y emboscar por la espalda antes de ser detectado.
- Recoger botín y avanzar a la siguiente zona. La escasez de recursos y la presión de la niebla empujan a repetir con más cuidado.

## Player goal & fail state — what "working" looks like
- Meta: limpiar una aldea o ruina de sus enemigos y llegar a su salida/objetivo, y luego repetirlo en la siguiente zona.
- Fallo: la vida llega a 0, y se pierde el progreso de la zona actual.
- Funciona si el jugador puede sobrevivir a un grupo de enemigos usando solo posicionamiento y terreno, y se castiga el avance a ciegas.

## MVP — what must exist to be the game
- Movimiento del jugador top-down 2.5D con ataque y esquiva.
- Niebla de ceniza: la visibilidad del jugador es limitada y los enemigos quedan ocultos fuera de su radio.
- IA enemiga con cono de visión y detección, que patrulla y persigue.
- Conos de ataque enemigos que se revelan solo un instante antes del golpe.
- Ataque por la espalda/emboscada con bonus de daño, y detección que rompe el sigilo.
- Terreno que bloquea visión y movimiento (muros, puertas, callejones).
- Una zona jugable (aldea) con 2–3 tipos de enemigo y un jefe o salida final.

## Out of scope — not building this
- Multijugador.
- Mundo abierto o varias zonas (una sola aldea para el MVP).
- Árbol de habilidades, crafteo y economía profunda.
- Diálogos, historia y misiones extensas.
- Sistema de guardado complejo.

## Build order
1. Niebla de ceniza y visibilidad limitada (es lo menos probado y define el juego).
2. Conos de visión y de ataque enemigos, con detección.
3. Flanqueo y emboscada por la espalda.
4. Terreno táctico (muros, puertas, callejones) y la zona de la aldea.
5. Variedad de enemigos y jefe o salida final.

---
**Who it's for / what they feel:** Jugadores de combate táctico y atmósfera opresiva que quieren tensión, cautela y la satisfacción de una emboscada bien planeada.

**Art & audio direction:** Pixel art oscuro y desaturado (barro, ceniza, tonos apagados) con iluminación mínima, y audio ambiental sordo con sonidos amortiguados por la niebla.

**Reference game:** Hyper Light Drifter, de la que el MVP conserva solo el combate top-down con lectura de ataques; aquí se le suma la niebla y el flanqueo táctico.
