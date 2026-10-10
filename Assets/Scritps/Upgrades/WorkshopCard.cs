using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Un plano holográfico del Taller: flota sobre la mesa, se ilumina al pasar el mouse
// y se levanta al elegirlo. Clic = elegir; clic en el ya elegido = instalar.
public class WorkshopCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public int index;
    public System.Action<int> onClick;

    // Partes que se animan (las pone WorkshopScene al construir el plano)
    public Image glow;
    public Image beam;
    public CanvasGroup group;
    public Vector2 basePosition;

    public bool Selected { get; set; }
    public bool Dimmed { get; set; }
    public bool Interactive { get; set; }

    bool hover;
    float phase;
    float appear;   // 0..1 entrada con rebote

    void Awake()
    {
        phase = Random.Range(0f, 10f);
        Interactive = true;
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; }
    public void OnPointerExit(PointerEventData e) { hover = false; }

    public void OnPointerClick(PointerEventData e)
    {
        if (Interactive && onClick != null) onClick(index);
    }

    // Reinicia la animación de entrada (retraso en segundos)
    public void Appear(float delay)
    {
        appear = -delay;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime + phase;
        appear = Mathf.Min(1f, appear + dt * 2.6f);
        float a = Mathf.Clamp01(appear);

        bool lit = Interactive && (hover || Selected);
        RectTransform rt = (RectTransform)transform;

        // Flota suave; el elegido sube un poco
        float lift = Selected ? 16f : (lit ? 6f : 0f);
        Vector2 target = basePosition + new Vector2(0f, Mathf.Sin(t * 1.4f) * 3f + lift - (1f - a) * 40f);
        rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, 1f - Mathf.Exp(-12f * dt));

        float s = (Selected ? 1.05f : (lit ? 1.025f : 1f)) * Mathf.Lerp(0.85f, 1f, EaseOutBack(a));
        float cur = Mathf.Lerp(rt.localScale.x, s, 1f - Mathf.Exp(-14f * dt));
        rt.localScale = new Vector3(cur, cur, 1f);

        if (group != null)
        {
            float alpha = Dimmed ? 0.5f : 1f;
            // Parpadeo de holograma al aparecer
            if (a < 1f) group.alpha = alpha * a * (0.7f + 0.3f * Mathf.Sin(t * 60f));
            else group.alpha = Mathf.MoveTowards(group.alpha, alpha, dt * 4f);
        }
        if (glow != null)
        {
            Color c = glow.color;
            float target01 = Selected ? 0.75f + Mathf.Sin(t * 5f) * 0.15f : (lit ? 0.4f : 0.08f);
            c.a = Mathf.MoveTowards(c.a, target01, dt * 3f);
            glow.color = c;
        }
        if (beam != null)
        {
            Color c = beam.color;
            c.a = (Selected ? 0.32f : 0.16f) * (0.85f + 0.15f * Mathf.Sin(t * 7f)) * a * (Dimmed ? 0.5f : 1f);
            beam.color = c;
        }
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
