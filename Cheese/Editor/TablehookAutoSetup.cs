#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class TablehookAutoSetup
{
    private const string PrefabPath = "Assets/VRChat-Pool-table-15-red-snooker-Pyramid-Chinese-8-MS-VRCSA-Billiards/Prefab/TableHook (replica) 2.prefab"; // 预制件路径
    static TablehookAutoSetup()
    {
        // 注册场景加载完成事件
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // 进入编辑模式时检查
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SetupTablehook();
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 场景加载完成后检查
        if (!EditorApplication.isPlaying)
        {
            SetupTablehook();
        }
    }

    [MenuItem("MS-VRCSA/Setup Tablehook")]
    private static void SetupTablehook()
    {
        // 查找场景中的BilliardsModule
        var modules = Object.FindObjectsByType<BilliardsModule>(FindObjectsSortMode.None);
        if (modules == null) return;

        // 检查是否已存在Tablehook实例
        if (IsTablehookExists()) return;

        // 加载预制件
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Tablehook prefab not found at path: {PrefabPath}");
            return;
        }

        // 实例化并记录Undo操作
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Undo.RegisterCreatedObjectUndo(instance, "Create Tablehook");
        Debug.Log("init table hook success,you can change the position as you want.");
        instance.transform.position = new Vector3(modules[0].transform.position.x, modules[0].transform.position.y + 1, modules[0].transform.position.z + 4);
        // 初始化逻辑（根据实际需求修改）
        foreach (var module in modules)
        {
            InitializeTablehook(module, instance);
        }

        // 标记场景为需要保存
        MarkSceneDirty();
    }

    private static bool IsTablehookExists()
    {
        // 通过Tag或名称查找现有实例
        return Object.FindAnyObjectByType<TableHook>() != null; 
    }

    private static void InitializeTablehook(BilliardsModule module, GameObject tablehook)
    {
        // 示例初始化逻辑（根据实际需求修改）
        module.tableHook = tablehook.GetComponent<TableHook>();
    }

    private static void MarkSceneDirty()
    {
        // 标记当前场景需要保存
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
#endif