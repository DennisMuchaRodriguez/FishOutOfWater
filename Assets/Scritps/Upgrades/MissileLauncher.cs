using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Arma secundaria: misiles teledirigidos (mejora del Taller, tecla E).
// E dispara una salva con todos los misiles cargados, uno por hombro, cada uno a un ave distinta si hay varias.
// El soporte se recarga misil por misil: el soporte completo tarda 'reload' segundos (I: 3 en 10 s, II: 4 en 8 s).
// CONTRATO para el HUD: Instance, Unlocked, Capacity, Loaded, Reload01 y Fired.
// Lo agrega PlayerUpgrades al jugador; sin la mejora no hace nada.
public class MissileLauncher : MonoBehaviour
{
    public static MissileLauncher Instance { get; private set; }

    public bool Unlocked { get; protected set; }
    public int Capacity { get; protected set; }
    public int Loaded { get; protected set; }
    // Progreso de la recarga del siguiente misil (0..1); 1 si está lleno
    public float Reload01 { get; protected set; }

    public event System.Action Fired;

    [Header("Misiles teledirigidos")]
    public KeyCode fireKey = KeyCode.E;
    public int damage = 35;
    [Tooltip("Daño a las aves cerca de la explosión (fracción del daño)")]
    public float splashFraction = 0.4f;
    public float splashRadius = 3f;
    public float speed = 30f;
    [Tooltip("Grados por segundo que puede girar buscando al ave")]
    public float turnRate = 240f;
    public float lifetime = 4.5f;
    [Tooltip("Distancia máxima a la que busca aves")]
    public float seekRange = 95f;
    [Tooltip("Ángulo (desde donde apuntas) dentro del que busca aves")]
    public float seekAngle = 75f;
    public float salvoInterval = 0.12f;

    float reloadTime = 10f;
    float reloadTimer;
    bool firing;
    PlayerController_Base player;
    PlayerShooting shooting;
    Material bodyMaterial, trailMaterial;

    protected virtual void Awake() { Instance = this; }
    protected virtual void OnDestroy() { if (Instance == this) Instance = null; }

    protected void RaiseFired() { if (Fired != null) Fired(); }

    // level 0 = sin misiles, 1 = 3 misiles / 10 s, 2 = 4 misiles / 8 s
    public void Configure(int level)
    {
        bool was = Unlocked;
        Unlocked = level > 0;
        Capacity = level >= 2 ? 4 : (level == 1 ? 3 : 0);
        reloadTime = level >= 2 ? 8f : 10f;
        if (Unlocked && !was) Loaded = Capacity;      // se estrena con el soporte lleno
        Loaded = Mathf.Clamp(Loaded, 0, Capacity);
        Reload01 = Loaded >= Capacity ? 1f : Mathf.Clamp01(reloadTimer / PerMissile);
    }

    float PerMissile { get { return reloadTime / Mathf.Max(1, Capacity); } }

    void Start()
    {
        player = GetComponent<PlayerController_Base>();
        shooting = GetComponent<PlayerShooting>();
        Material template = shooting != null ? shooting.fxMaterialTemplate : null;
        bodyMaterial = FXFactory.ParticleMaterialAlpha(template, FXFactory.White, new Color(1f, 0.55f, 0.25f, 1f));
        trailMaterial = FXFactory.ParticleMaterial(template, FXFactory.SoftDot, Color.white);
    }

    void Update()
    {
        if (!Unlocked || Capacity <= 0) return;
        float dt = Time.deltaTime;

        // Recarga misil por misil
        if (Loaded < Capacity)
        {
            reloadTimer += dt;
            if (reloadTimer >= PerMissile)
            {
                reloadTimer = 0f;
                Loaded++;
            }
            Reload01 = Loaded >= Capacity ? 1f : Mathf.Clamp01(reloadTimer / PerMissile);
        }
        else
        {
            reloadTimer = 0f;
            Reload01 = 1f;
        }

        bool blocked = PauseMenu.IsPaused || PauseMenu.InputBlockedThisFrame || GameDirector.InCinematic ||
                       (player != null && (player.isDead || player.InputLocked)) || (shooting != null && !shooting.enabled);
        if (!blocked && !firing && Loaded > 0 && Input.GetKeyDown(fireKey))
            StartCoroutine(Salvo());
    }

    IEnumerator Salvo()
    {
        firing = true;
        int count = Loaded;
        List<BirdAI> targets = PickTargets(count);
        for (int i = 0; i < count && Loaded > 0; i++)
        {
            if (player != null && player.isDead) break;
            BirdAI target = targets.Count > 0 ? targets[i % targets.Count] : null;
            Launch(target, i % 2 == 0 ? 1f : -1f);
            Loaded--;
            RaiseFired();
            yield return new WaitForSeconds(salvoInterval);
        }
        firing = false;
    }

    // Aves delante (hacia donde apuntas), las más cercanas al centro primero
    List<BirdAI> PickTargets(int count)
    {
        Vector3 origin = transform.position;
        Vector3 forward = AimForward();
        List<BirdAI> found = new List<BirdAI>();
        List<float> scores = new List<float>();
        foreach (BirdAI b in BirdAI.All)
        {
            if (b == null || !b.IsAlive || b.IsArriving) continue;
            Vector3 to = b.transform.position - origin;
            float dist = to.magnitude;
            if (dist > seekRange || dist < 0.5f) continue;
            float angle = Vector3.Angle(forward, to);
            if (angle > seekAngle) continue;
            float score = angle * 1.5f + dist * 0.4f - (b.CarriedCount > 0 ? 25f : 0f) - (b.IsBoss ? 10f : 0f);
            int at = 0;
            while (at < scores.Count && scores[at] <= score) at++;
            scores.Insert(at, score);
            found.Insert(at, b);
        }
        if (found.Count > count) found.RemoveRange(count, found.Count - count);
        return found;
    }

    Vector3 AimForward()
    {
        Camera cam = player != null ? player.PlayerCamera : null;
        if (cam == null) cam = Camera.main;
        return cam != null ? cam.transform.forward : transform.forward;
    }

    void Launch(BirdAI target, float side)
    {
        Vector3 forward = AimForward();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        if (right.sqrMagnitude < 0.01f) right = transform.right;
        // Sale del hombro, un poco hacia arriba y hacia fuera
        Vector3 start = transform.position + Vector3.up * 0.9f + right * side * 0.55f + forward * 0.3f;
        Vector3 dir = (forward + Vector3.up * 0.55f + right * side * 0.35f).normalized;

        GameObject go = new GameObject("Misil");
        go.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir));
        HomingMissile m = go.AddComponent<HomingMissile>();
        m.Setup(this, shooting, target, dir, bodyMaterial, trailMaterial);
    }

    // Lo llaman los misiles: el nuevo objetivo si el suyo murió (el más cercano por delante del misil)
    public BirdAI Retarget(Vector3 position, Vector3 forward)
    {
        BirdAI best = null;
        float bestScore = float.MaxValue;
        foreach (BirdAI b in BirdAI.All)
        {
            if (b == null || !b.IsAlive || b.IsArriving) continue;
            Vector3 to = b.transform.position - position;
            float dist = to.magnitude;
            if (dist > seekRange * 0.7f) continue;
            float angle = Vector3.Angle(forward, to);
            if (angle > 100f) continue;
            float score = dist + angle * 0.5f;
            if (score < bestScore) { bestScore = score; best = b; }
        }
        return best;
    }
}
