using UnityEngine;

// Giro de esquive del pez (parte de PlayerController_Base), como el que hacen las aves y las naves de los juegos de
// combate aéreo: un tirón de lado con una vuelta completa sobre sí mismo.
//  - Doble toque de A o D (o las flechas), o las teclas Z (izquierda) y X (derecha).
//  - Durante un instante las aves no lo pueden golpear (se le pasan de largo).
//  - En el aire gasta un poco de combustible; en el agua no gasta (es un quiebre de lado nadando).
//  - En primera persona la cámara se ladea un poco hacia el lado del giro (no da la vuelta entera, para no marear).
public partial class PlayerController_Base
{
    [Header("Giro de esquive - NUEVO")]
    [Tooltip("Doble toque de A / D (o de las flechas) para esquivar")]
    public bool dodgeOnDoubleTap = true;
    public KeyCode dodgeLeftKey = KeyCode.Z;
    public KeyCode dodgeRightKey = KeyCode.X;
    [Tooltip("Segundos máximos entre los dos toques")]
    public float doubleTapTime = 0.28f;
    [Tooltip("Velocidad de lado que gana al esquivar (m/s)")]
    public float dodgeSpeed = 13f;
    [Tooltip("Duración de la vuelta")]
    public float dodgeDuration = 0.42f;
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
    int dodgeDir, dodgeRequest;
    float lastTapLeft = -10f, lastTapRight = -10f;

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

        Vector3 side = transform.right * dir;
        Vector3 v = rb.linearVelocity;
        // Si iba hacia el otro lado, primero frena eso; luego suma el tirón
        float lateral = Vector3.Dot(v, side);
        if (lateral < 0f) v -= side * lateral;
        v += side * dodgeSpeed;
        if (air && !isGrounded) v.y = Mathf.Max(v.y, 1.5f);
        rb.linearVelocity = v;

        dodgeDir = dir;
        dodgeStartTime = Time.time;
        dodgeEndTime = Time.time + dodgeDuration;
        dodgeInvulnerableUntil = Time.time + dodgeInvulnerable;
        nextDodgeTime = Time.time + dodgeCooldown;
        fovKick = Mathf.Max(fovKick, 6f);
        Sfx.Play2D(Sonido.Dash, 0.6f, air ? 1.25f : 1.05f, 0.05f, 0.1f);
        OnDodge?.Invoke(dir);
    }

    // Vuelta completa del modelo alrededor del eje de avance (grados)
    float DodgeRoll()
    {
        if (!IsDodging) return 0f;
        float k = Mathf.Clamp01((Time.time - dodgeStartTime) / Mathf.Max(0.05f, dodgeDuration));
        return -dodgeDir * 360f * Mathf.SmoothStep(0f, 1f, k);
    }

    // Ladeo de la cámara durante el giro (sube y vuelve)
    float DodgeCameraRoll()
    {
        if (!IsDodging) return 0f;
        float k = Mathf.Clamp01((Time.time - dodgeStartTime) / Mathf.Max(0.05f, dodgeDuration));
        return -dodgeDir * dodgeCameraRoll * Mathf.Sin(k * Mathf.PI);
    }
}
