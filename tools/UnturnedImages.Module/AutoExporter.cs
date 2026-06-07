using SDG.Unturned;
using UnityEngine;
using UnturnedImages.Module.Images;
using System.Collections;
using System;
using System.Reflection;
using System.Diagnostics;

namespace UnturnedImages.Module
{
    public class AutoExporter : MonoBehaviour
    {
        public static bool IsExporting { get; private set; }
        private ISleekLabel? _statusLabel;
        private ISleekLabel? _etaLabel;
        private MethodInfo? _itemToolUpdateMethod;

        private void Awake()
        {
            IsExporting = true;
        }

        private IEnumerator Start()
        {
            UnturnedLog.info("AutoExporter started, waiting for menu...");
            while (MenuUI.window == null || MenuUI.container == null) yield return null;

            try 
            {
                MenuUI.container.IsVisible = false;
                var glazier = Glazier.Get();
                var background = glazier.CreateBox();
                background.SizeScale_X = 1f; background.SizeScale_Y = 1f;
                MenuUI.window.AddChild(background);

                var titleLabel = glazier.CreateLabel();
                titleLabel.SizeScale_X = 1f; titleLabel.SizeOffset_Y = 100;
                titleLabel.PositionScale_Y = 0.25f;
                titleLabel.Text = "正在自动导出游戏素材，请勿进行游戏操作...\nAutomated Exporting Assets. DO NOT PLAY...";
                titleLabel.FontSize = ESleekFontSize.Title;
                titleLabel.TextColor = Color.yellow;
                background.AddChild(titleLabel);

                _statusLabel = glazier.CreateLabel();
                _statusLabel.SizeScale_X = 1f; _statusLabel.SizeOffset_Y = 100;
                _statusLabel.PositionScale_Y = 0.45f;
                _statusLabel.FontSize = ESleekFontSize.Large;
                background.AddChild(_statusLabel);

                _etaLabel = glazier.CreateLabel();
                _etaLabel.SizeScale_X = 1f; _etaLabel.SizeOffset_Y = 100;
                _etaLabel.PositionScale_Y = 0.6f;
                _etaLabel.FontSize = ESleekFontSize.Large;
                _etaLabel.TextColor = Color.cyan;
                background.AddChild(_etaLabel);
            } catch (Exception ex) {
                UnturnedLog.error("Error creating UI: " + ex);
            }

            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 1000;

            yield return new WaitForSeconds(1f);
            IconUtils.CreateExtrasDirectory();
            
            ImageUtils.CaptureAllItemImages();
            ImageUtils.CaptureAllVehicleImages();
            yield return null;

            ItemTool itemToolInstance = GameObject.FindObjectOfType<ItemTool>();
            _itemToolUpdateMethod = typeof(ItemTool).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);

            UnturnedLog.info("OVERDRIVE ENGAGED.");
            Stopwatch sw = new Stopwatch();
            float startTime = Time.realtimeSinceStartup;
            
            // Wait a frame to ensure PendingItemCount is populated
            yield return null; 
            int initialTotal = ImageUtils.PendingItemCount + CustomVehicleTool.GetQueueCount();

            while (true)
            {
                int currentItemQueue = ImageUtils.GetInternalItemQueueCount();
                int currentVehicleQueue = CustomVehicleTool.GetQueueCount();

                if (currentItemQueue == 0 && currentVehicleQueue == 0) break;

                sw.Restart();
                while (sw.ElapsedMilliseconds < 150)
                {
                    if (ImageUtils.GetInternalItemQueueCount() == 0) break;
                    if (itemToolInstance != null && _itemToolUpdateMethod != null)
                        ImageUtils.ForceDrive(itemToolInstance, _itemToolUpdateMethod);
                }

                int currentPendingTotal = ImageUtils.PendingItemCount + currentVehicleQueue;
                int completed = initialTotal - currentPendingTotal;
                float elapsed = Time.realtimeSinceStartup - startTime;
                
                if (_statusLabel != null) 
                    _statusLabel.Text = $"剩余物品: {ImageUtils.PendingItemCount} | 剩余车辆: {currentVehicleQueue}";

                if (_etaLabel != null)
                {
                    string etaText = "预计剩余时间: 计算中...";
                    if (completed > 20 && elapsed > 1f)
                    {
                        float speed = completed / elapsed;
                        if (speed > 0) {
                            float remainingSeconds = currentPendingTotal / speed;
                            TimeSpan t = TimeSpan.FromSeconds(remainingSeconds);
                            etaText = string.Format("预计剩余时间: {0:D2}:{1:D2}", (int)t.TotalMinutes, t.Seconds);
                        }
                    }
                    _etaLabel.Text = etaText;
                }
                
                yield return null; 
            }

            if (_statusLabel != null) 
            {
                _statusLabel.Text = "导出任务已完成！正在退出游戏...";
                _statusLabel.TextColor = Color.green;
            }
            if (_etaLabel != null) _etaLabel.IsVisible = false;
            
            yield return new WaitForSeconds(3f);
            Application.Quit();
        }
    }
}
