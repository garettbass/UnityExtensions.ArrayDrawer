using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    internal static class SubassetUtility
    {

        public static void TryDestroyImmediate(this Object asset)
        {
            try
            {
                if (asset != null)
                    Object.DestroyImmediate(asset, allowDestroyingAssets: true);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        //----------------------------------------------------------------------

        public static HashSet<Object>
        FindReferencedSubassets(this SerializedProperty property)
        {
            var propertyAsset = property.serializedObject.targetObject;
            var assetPath = AssetDatabase.GetAssetPath(propertyAsset);
            var allSubassets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var referencedSubassets = new HashSet<Object>();
            AddReferencedSubassets(referencedSubassets, allSubassets, property);
            return referencedSubassets;
        }

        public static bool
        DoesReferenceSubassets(this SerializedProperty property)
        {
            return FindReferencedSubassets(property).Any();
        }

        //----------------------------------------------------------------------

        public static void
        DestroyUnreferencedSubassetsInAsset(
            this SerializedObject serializedObject)
        {
            var targetObject = serializedObject.targetObject;
            var assetPath = AssetDatabase.GetAssetPath(targetObject);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var localAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var referencedAssets = new HashSet<Object>();
            AddReferencedSubassets(referencedAssets, localAssets, mainAsset);
            var unreferencedAssets = localAssets.Except(referencedAssets);
            foreach (var unreferencedAsset in unreferencedAssets)
                TryDestroyImmediate(unreferencedAsset);
        }

        //----------------------------------------------------------------------

        private static void
        AddReferencedSubassets(
            HashSet<Object> referencedSubassets,
            Object[] localAssets,
            Object asset)
        {
            if (asset == null)
                return;

            if (!localAssets.Contains(asset))
                return;

            if (!referencedSubassets.Add(asset))
                return;

            var serializedObject = new SerializedObject(asset);
            var children = serializedObject.EnumerateChildProperties();
            foreach (var child in children)
                AddReferencedSubassets(
                    referencedSubassets,
                    localAssets,
                    child);
        }

        private static void
        AddReferencedSubassets(
            HashSet<Object> referencedSubassets,
            Object[] localAssets,
            SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                AddReferencedSubassets(
                    referencedSubassets,
                    localAssets,
                    property.objectReferenceValue);
            }
            else
            {
                var children = property.EnumerateChildProperties();
                foreach (var child in children)
                    AddReferencedSubassets(
                        referencedSubassets,
                        localAssets,
                        child);
            }
        }
    }

}