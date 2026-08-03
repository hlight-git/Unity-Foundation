using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Foundation.Editor
{
    [CustomEditor(typeof(MultiGraphicButton))]
    internal sealed class MultiGraphicButtonEditor : ButtonEditor
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

            var button = (MultiGraphicButton)target;
            using (new EditorGUI.DisabledScope(button.transition != Selectable.Transition.ColorTint))
                EditorGUILayout.PropertyField(targetGraphics);

            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(Button))]
    internal sealed class ButtonUpgradeEditor : ButtonEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var button = (Button)target;
            using (new EditorGUI.DisabledScope(button.transition != Selectable.Transition.ColorTint))
            {
                if (!GUILayout.Button("Convert to Multi Graphic Button", GUILayout.Height(30f)))
                    return;
            }

            Convert(button);
            GUIUtility.ExitGUI();
        }

        private static void Convert(Button source)
        {
            var gameObject = source.gameObject;
            var graphics = gameObject.GetComponentsInChildren<Graphic>(includeInactive: true);

            SelectableScriptConverter.Convert<MultiGraphicButton>(
                source,
                "Convert to Multi Graphic Button",
                replacement => replacement.TargetGraphics = graphics);
        }
    }
}
