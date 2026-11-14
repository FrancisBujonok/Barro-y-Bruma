# Game Brief: Barro y Bruma

**One-sentence pitch:** Un action RPG táctico, oscuro y medieval, en pixel art 2.5D, donde grupos de enemigos salen de una niebla de ceniza y solo sobrevives eligiendo dónde plantar cara: en puertas, callejones y cuellos de botella donde te alcanzan de uno en uno.

## Core loop
- Explorar aldeas y ruinas con visión limitada por la niebla, buscando el mejor terreno antes de que llegue el grupo.
- Ser detectado: un grupo de enemigos carga y, en campo abierto, te rodea.
- Colocarte en un cuello de botella (puerta, callejón, esquina) para que solo uno o dos puedan atacarte a la vez, y leer sus conos de ataque.
- Eliminar al grupo, recoger botín y avanzar. Retirarse a un mal sitio o pelear al descubierto se castiga con la muerte.

## Player goal & fail state — what "working" looks like
- Meta: cruzar la aldea, sobrevivir a sus grupos de enemigos y llegar a la salida (o derrotar al jefe).
- Fallo: la vida llega a 0, y se pierde el progreso de la zona actual.
- Funciona si un grupo que mata al jugador en campo abierto puede derrotarse usando un cuello de botella, y si pelear al descubierto es claramente peor.

## MVP — what must exist to be the game
- Movimiento, ataque y esquiva del jugador (top-down 2.5D). *Ya implementado (historia 001).*
- Terreno que bloquea movimiento y crea cuellos de botella (muros, puertas, callejones).
- IA de grupo: los enemigos persiguen, rodean en campo abierto y hacen cola en los cuellos de botella; solo unos pocos pueden atacar a la vez.
- Conos de ataque enemigos que se revelan un instante antes del golpe.
- Niebla de ceniza: visibilidad limitada que oculta el tamaño del grupo hasta que está cerca.
- Daño extra por la espalda, sin sistema de sigilo.
- Una zona jugable (aldea) con 2–3 tipos de enemigo en grupos y un jefe o salida final.

## Out of scope — not building this
- Multijugador.
- Mundo abierto o varias zonas (una sola aldea para el MVP).
- Sistema de sigilo: detección con rotura de sigilo y emboscadas como mecánica central.
- Árbol de habilidades, crafteo y economía profunda.
- Diálogos, historia y misiones extensas.
- Sistema de guardado complejo.

## Build order
1. Terreno con cuellos de botella más IA de grupo: es lo menos probado, y define si el juego funciona.
2. Conos de ataque enemigos, para que la pelea en un cuello de botella se pueda leer.
3. Niebla de ceniza como apoyo (ocultar el tamaño del grupo).
4. Daño por la espalda y la zona de la aldea con varios grupos.
5. Variedad de enemigos y jefe o salida final.

---
**Who it's for / what they feel:** Jugadores de combate táctico que disfrutan resolviendo la pelea antes de empezarla, con la tensión de ver llegar un grupo y la satisfacción de haber elegido bien el sitio.

**Art & audio direction:** Pixel art oscuro y desaturado (barro, ceniza, tonos apagados) con iluminación mínima, y audio ambiental sordo con sonidos amortiguados por la niebla.

**Reference game:** Hyper Light Drifter, de la que el MVP conserva solo el combate top-down con lectura de ataques; aquí se le suma la IA de grupo y el uso del terreno para controlar el flujo de enemigos.
