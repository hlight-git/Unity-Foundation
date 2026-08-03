#if ODIN_INSPECTOR
using System;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Hlight.Foundation.Editor
{
    public abstract class ADateTimeOdinDrawer<T> : OdinValueDrawer<T>
    {
        private const string SetAsNowLabel = "Set as now";

        private bool isExpanded;

        protected abstract DateTime Value { get; set; }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            isExpanded = SirenixEditorGUI.Foldout(isExpanded, label);
            if (!isExpanded)
                return;

            EditorGUI.indentLevel++;
            var value = Value;
            EditorGUI.BeginChangeCheck();

            var year = SirenixEditorFields.RangeIntField(nameof(value.Year), value.Year, 1, 9999);
            var month = SirenixEditorFields.RangeIntField(nameof(value.Month), value.Month, 1, 12);
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var day = SirenixEditorFields.RangeIntField(nameof(value.Day), Mathf.Min(daysInMonth, value.Day), 1, daysInMonth);
            var hour = SirenixEditorFields.RangeIntField(nameof(value.Hour), value.Hour, 0, 23);
            var minute = SirenixEditorFields.RangeIntField(nameof(value.Minute), value.Minute, 0, 59);
            var second = SirenixEditorFields.RangeIntField(nameof(value.Second), value.Second, 0, 59);

            if (EditorGUI.EndChangeCheck())
                Value = new DateTime(year, month, day, hour, minute, second);

            GUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 15f);
            if (GUILayout.Button(SetAsNowLabel))
                Value = DateTime.Now;
            GUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }
    }
}
#endif
