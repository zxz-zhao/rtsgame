using UnityEditor;
using UnityEngine;

// 编译完成后自动执行低风险冒烟测试（一次性）
[InitializeOnLoad]
public class AutoTest
{
    static AutoTest()
    {
        if (!EditorPrefs.GetBool("RTS_AutoTest_Triggered", false))
        {
            EditorPrefs.SetBool("RTS_AutoTest_Triggered", true);
            // 三帧延迟确保编辑器完全加载
            EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
            {
                if (!Application.isBatchMode && !EditorApplication.isPlaying)
                {
                    Debug.Log("=== 自动冒烟测试启动 ===");
                    AutomatedProjectTest.RunSmokeTest(false);
                }
            };
        }
    }

    // 重置自动测试标记（下次编译后再次自动运行）
    [MenuItem("RTS/重置自动测试标记")]
    static void ResetFlag()
    {
        EditorPrefs.DeleteKey("RTS_AutoTest_Triggered");
        Debug.Log("已重置，下次编译后自动运行冒烟测试");
    }
}
