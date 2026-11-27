using Unity.Netcode;
using UnityEngine;

// Mensajes del modo cooperativo. Todos viajan dentro de un solo mensaje con nombre de Netcode ("FOW"); el primer
// byte dice cuál es. El anfitrión simula el juego (aves, peces, oleadas) y los demás muestran lo que les llega; cada
// jugador mueve su propio pez y avisa sus disparos y golpes.
public static class CoopOp
{
    // ---- Sala ----
    public const byte Hello = 1;          // cliente -> anfitrión: mi nombre
    public const byte Roster = 2;         // anfitrión -> todos: las 4 casillas (nombre, listo, Taller)
    public const byte Ready = 3;          // cliente -> anfitrión: listo / no listo
    public const byte StartGame = 4;      // anfitrión -> todos: empieza la partida (semilla)
    public const byte LoadScene = 5;      // anfitrión -> todos: cargar una escena
    public const byte InScene = 6;        // todos -> anfitrión: ya estoy en la escena (terminó de cargar)
    public const byte GoWorkshop = 7;     // anfitrión -> todos: al Taller (oleada superada, si hubo jefe, hora)
    public const byte WorkshopDone = 8;   // cliente -> anfitrión: ya elegí mi mejora
    public const byte BackToLobby = 9;    // anfitrión -> todos: volver a la sala
    public const byte HostLeaving = 10;   // anfitrión -> todos: la sala se cierra

    // ---- Juego ----
    public const byte PlayerState = 20;   // cliente -> anfitrión: cómo está mi pez
    public const byte PlayersState = 21;  // anfitrión -> todos: cómo están los peces de todos
    public const byte SpawnBird = 22;     // anfitrión -> todos
    public const byte BirdsState = 23;    // anfitrión -> todos (frecuente, puede perderse)
    public const byte BirdHit = 24;       // anfitrión -> todos: un ave recibió un golpe (destello)
    public const byte BirdDie = 25;       // anfitrión -> todos
    public const byte SpawnFish = 26;     // anfitrión -> todos (también los defensores)
    public const byte FishState = 27;     // anfitrión -> todos (frecuente, puede perderse)
    public const byte FishDie = 28;       // anfitrión -> todos
    public const byte DefenderBroken = 29;// anfitrión -> todos
    public const byte SpawnPickup = 30;   // anfitrión -> todos
    public const byte RemovePickup = 31;  // anfitrión -> todos
    public const byte TakePickup = 32;    // cliente -> anfitrión: recogí esta cápsula
    public const byte HitBird = 33;       // cliente -> anfitrión: le di a esta ave
    public const byte Director = 34;      // anfitrión -> todos: oleada, cuenta atrás, peces, hora del día
    public const byte Banner = 35;        // anfitrión -> todos: cartel grande
    public const byte Toast = 36;         // anfitrión -> todos: aviso corto
    public const byte Alert = 37;         // anfitrión -> todos: alerta del casco
    public const byte Sound = 38;         // anfitrión -> todos: sonido 2D (cuerno de la oleada...)
    public const byte DamagePlayer = 39;  // anfitrión -> dueño: te golpearon
    public const byte KnockPlayer = 40;   // anfitrión -> dueño: te empujaron
    public const byte CurrentPlayer = 41; // anfitrión -> dueño: te arrastra una corriente
    public const byte Shot = 42;          // cualquiera -> anfitrión -> los demás: disparo (solo se ve)
    public const byte EnemyShot = 43;     // anfitrión -> todos: proyectil de un ave (solo se ve)
    public const byte GameOver = 44;      // anfitrión -> todos: fin de la partida
    public const byte GroupIncoming = 45; // anfitrión -> todos: llega otro grupo de aves (cada uno calcula el lado)
    public const byte Revive = 46;        // anfitrión -> todos: oleada superada, los caídos vuelven (armadura 0..1)
}

// Ayudas para escribir y leer compacto
public static class CoopWire
{
    // Ángulo en grados -> short (±180° = ±32767)
    public static short Angle(float degrees)
    {
        return (short)Mathf.Clamp(Mathf.RoundToInt(Mathf.DeltaAngle(0f, degrees) / 180f * 32767f), -32767, 32767);
    }

    public static float Angle(short packed)
    {
        return packed / 32767f * 180f;
    }

    public static void WriteEuler(FastBufferWriter w, Quaternion q)
    {
        Vector3 e = q.eulerAngles;
        w.WriteValueSafe(Angle(e.x));
        w.WriteValueSafe(Angle(e.y));
        w.WriteValueSafe(Angle(e.z));
    }

    public static Quaternion ReadEuler(FastBufferReader r)
    {
        r.ReadValueSafe(out short x);
        r.ReadValueSafe(out short y);
        r.ReadValueSafe(out short z);
        return Quaternion.Euler(Angle(x), Angle(y), Angle(z));
    }

    public static byte Unit(float v01)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(v01) * 255f), 0, 255);
    }

    public static float Unit(byte packed)
    {
        return packed / 255f;
    }

    public static void WriteColor(FastBufferWriter w, Color c)
    {
        Color32 c32 = c;
        w.WriteValueSafe(c32);
    }

    public static Color ReadColor(FastBufferReader r)
    {
        r.ReadValueSafe(out Color32 c);
        return c;
    }

    public static void WriteText(FastBufferWriter w, string s)
    {
        w.WriteValueSafe(s ?? "");
    }

    public static string ReadText(FastBufferReader r)
    {
        r.ReadValueSafe(out string s);
        return s ?? "";
    }
}
