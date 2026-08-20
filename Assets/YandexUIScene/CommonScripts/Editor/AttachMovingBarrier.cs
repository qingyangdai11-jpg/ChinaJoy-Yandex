using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器脚本：在编辑器编译/加载时自动为 barrier3 和 barrier4 预制体挂载并初始化 MovingBarrier 组件。
/// </summary>
[InitializeOnLoad]
public class AttachMovingBarrier
{
    static AttachMovingBarrier()
    {
        // 延迟调用，以确保 AssetDatabase 安全加载
        EditorApplication.delayCall += AttachComponents;
    }

    private static void AttachComponents()
    {
        string[] prefabPaths = {
            "Assets/Resources/Prefabs/barrier3.prefab",
            "Assets/Resources/Prefabs/barrier4.prefab",
            "Assets/YandexUIScene/Prefabs/barrier3.prefab",
            "Assets/YandexUIScene/Prefabs/barrier4.prefab"
        };

        foreach (var path in prefabPaths)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset == null)
            {
                continue;
            }

            // 如果该预制体上还没有 MovingBarrier，就加载并添加
            if (prefabAsset.GetComponent<MovingBarrier>() == null)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root != null)
                {
                    if (root.GetComponent<MovingBarrier>() == null)
                    {
                        MovingBarrier comp = root.AddComponent<MovingBarrier>();
                        
                        // 初始化默认值
                        comp.movementDirection = Vector3.back;
                        comp.movementSpeed = 3.0f;
                        comp.useDynamicPlayerDirection = true;
                        comp.isMoving = true;

                        // 保存修改后的预制体内容
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        Debug.Log($"[AttachMovingBarrier] 成功为预制体 {path} 挂载并初始化 MovingBarrier 组件。");
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
    }
}
