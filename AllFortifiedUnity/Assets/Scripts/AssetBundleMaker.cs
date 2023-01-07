#if UNITY_EDITOR

using UnityEngine;
using System.Collections;
using UnityEditor;

public class CreateAssetBundles : Editor {
    [MenuItem("Assets/Build Asset Bundle")]
    static void CreateBundle() {
        string bundlePath = "Assets/AssetBundles/";
        BuildPipeline.BuildAssetBundles(bundlePath, BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows);

    }
}

#endif