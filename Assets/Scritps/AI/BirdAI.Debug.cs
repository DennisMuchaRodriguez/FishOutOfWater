using UnityEngine;

// Depuración de la IA (SOLO EN EL EDITOR; no existe en el juego publicado):
// sobre cada ave, lo que está haciendo según su árbol de comportamiento ("Patrullar el lago", "Acechar",
// "Lanzarse en picada", "Escapar con la presa", "Huir herida"...), con un color por tipo de acción, y una línea
// hacia el pez que persigue. Se ve en la vista Scene y, con el botón Gizmos encendido, también en la vista Game.
// Se prende y se apaga en el menú Herramientas > Aves > Mostrar decisiones de las aves.
public partial class BirdAI
{
#if UNITY_EDITOR
    const string ThoughtsPref = "FOW_MostrarDecisionesAves";
    static GUIStyle thoughtStyle;
    static Texture2D thoughtBack;

    public static bool ShowThoughts
    {
        get { return UnityEditor.EditorPrefs.GetBool(ThoughtsPref, false); }
        set { UnityEditor.EditorPrefs.SetBool(ThoughtsPref, value); }
    }

    [UnityEditor.MenuItem("Herramientas/Aves/Mostrar decisiones de las aves")]
    static void ToggleThoughts()
    {
        ShowThoughts = !ShowThoughts;
        UnityEditor.SceneView.RepaintAll();
    }

    [UnityEditor.MenuItem("Herramientas/Aves/Mostrar decisiones de las aves", true)]
    static bool ToggleThoughtsCheck()
    {
        UnityEditor.Menu.SetChecked("Herramientas/Aves/Mostrar decisiones de las aves", ShowThoughts);
        return true;
    }

    // Color según lo que hace: atacar (rojo), cazar (naranja), escapar (amarillo), tranquila (celeste), aturdida (violeta)
    static Color ThoughtColor(string action)
    {
        string a = action.ToLowerInvariant();
        if (a.Contains("pelear") || a.Contains("jugador") || a.Contains("ataque") || a.Contains("rugir")) return new Color(1f, 0.32f, 0.3f);
        if (a.Contains("picada") || a.Contains("acechar") || a.Contains("arponear") || a.Contains("robar")) return new Color(1f, 0.62f, 0.2f);
        if (a.Contains("escapar") || a.Contains("huir") || a.Contains("remontar")) return new Color(1f, 0.9f, 0.3f);
        if (a.Contains("quieto") || a.Contains("vulnerable") || a.Contains("caer")) return new Color(0.75f, 0.55f, 1f);
        return new Color(0.45f, 0.85f, 1f);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !ShowThoughts || dead) return;
        string action = CurrentBehaviour;
        if (string.IsNullOrEmpty(action)) return;
        Color col = ThoughtColor(action);

        if (TargetFish != null)
        {
            Gizmos.color = new Color(col.r, col.g, col.b, 0.8f);
            Gizmos.DrawLine(transform.position, TargetFish.transform.position);
        }

        if (thoughtStyle == null)
        {
            thoughtBack = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            thoughtBack.SetPixel(0, 0, new Color(0.02f, 0.05f, 0.1f, 0.78f));
            thoughtBack.Apply();
            thoughtStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 2, 2),
            };
            thoughtStyle.normal.background = thoughtBack;
        }
        thoughtStyle.normal.textColor = col;
        string label = (IsBoss ? "JEFE · " : "") + action;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.4f, label, thoughtStyle);
    }
#endif
}
