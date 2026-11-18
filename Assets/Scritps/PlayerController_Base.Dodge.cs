using UnityEngine;

// Esquive del pez (parte de PlayerController_Base), como el de las naves de los juegos de combate aéreo:
// un impulso brusco hacia un lado mientras el pez da una vuelta completa (360°) sobre sí mismo.
//  - Q: hacia la izquierda.  E: hacia la derecha. No hace falta girar: se mueve directo de lado.
//  - Sirve igual si vuela hacia adelante o hacia atrás, si sube o si cae (y también nadando): solo cambia el
//    movimiento de lado; lo que iba hacia adelante, arriba o abajo se conserva.
//  - El impulso es brusco: sale de golpe a toda velocidad y frena enseguida (recorre Dodge Distance metros).
//  - Durante un instante las aves no lo pueden golpear (se le pasan de largo).
//  - Tercera persona: solo gira el modelo del pez; la cámara no gira (lo sigue de lado).
//  - Primera persona: toda la pantalla da la vuelta completa con el giro.
public partial class PlayerController_Base
{
    [Header("Esquive - NUEVO")]
    public KeyCode dodgeLeftKey = KeyCode.Q;
    public KeyCode dodgeRightKey = KeyCode.E;
    [Tooltip("Además de Q / E, esquivar con doble toque de A / D")]
    public bool dodgeOnDoubleTap = false;
    [Tooltip("Segundos máximos entre los dos toques")]
    public float doubleTapTime = 0.28f;
    [Tooltip("Metros que se desplaza hacia el lado")]
    public float dodgeDistance = 7.5f;
    [Tooltip("Duración del impulso y de la vuelta")]
    public float dodgeDuration = 0.42f;
    public float dodgeCooldown = 0.6f;
    [Tooltip("Segundos en los que las aves no lo pueden golpear")]
    public float dodgeInvulnerable = 0.35f;
    [Tooltip("Combustible que gasta en el aire si tiene (en el agua no gasta). Sin combustible igual esquiva")]
    public float dodgeEnergyCost = 4f;

    public bool IsDodging { get { return Time.time < dodgeEndTime; } }
    public bool IsDodgeInvulnerable { get { return Time.time < dodgeInvulnerableUntil; } }
    // -1 = a la izquierda, 1 = a la derecha
    public event System.Action<int> OnDodge;

    float dodgeEndTime = -10f, dodgeInvulnerableUntil = -10f, nextDodgeTime, dodgeStartTime;
    int dodgeDir, dodgeRequest;
    float lastTapLeft = -10f, lastTapRight = -10f;
    Vector3 dodgeSide;

    // En Update: lee las teclas (el impulso se aplica en FixedUpdate)
    void ReadDodgeInput()
    {
        int req = 0;
        if (Input.GetKeyDown(dodgeLeftKey)) req = -1;
        else if (Input.GetKeyDown(dodgeRightKey)) req = 1;
        if (dodgeOnDoubleTap)
        {
            float now = Time.time;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (now - lastTapLeft < doubleTapTime) { req = -1; lastTapLeft = -10f; }
                else lastTapLeft = now;
            }
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (now - lastTapRight < doubleTapTime) { req = 1; lastTapRight = -10f; }
                else lastTapRight = now;
            }
        }
        if (req != 0) dodgeRequest = req;
    }

    // En FixedUpdate: arranca el esquive
    void TryDodge()
    {
        if (dodgeRequest == 0) return;
        int dir = dodgeRequest;
        dodgeRequest = 0;
        if (Time.time < nextDodgeTime || IsStranded || IsTurningBack || InputLocked) return;
        bool air = !isInWater;
        if (air) currentJetpackEnergy = Mathf.Max(0f, currentJetpackEnergy - dodgeEnergyCost);

        // El lado del pez en este momento (horizontal): el impulso va siempre de lado, mire a donde mire
        dodgeSide = new Vector3(transform.right.x, 0f, transform.right.z).normalized * dir;
        dodgeDir = dir;
        dodgeStartTime = Time.time;
        dodgeEndTime = Time.time + dodgeDuration;
        dodgeInvulnerableUntil = Time.time + dodgeInvulnerable;
        nextDodgeTime = Time.time + dodgeCooldown;
        fovKick = Mathf.Max(fovKick, 8f);
        Sfx.Play2D(Sonido.Dash, 0.7f, air ? 1.25f : 1.05f, 0.05f, 0.1f);
        ApplyDodgeMotion();
        OnDodge?.Invoke(dir);
    }

    // En FixedUpdate, después del nado o el vuelo: la velocidad de lado sale de golpe a tope y frena enseguida
    // (v(t) = V·(1 - t/T)², que recorre justo dodgeDistance si V = 3·D / T). Solo se toca el movimiento de lado: lo
    // que iba hacia adelante, hacia atrás, hacia arriba o cayendo se conserva.
    void ApplyDodgeMotion()
    {
        if (!IsDodging || dodgeSide.sqrMagnitude < 0.01f) return;
        float T = Mathf.Max(0.05f, dodgeDuration);
        float k = Mathf.Clamp01((Time.time - dodgeStartTime) / T);
        float peak = 3f * dodgeDistance / T;
        float lateral = peak * (1f - k) * (1f - k);
        Vector3 v = rb.linearVelocity;
        v -= dodgeSide * Vector3.Dot(v, dodgeSide);
        v += dodgeSide * lateral;
        rb.linearVelocity = v;
    }

    // Vuelta completa alrededor del eje de avance (grados): rápida al empezar, como el impulso
    float DodgeRoll()
    {
        if (!IsDodging) return 0f;
        float k = Mathf.Clamp01((Time.time - dodgeStartTime) / Mathf.Max(0.05f, dodgeDuration));
        float ease = 1f - (1f - k) * (1f - k) * (1f - k);
        return -dodgeDir * 360f * ease;
    }

    // Giro de la cámara: en primera persona toda la pantalla da la vuelta completa; en tercera persona la cámara no
    // gira (solo gira el modelo del pez)
    float DodgeCameraRoll()
    {
        if (!IsDodging || isThirdPerson) return 0f;
        return DodgeRoll();
    }
}
