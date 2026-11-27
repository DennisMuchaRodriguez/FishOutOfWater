using TMPro;
using UnityEngine;

// El pez de un compañero en el modo cooperativo: una copia del modelo del pez local (traje y escafandra) con el color
// de su casilla y su nombre encima. Lo mueve lo que manda la red (posición, giro del modelo, si vuela o nada, si está
// caído). En el anfitrión lleva además un PlayerController_Base "representante" para que las aves lo persigan.
public class CoopRemotePlayer : MonoBehaviour
{
    public int Slot { get; private set; }
    public PlayerController_Base Proxy { get; private set; }
    public bool Dead { get; private set; }
    public float Armor01 { get; private set; } = 1f;
    public string PlayerName { get; private set; } = "";
    public bool HasState { get { return netTime > 0f; } }

    Transform model;
    ParticleSystem jet;
    TextMeshPro label;
    Vector3 netPos, netVel;
    Quaternion netBody = Quaternion.identity, netModel = Quaternion.identity;
    float netTime = -1f;
    bool jetting;

    public static CoopRemotePlayer Create(int slot, string playerName, PlayerController_Base template)
    {
        GameObject go = new GameObject("Companero_" + (slot + 1));
        CoopRemotePlayer rp = go.AddComponent<CoopRemotePlayer>();
        rp.Slot = slot;

        // Cuerpo para las aves (no choca con nada: es un disparador)
        Rigidbody body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        CapsuleCollider col = go.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.radius = 0.6f;
        col.height = 1.6f;
        col.center = Vector3.up * 0.4f;

        rp.Proxy = PlayerController_Base.AddNetworkProxy(go, slot);
        if (template != null) rp.Proxy.maxArmor = template.maxArmor;

        // Copia del modelo del pez local
        if (template != null && template.modeloPez != null)
        {
            GameObject copy = Instantiate(template.modeloPez.gameObject, go.transform);
            copy.name = "Modelo";
            CoopVisuals.StripLogic(copy);
            rp.model = copy.transform;
            if (template.jetpackWaterEffect != null)
            {
                foreach (ParticleSystem ps in copy.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps.gameObject.name == template.jetpackWaterEffect.gameObject.name) { rp.jet = ps; break; }
                }
            }
            if (rp.jet != null) rp.jet.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            // Su estilo (o, con el original, el color de su casilla para distinguirlo)
            int skin = CoopSession.SkinOf(slot);
            SkinCatalog.Apply(copy, skin);
            if (skin == 0) CoopVisuals.TintSuit(copy, CoopSession.SlotColors[Mathf.Clamp(slot, 0, 3)]);
        }

        rp.label = CoopVisuals.NameTag(go.transform, Vector3.up * 1.7f, playerName, CoopSession.SlotColors[Mathf.Clamp(slot, 0, 3)], 3.2f);
        rp.SetName(playerName);
        return rp;
    }

    public void SetName(string playerName)
    {
        PlayerName = playerName ?? "";
        RefreshLabel();
    }

    void RefreshLabel()
    {
        if (label == null) return;
        label.text = Dead ? PlayerName.ToUpperInvariant() + "\n<size=60%><color=#FF5A4A>CAÍDO</color></size>" : PlayerName.ToUpperInvariant();
    }

    // Banderas: 1 caído, 2 en el agua, 4 propulsor, 8 turbo, 16 esquive
    public void ApplyState(Vector3 position, Quaternion bodyRotation, Quaternion modelRotation, Vector3 velocity, byte flags, float armor01)
    {
        bool first = netTime < 0f;
        netPos = position;
        netBody = bodyRotation;
        netModel = modelRotation;
        netVel = velocity;
        netTime = Time.time;
        bool dead = (flags & 1) != 0;
        jetting = (flags & 4) != 0 && !dead;
        Armor01 = armor01;
        if (dead != Dead)
        {
            Dead = dead;
            RefreshLabel();
        }
        if (Proxy != null) Proxy.ApplyProxyState(dead, (flags & 2) != 0, velocity, armor01);
        if (first)
        {
            transform.SetPositionAndRotation(position, bodyRotation);
            if (model != null) model.rotation = modelRotation;
        }
    }

    void Update()
    {
        if (netTime < 0f) return;
        float dt = Time.deltaTime;
        Vector3 target = netPos + netVel * Mathf.Clamp(Time.time - netTime, 0f, 0.25f);
        float k = 1f - Mathf.Exp(-16f * dt);
        transform.position = (target - transform.position).sqrMagnitude > 400f ? target : Vector3.Lerp(transform.position, target, k);
        transform.rotation = Quaternion.Slerp(transform.rotation, netBody, k);
        if (model != null) model.rotation = Quaternion.Slerp(model.rotation, netModel, k);

        if (jet != null)
        {
            if (jetting && !jet.isEmitting) jet.Play(true);
            else if (!jetting && jet.isEmitting) jet.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void LateUpdate()
    {
        CoopVisuals.FaceCamera(label);
    }
}

// Ayudas visuales del cooperativo (también las usa la sala del Taller)
public static class CoopVisuals
{
    static readonly string[] FishParts = { "Head", "Ojos", "Body", "Aleta" };
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    // Quita scripts, colisionadores, cámaras y luces de audio de una copia (solo queda lo que se ve)
    public static void StripLogic(GameObject copy)
    {
        foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(mb);
        foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        foreach (Rigidbody r in copy.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(r);
        foreach (Camera cam in copy.GetComponentsInChildren<Camera>(true)) Object.Destroy(cam.gameObject);
        foreach (AudioListener al in copy.GetComponentsInChildren<AudioListener>(true)) Object.Destroy(al);
    }

    // El traje (no el pez ni la escafandra) toma el color del jugador
    public static void TintSuit(GameObject model, Color color)
    {
        if (model == null) return;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            string n = r.gameObject.name;
            bool skip = n.IndexOf("Vidrio", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (string part in FishParts)
                if (n.StartsWith(part, System.StringComparison.OrdinalIgnoreCase)) skip = true;
            if (skip) continue;
            Material m = r.sharedMaterial;
            if (m == null || SkinCatalog.PartOf(m) == 1) continue;   // el cuerpo del pez no se tiñe
            // El color base del shader que tenga (el de Arnold del modelo usa _BASE_COLOR; el Lit, _BaseColor)
            int[] ids = SkinCatalog.BaseColorIds(m);
            if (ids.Length == 0) ids = new[] { BaseColorId, ColorId };
            Color baseColor = m.HasProperty(ids[0]) ? m.GetColor(ids[0]) : Color.white;
            Color tinted = Color.Lerp(baseColor, color * new Color(1.15f, 1.15f, 1.15f, 1f), 0.45f);
            tinted.a = baseColor.a;
            r.GetPropertyBlock(block);
            foreach (int id in ids) block.SetColor(id, tinted);
            r.SetPropertyBlock(block);
        }
    }

    // Nombre flotando sobre un pez
    public static TextMeshPro NameTag(Transform parent, Vector3 localPos, string text, Color color, float size)
    {
        GameObject go = new GameObject("Nombre");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        if (MenuUI.TitleFont != null) tmp.font = MenuUI.TitleFont;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 10, 25, 220);
        tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    public static void FaceCamera(TextMeshPro label)
    {
        if (label == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Transform t = label.transform;
        t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, Vector3.up);
        // Se lee igual de cerca que de lejos (hasta un tope)
        float d = Vector3.Distance(cam.transform.position, t.position);
        t.localScale = Vector3.one * Mathf.Clamp(d / 12f, 0.35f, 3f);
    }
}
