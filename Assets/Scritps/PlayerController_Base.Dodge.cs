using UnityEngine;

// Giro de esquive del pez (parte de PlayerController_Base), como el que hacen las aves y las naves de los juegos de
// combate aéreo: se desplaza de golpe varios metros hacia un lado mientras da una vuelta completa sobre sí mismo.
//  - Tecla E: el lado lo eliges con A / D (o las flechas) mantenidas; sin dirección, va hacia el lado de la mira
//    (y si la mira está al centro, alterna). También doble toque de A o D.
//  - El desplazamiento es fuerte y suave: acelera, recorre Dodge Distance metros y frena en Dodge Duration.
//  - Durante un instante las aves no lo pueden golpear (se le pasan de largo).
//  - En el aire gasta un poco de combustible; en el agua no gasta (es un quiebre de lado nadando).
//  - En tercera persona la cámara da la vuelta completa (360°) con el pez; en primera persona solo se ladea un poco.
public partial class PlayerController_Base
{
    [Header("Giro de esquive - NUEVO")]
    [Tooltip("Doble toque de A / D (o de las flechas) para esquivar")]
    public bool dodgeOnDoubleTap = true;
    [Tooltip("Tecla del giro (el lado se elige con A / D)")]
    public KeyCode dodgeKey = KeyCode.E;
    [Tooltip("Segundos máximos entre los dos toques")]
    public float doubleTapTime = 0.28f;
    [Tooltip("Metros que se desplaza hacia el lado durante el giro")]
    public float dodgeDistance = 7f;
    [Tooltip("Duración del giro (la vuelta y el desplazamiento)")]
    public float dodgeDuration = 0.45f;
    public float dodgeCooldown = 0.75f;
    [Tooltip("Segundos en los que las aves no lo pueden golpear")]
    public float dodgeInvulnerable = 0.35f;
    [Tooltip("Combustible que gasta en el aire (en el agua no gasta)")]
    public float dodgeEnergyCost = 6f;
    [Tooltip("Inclinación de la cámara en primera persona durante el giro (grados)")]
    public float dodgeCameraRoll = 22f;

    public bool IsDodging { get { return Time.time < dodgeEndTime; } }
    public bool IsDodgeInvulnerable { get { return Time.time < dodgeInvulnerableUntil; } }
    // -1 = a la izquierda, 1 = a la derecha
    public event System.Action<int> OnDodge;

    float dodgeEndTime = -10f, dodgeInvulnerableUntil = -10f, nextDodgeTime, dodgeStartTime;
    int dodgeDir, dodgeRequest, lastDodgeDir = -1;
    float lastTapLeft = -10f, lastTapRight = -10f;
    Vector3 dodgeSide;

    // En Update: lee las teclas (el impulso se aplica en FixedUpdate)
    void ReadDodgeInput()
    {
        int req = 0;
        if (Input.GetKeyDown(dodgeKey))
        {
            // El lado: A / D mantenidas; si no, hacia donde está la mira; si está al centro, alterna
            float h = Input.GetAxisRaw("Horizontal");
            if (h < -0.1f) req = -1;
            else if (h > 0.1f) req = 1;
            else
            {
                float mx = Input.mousePosition.x / Mathf.Max(1f, Screen.width) - 0.5f;
                req = mx < -0.08f ? -1 : (mx > 0.08f ? 1 : -lastDodgeDir);
            }
        }
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

    // En FixedUpdate: el tirón de lado
    void TryDodge()
    {
        if (dodgeRequest == 0) return;
        int dir = dodgeRequest;
        dodgeRequest = 0;
        if (Time.time < nextDodgeTime || IsStranded || IsTurningBack || InputLocked) return;
        bool air = !isInWater;
        if (air && currentJetpackEnergy < dodgeEnergyCost) return;
        if (air) currentJetpackEnergy -= dodgeEnergyCost;

        dodgeSide = new Vector3(transform.right.x, 0f, transform.right.z).normalized * dir;
        if (air && !isGrounded)
        {
            Vector3 v = rb.linearVelocity;
            v.y = Mathf.Max(v.y, 1.5f);
            rb.linearVelocity = v;
        }

        dodgeDir = dir;
        lastDodgeDir = dir;
        dodgeStartTime = Time.time;
        dodgeEndTime = Time.time + dodgeDuration;
        dodgeInvulnerableUntil = Time.time + dodgeInvulnerable;
        nextDodgeTime = Time.time + dodgeCooldown;
        fovKick = Mathf.Max(fovKick, 6f);
        Sfx.Play2D(Sonido.Dash, 0.6f, air ? 1.25f : 1.05f, 0.05f, 0.1f);
        OnDodge?.Invoke(dir);
    }

    // En FixedUpdate, después del nado o el vuelo: la velocidad hacia el lado sigue una curva suave (sale, recorre
    // dodgeDistance metros y frena). Lo que iba hacia adelante y hacia arriba se conserva.
    void ApplyDodgeMotion()
    {
        if (!IsDodging || dodgeSide.sqrMagnitude < 0.01f) return;
        float T = Mathf.Max(0.05f, dodgeDuration);
        float k = Mathf.Clamp01((Time.time - dodgeStartTime) / T);
        // v(t) = V·sen(π·t/T) recorre exactamente dodgeDistance si V = π·D / (2·T)
        float peak = Mathf.PI * dodgeDistance / (2f * T);
        float lateral = peak * Mathf.Sin(k * Mathf.PI);
        Vector3 v = rb.linearVelocity;
        v -= dodgeSide * Vector3.Dot(v, dodgeSide);
        v += dodgeSide * lateral;
        rb.linearVelocity = v;
    }

    // Vuelta completa del modelo alrededor del eje de avance (grados)
    float DodgeRoll()
    {
        if (!IsDodging) return 0f;
        float k = Mathf.Clamp01((Time.time - dodgeStartTime) / Mathf.Max(0.05f, dodgeDuration));
        return -dodgeDir * 360f * Mathf.SmoothStep(0f, 1f, k);
    }

    // Giro de la cámara: en tercera persona la vuelta completa (como el pez); en primera, un ladeo que sube y vuelve
    float DodgeCameraRoll()
    {
        if (!IsDodging) return 0f;
        if (isThirdPerson) return DodgeRoll();
        float k = Mathf.Clamp01((Time.time - dodgeStartTime) / Mathf.Max(0.05f, dodgeDuration));
        return -dodgeDir * dodgeCameraRoll * Mathf.Sin(k * Mathf.PI);
    }
}
