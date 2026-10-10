using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Mejora "Sonar" en el visor del casco:
//  - Nivel I: un anillo cian que late sobre cada ave que está cazando un pez, con la distancia.
//    Si está fuera de la vista, una flechita en un óvalo interior del visor señala hacia ella.
//  - Nivel II: además marca en naranja los peces que ya van atrapados en un pico o en unas garras.
// Lo crea HelmetVisorHUD dentro de "VisorOverlay" (no se balancea con el casco: sigue al mundo).
public class VisorSonarHUD : MonoBehaviour
{
    const float RingSize = 30f, OuterSize = 46f;
    const float OvalX = 0.46f, OvalY = 0.62f;     // óvalo de las flechas (fracción de la mitad de la pantalla)
    const int MaxMarkers = 16;

    [HideInInspector] public PlayerController_Base player;

    Color accent = new Color(0.41f, 0.87f, 0.9f, 1f);
    static readonly Color Warning = new Color(1f, 0.62f, 0.2f, 1f);

    RectTransform root;
    readonly List<Marker> pool = new List<Marker>();
    int used;
    static Texture2D ringTex, chevronTex;

    class Marker
    {
        public RectTransform rt;
        public RawImage ring, outer, chevron;
        public TextMeshProUGUI label;
    }

    public void Build(RectTransform parent, Color accentColor)
    {
        accent = accentColor;
        if (ringTex == null)
        {
            ringTex = FXFactory.MakeRadial(96, d => Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.08f), "FX_SonarAnillo");
            chevronTex = MakeChevron(48);
        }
        GameObject go = new GameObject("Sonar", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        root = (RectTransform)go.transform;
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.SetAsFirstSibling();   // debajo de la retícula
    }

    void LateUpdate()
    {
        if (root == null) return;
        used = 0;
        int level = Upgrades.Level(UpgradeId.Sonar);
        Camera cam = player != null ? player.PlayerCamera : Camera.main;
        bool active = level > 0 && cam != null && player != null && !player.isDead && !GameDirector.InCinematic && !PauseMenu.IsPaused;
        if (active)
        {
            float pulse = Mathf.Repeat(Time.time * 1.6f, 1f);
            foreach (BirdAI bird in BirdAI.All)
            {
                if (bird == null || !bird.IsAlive || bird.IsArriving) continue;
                if (used >= MaxMarkers) break;
                if (bird.IsHunting)
                    Place(cam, bird.transform.position, accent, pulse);
                if (level >= 2)
                {
                    IReadOnlyList<FishAI> fishes = bird.CarriedFishes;
                    for (int i = 0; i < fishes.Count && used < MaxMarkers; i++)
                        if (fishes[i] != null) Place(cam, fishes[i].transform.position, Warning, Mathf.Repeat(pulse + 0.5f, 1f));
                }
            }
        }
        for (int i = used; i < pool.Count; i++)
            if (pool[i].rt.gameObject.activeSelf) pool[i].rt.gameObject.SetActive(false);
    }

    void Place(Camera cam, Vector3 world, Color color, float pulse)
    {
        Rect r = root.rect;
        Vector3 vp = cam.WorldToViewportPoint(world);
        bool behind = vp.z < 0f;
        if (behind) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; }
        Vector2 pos = new Vector2((vp.x - 0.5f) * r.width, (vp.y - 0.5f) * r.height);
        bool onScreen = !behind && Mathf.Abs(pos.x) < r.width * 0.5f - 10f && Mathf.Abs(pos.y) < r.height * 0.5f - 10f;
        float dist = Vector3.Distance(cam.transform.position, world);

        Marker m = Get();
        m.rt.gameObject.SetActive(true);
        if (onScreen)
        {
            m.rt.anchoredPosition = pos;
            m.ring.gameObject.SetActive(true);
            m.outer.gameObject.SetActive(true);
            m.chevron.gameObject.SetActive(false);
            m.ring.color = new Color(color.r, color.g, color.b, 0.85f);
            m.outer.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(RingSize, OuterSize, pulse);
            m.outer.color = new Color(color.r, color.g, color.b, 0.5f * (1f - pulse));
            m.label.rectTransform.anchoredPosition = new Vector2(0f, -RingSize * 0.5f - 8f);
        }
        else
        {
            // Flecha sobre un óvalo interior (no choca con las flechas rojas del borde de GameHUD)
            Vector2 dir = pos.sqrMagnitude > 0.01f ? pos.normalized : Vector2.down;
            float rx = r.width * 0.5f * OvalX, ry = r.height * 0.5f * OvalY;
            float ang = Mathf.Atan2(dir.y * rx, dir.x * ry);
            m.rt.anchoredPosition = new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry);
            m.ring.gameObject.SetActive(false);
            m.outer.gameObject.SetActive(false);
            m.chevron.gameObject.SetActive(true);
            m.chevron.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            m.chevron.color = new Color(color.r, color.g, color.b, 0.6f + 0.4f * (1f - pulse));
            m.label.rectTransform.anchoredPosition = -dir * 18f;
        }
        m.label.text = Mathf.RoundToInt(dist) + " m";
        m.label.color = new Color(color.r, color.g, color.b, 0.9f);
    }

    Marker Get()
    {
        if (used < pool.Count) return pool[used++];
        Marker m = new Marker();
        GameObject go = new GameObject("MarcaSonar", typeof(RectTransform));
        go.layer = root.gameObject.layer;
        m.rt = (RectTransform)go.transform;
        m.rt.SetParent(root, false);
        m.rt.anchorMin = m.rt.anchorMax = new Vector2(0.5f, 0.5f);
        m.rt.sizeDelta = Vector2.zero;
        m.outer = Raw("Pulso", m.rt, ringTex, OuterSize);
        m.ring = Raw("Anillo", m.rt, ringTex, RingSize);
        m.chevron = Raw("Flecha", m.rt, chevronTex, 16f);
        GameObject tgo = new GameObject("Distancia", typeof(RectTransform));
        tgo.layer = root.gameObject.layer;
        tgo.transform.SetParent(m.rt, false);
        m.label = tgo.AddComponent<TextMeshProUGUI>();
        if (MenuUI.BodyFont != null) m.label.font = MenuUI.BodyFont;
        m.label.fontSize = 9f;
        m.label.characterSpacing = 4f;
        m.label.alignment = TextAlignmentOptions.Center;
        m.label.textWrappingMode = TextWrappingModes.NoWrap;
        m.label.raycastTarget = false;
        m.label.rectTransform.sizeDelta = new Vector2(60f, 12f);
        pool.Add(m);
        used++;
        return m;
    }

    static RawImage Raw(string name, RectTransform parent, Texture tex, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        RawImage img = go.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = Vector2.one * size;
        return img;
    }

    // Flechita (punta hacia +X) con borde suave
    static Texture2D MakeChevron(int s)
    {
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.name = "FX_SonarFlecha";
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s * 2f - 1f;
                float v = (y + 0.5f) / s * 2f - 1f;
                // Punta en (0.85, 0), alas hacia (-0.6, ±0.7) con una muesca atrás
                float edge = Mathf.Abs(v) - (0.85f - u) * 0.8f;
                float notch = (u + 0.35f) - Mathf.Abs(v) * 0.6f;
                float a = Mathf.Clamp01(-edge * s * 0.25f) * Mathf.Clamp01(notch * s * 0.25f) * (u > -0.7f ? 1f : 0f);
                px[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
