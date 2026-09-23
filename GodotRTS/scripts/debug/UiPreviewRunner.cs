using Godot;
using System;
using System.IO;

/// <summary>
/// 通用 UI 与独立场景自动化高清渲染走查组件
/// 支持将任意 .tscn 场景或 UI 控件动态挂载并使用本地 GPU 进行帧渲染与截图导出
/// </summary>
public partial class UiPreviewRunner : Control
{
    private string _uiScenePath = "";
    private string _capturePath = "screenshots/ui_preview.png";
    private int _framesToWait = 15;

    public override void _Ready()
    {
        var args = CommandLineArgs.Get();
        GD.Print($"[UiPreviewRunner] 启动参数: {string.Join(" ", args)}");

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--ui-scene" || args[i] == "--scene") && i + 1 < args.Length)
            {
                _uiScenePath = args[i + 1].Trim();
            }
            else if (args[i] == "--capture-path" && i + 1 < args.Length)
            {
                _capturePath = args[i + 1].Trim();
            }
            else if (args[i] == "--capture-frames" && i + 1 < args.Length)
            {
                int.TryParse(args[i + 1], out _framesToWait);
            }
        }

        if (string.IsNullOrWhiteSpace(_uiScenePath))
        {
            GD.PrintErr("[UiPreviewRunner] 未指定 --ui-scene 参数！");
            GetTree().Quit(1);
            return;
        }

        MountAndCaptureAsync();
    }

    private async void MountAndCaptureAsync()
    {
        try
        {
            if (!ResourceLoader.Exists(_uiScenePath))
            {
                GD.PrintErr($"[UiPreviewRunner] 场景文件不存在: {_uiScenePath}");
                GetTree().Quit(1);
                return;
            }

            var packedScene = GD.Load<PackedScene>(_uiScenePath);
            if (packedScene == null)
            {
                GD.PrintErr($"[UiPreviewRunner] 无法加载 PackedScene: {_uiScenePath}");
                GetTree().Quit(1);
                return;
            }

            var instance = packedScene.Instantiate();
            if (instance == null)
            {
                GD.PrintErr($"[UiPreviewRunner] 实例化场景失败: {_uiScenePath}");
                GetTree().Quit(1);
                return;
            }

            var container = GetNodeOrNull<Control>("CanvasLayer/UIContainer");
            if (container != null)
            {
                container.AddChild(instance);
                if (instance is Control ctrl)
                {
                    // 若未显式设置全屏锚点，默认居中展示
                    if (ctrl.LayoutMode == 0 || ctrl.AnchorsPreset == (int)LayoutPreset.TopLeft)
                    {
                        ctrl.SetAnchorsPreset(LayoutPreset.Center);
                    }
                }
            }
            else
            {
                AddChild(instance);
            }

            GD.Print($"[UiPreviewRunner] 成功挂载场景实例: {instance.Name} ({instance.GetType().Name})");

            // 等待指定帧数，让 UI 布局、字体抗锯齿、动画与 Shader 渲染稳定
            for (int i = 0; i < Math.Max(5, _framesToWait); i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            // 获取视口纹理与像素
            var texture = GetViewport().GetTexture();
            var image = texture?.GetImage();

            if (image == null || image.IsEmpty())
            {
                GD.PrintErr("[UiPreviewRunner] 视口图像纹理为空！");
                GetTree().Quit(1);
                return;
            }

            string globalPath = _capturePath.StartsWith("res://") || _capturePath.StartsWith("user://")
                ? ProjectSettings.GlobalizePath(_capturePath)
                : Path.GetFullPath(_capturePath);

            string dir = Path.GetDirectoryName(globalPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var err = image.SavePng(globalPath);
            if (err == Error.Ok)
            {
                GD.Print($"[UI_PREVIEW_SUCCESS] 成功保存 UI 渲染效果图至: {globalPath}");
            }
            else
            {
                GD.PrintErr($"[UiPreviewRunner] 保存 PNG 失败: {err}");
            }

            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[UiPreviewRunner 异常]: {ex}");
            GetTree().Quit(1);
        }
    }
}
