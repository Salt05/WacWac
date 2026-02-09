#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class CreateGameRequestAsset
{
    [MenuItem("WacWac/Create GameRequest Asset")]
    public static void CreateAsset()
    {
        var asset = ScriptableObject.CreateInstance<GameRequest>();
        asset.gameTitle = "WacWac Sample";
        asset.version = "0.1";
        asset.description = "Mẫu GameRequest - chỉnh sửa trong Inspector để thêm nội dung cụ thể.";

        // Try to load common prefabs
        var duckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DuckPrefab.prefab");
        if (duckPrefab != null) asset.prefabs.Add(duckPrefab);
        var finishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Finish.prefab");
        if (finishPrefab != null) asset.prefabs.Add(finishPrefab);

        // Try to load sprites from Assets/Resource/DuckSkin
        string duckSkinFolder = "Assets/Resource/DuckSkin";
        if (Directory.Exists(duckSkinFolder))
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { duckSkinFolder });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) asset.duckSkins.Add(s);
            }
        }

        // Ensure Resources folder exists
        if (!Directory.Exists("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        string assetPath = "Assets/Resources/GameRequest.asset";
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;
        Debug.Log("GameRequest asset created at " + assetPath + ". Mở Inspector để chỉnh nội dung.");
    }
}
#endif
