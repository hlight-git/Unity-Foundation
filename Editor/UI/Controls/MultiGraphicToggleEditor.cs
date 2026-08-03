using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Foundation.Editor
{
    [CustomEditor(typeof(MultiGraphicToggle))]
    internal sealed class MultiGraphicToggleEditor : ToggleEditor
    {
        private SerializedProperty targetGraphics;

        protected override void OnEnable()
        {
            base.OnEnable();
            targetGraphics = serializedObject.FindProperty("targetGraphics");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            serializedObject.Update();

            var toggle = (MultiGraphicToggle)target;
            using (new EditorGUI.DisabledScope(toggle.transition != Selectable.Transition.ColorTint))
                EditorGUILayout.PropertyField(targetGraphics);

            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(Toggle))]
    internal sealed class ToggleUpgradeEditor : ToggleEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var toggle = (Toggle)target;
            using (new EditorGUI.DisabledScope(toggle.transition != Selectable.Transition.ColorTint))
            {
                if (!GUILayout.Button("Convert to Multi Graphic Toggle", GUILayout.Height(30f)))
                    return;
            }

            Convert(toggle);
            GUIUtility.ExitGUI();
        }

        private static void Convert(Toggle source)
        {
            var gameObject = source.gameObject;
            var graphics = gameObject.GetComponentsInChildren<Graphic>(includeInactive: true);

            SelectableScriptConverter.Convert<MultiGraphicToggle>(
                source,
                "Convert to Multi Graphic Toggle",
                replacement => replacement.TargetGraphics = graphics);
        }
    }
}
