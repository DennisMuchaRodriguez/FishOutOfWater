using UnityEngine;
using UnityEngine.EventSystems;

// Animación de los botones de cómic: crece y se ladea al pasar el mouse, y "rebota" al hacer clic
public class MenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler
{
    bool hover;
    float punch;
    float tilt;

    void Awake()
    {
        // Cada botón se ladea un poquito distinto (más "dibujado a mano")
        tilt = Random.Range(-2.2f, 2.2f);
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; }
    public void OnPointerExit(PointerEventData e) { hover = false; }
    public void OnSelect(BaseEventData e) { hover = true; }
    public void OnDeselect(BaseEventData e) { hover = false; }
    public void OnPointerDown(PointerEventData e) { punch = 1f; }

    void OnDisable()
    {
        hover = false;
        punch = 0f;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
        float target = (hover ? 1.07f : 1f) - punch * 0.08f;
        float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-16f * dt));
        transform.localScale = new Vector3(s, s, 1f);
        float rot = hover ? tilt : 0f;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(0f, 0f, rot), 1f - Mathf.Exp(-12f * dt));
    }
}
