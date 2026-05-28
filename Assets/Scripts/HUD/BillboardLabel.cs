using UnityEngine;

// 让 TextMesh 标签始终朝向主摄像机（Billboard 效果）
public class BillboardLabel : MonoBehaviour
{
    private Camera _cam;
    void Start() { _cam = Camera.main; }
    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        transform.rotation = _cam.transform.rotation;
    }
}
