using UnityEditor;
using UnityEngine;

// Inspector del agua: botones para reconstruir y ajustar la altura, y datos del lago.
[CustomEditor(typeof(LakeWater))]
public class LakeWaterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LakeWater water = (LakeWater)target;

        EditorGUILayout.HelpBox(
            "Cómo usar:\n" +
            "• Coloca este objeto dentro del hueco del lago.\n" +
            "• La altura (Y) del objeto es el nivel del agua.\n" +
            "• El agua llena el hueco sola y se adapta a la forma del terreno.\n" +
            "• Los colores y las ondas se cambian en el material (shader 'FishOutOfWater/Agua Estilizada').",
            MessageType.Info);

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reconstruir agua", GUILayout.Height(26)))
            {
                water.Rebuild();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Ajustar altura al hueco", GUILayout.Height(26)))
            {
                Undo.RecordObject(water.transform, "Ajustar altura del agua");
                water.AutoFitHeight();
                EditorUtility.SetDirty(water.transform);
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.Space(4);
        if (!string.IsNullOrEmpty(water.LastError))
        {
            EditorGUILayout.HelpBox(water.LastError, MessageType.Error);
        }
        else if (water.HasData)
        {
            if (water.Spills)
                EditorGUILayout.HelpBox("El agua se desborda fuera del hueco (llega al límite de búsqueda). Baja el objeto o usa 'Ajustar altura al hueco'.", MessageType.Warning);

            EditorGUILayout.LabelField("Nivel del agua", water.SurfaceY.ToString("0.00") + " m");
            EditorGUILayout.LabelField("Superficie", Mathf.RoundToInt(water.Area) + " m²");
            EditorGUILayout.LabelField("Profundidad máxima", water.MaxDepth.ToString("0.0") + " m");
            EditorGUILayout.LabelField("Vértices", water.VertexCount.ToString());
        }

        EditorGUILayout.Space(6);
        DrawDefaultInspector();
    }
}
