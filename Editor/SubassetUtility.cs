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

        public static string GetAssetPath(this Object asset)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(assetPath))
                return assetPath;

        #if UNITY_2018_3_OR_NEWER
            if (PrefabUtility.IsPartOfPrefabAsset(asset))
                assetPath =
                    PrefabUtility
                    .GetPrefabAssetPathOfNearestInstanceRoot(asset);
        #else
            var prefabRoot = PrefabUtility.GetPrefabObject(asset);
            if (prefabRoot != null)
                assetPath =
                    AssetDatabase
                    .GetAssetPath(prefabRoot);
        #endif

            if (!string.IsNullOrEmpty(assetPath))
                return assetPath;

            GameObject gameObject = null;
            if ( asset is GameObject )
                gameObject = (GameObject) asset;
            else if ( asset is Component )
                gameObject = ((Component) asset).gameObject;

            if (gameObject != null)
                return gameObject.scene.path;

            return null;
        }

        //----------------------------------------------------------------------

        public static void AddSubasset(this Object asset, Object subasset)
        {
            var assetPath = asset.GetAssetPath();
            Debug.Assert(assetPath != null);
            AssetDatabase.AddObjectToAsset(subasset, assetPath);
            AssetDatabase.ImportAsset(assetPath);
        }

        //----------------------------------------------------------------------

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
        DestroyUnreferencedSubassets(
            this SerializedObject serializedObject,
            IEnumerable<Object> candidateSubassets)
        {
            var targetObject = serializedObject.targetObject;
            var assetPath = AssetDatabase.GetAssetPath(targetObject);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var allSubassets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var referencedSubassets = new HashSet<Object>();
            AddReferencedSubassets(referencedSubassets, allSubassets, mainAsset);
            var unreferencedSubassets = candidateSubassets.Except(referencedSubassets);
            foreach (var unreferencedAsset in unreferencedSubassets)
                TryDestroyImmediate(unreferencedAsset);
            AssetDatabase.ImportAsset(assetPath);
        }

        //----------------------------------------------------------------------

        public static void
        DestroyAllUnreferencedSubassetsInAsset(this Object asset)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var allSubassets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var referencedSubassets = new HashSet<Object>();
            AddReferencedSubassets(referencedSubassets, allSubassets, mainAsset);
            var unreferencedSubassets = allSubassets.Except(referencedSubassets);
            foreach (var unreferencedAsset in unreferencedSubassets)
                TryDestroyImmediate(unreferencedAsset);
        }

        //----------------------------------------------------------------------

        private static void
        AddReferencedSubassets(
            HashSet<Object> referencedSubassets,
            Object[] allSubassets,
            Object asset)
        {
            if (asset == null)
                return;

            if (!allSubassets.Contains(asset))
                return;

            if (!referencedSubassets.Add(asset))
                return;

            var serializedObject = new SerializedObject(asset);
            var children = serializedObject.EnumerateChildProperties();
            foreach (var child in children)
                AddReferencedSubassets(
                    referencedSubassets,
                    allSubassets,
                    child);
        }

        private static void
        AddReferencedSubassets(
            HashSet<Object> referencedSubassets,
            Object[] allSubassets,
            SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                AddReferencedSubassets(
                    referencedSubassets,
                    allSubassets,
                    property.objectReferenceValue);
            }
            else
            {
                var children = property.EnumerateChildProperties();
                foreach (var child in children)
                    AddReferencedSubassets(
                        referencedSubassets,
                        allSubassets,
                        child);
            }
        }
    }

}