using System;
using UnityEditor;
using UnityEngine;

namespace Hlight.Foundation.Editor
{
    internal static class SelectableScriptConverter
    {
        public static TTarget Convert<TTarget>(
            MonoBehaviour source,
            string undoName,
            Action<TTarget> initialize)
            where TTarget : MonoBehaviour
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (initialize == null) throw new ArgumentNullException(nameof(initialize));

            var sourceType = source.GetType();
            var gameObject = source.gameObject;
            var targetType = typeof(TTarget);
            if (!sourceType.IsAssignableFrom(targetType))
            {
                throw new InvalidOperationException(
                    $"Cannot convert {sourceType.Name} to unrelated type {targetType.Name}.");
            }

            if (PrefabUtility.IsPartOfPrefabInstance(source))
            {
                EditorUtility.DisplayDialog(
                    "Open the Prefab Source",
                    $"Convert '{source.name}' in its prefab source. Changing a component script " +
                    "on a prefab instance is not a supported prefab override.",
                    "OK");
                return null;
            }

            var targetScript = FindScript(targetType);
            var sourceId = GlobalObjectId.GetGlobalObjectIdSlow(source);
            var entityId = source.GetEntityId();
            var serializedSource = new SerializedObject(source);
            serializedSource.Update();

            var scriptProperty = serializedSource.FindProperty("m_Script") ??
                throw new InvalidOperationException(
                    $"{source.GetType().Name} does not expose a serialized script reference.");
            var sourceScript = scriptProperty.objectReferenceValue as MonoScript ??
                throw new InvalidOperationException(
                    $"{sourceType.Name} does not have a valid source script.");

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RegisterCompleteObjectUndo(source, undoName);

            try
            {
                scriptProperty.objectReferenceValue = targetScript;
                serializedSource.ApplyModifiedProperties();

                var convertedObject = EditorUtility.EntityIdToObject(entityId) as MonoBehaviour;
                var replacement = convertedObject as TTarget ?? gameObject.GetComponent<TTarget>();
                if (replacement == null)
                {
                    throw new InvalidOperationException(
                        $"Unity did not reload the component as {targetType.Name}.");
                }

                var targetId = GlobalObjectId.GetGlobalObjectIdSlow(replacement);
                if (!sourceId.Equals(targetId))
                {
                    throw new InvalidOperationException(
                        $"Converting {sourceType.Name} to {targetType.Name} changed its " +
                        "serialized identity.");
                }

                Undo.RecordObject(replacement, undoName);
                initialize(replacement);
                EditorUtility.SetDirty(replacement);
                Undo.CollapseUndoOperations(undoGroup);
                Selection.activeObject = replacement;
                return replacement;
            }
            catch (Exception conversionException)
            {
                try
                {
                    var convertedObject = EditorUtility.EntityIdToObject(entityId) as MonoBehaviour;
                    RestoreScript(convertedObject, serializedSource, sourceScript);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "Component conversion failed and its script rollback also failed.",
                        conversionException,
                        rollbackException);
                }
                finally
                {
                    Undo.CollapseUndoOperations(undoGroup);
                }

                throw;
            }
        }

        private static MonoScript FindScript(Type targetType)
        {
            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (script != null && script.GetClass() == targetType)
                    return script;
            }

            throw new InvalidOperationException(
                $"Could not find the MonoScript for {targetType.FullName}.");
        }

        private static void RestoreScript(
            MonoBehaviour component,
            SerializedObject fallback,
            MonoScript sourceScript)
        {
            var serializedComponent = component != null
                ? new SerializedObject(component)
                : fallback;
            if (serializedComponent == null)
                throw new InvalidOperationException("The converted component could not be recovered.");

            serializedComponent.Update();
            var scriptProperty = serializedComponent.FindProperty("m_Script") ??
                throw new InvalidOperationException("The converted component lost its script property.");

            scriptProperty.objectReferenceValue = sourceScript;
            serializedComponent.ApplyModifiedProperties();
        }
    }
}
