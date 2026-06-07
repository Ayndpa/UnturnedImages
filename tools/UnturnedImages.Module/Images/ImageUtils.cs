using HarmonyLib;
using JetBrains.Annotations;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnturnedImages.Module.Workshop;
using System.Threading.Tasks;
using System.Reflection;

namespace UnturnedImages.Module.Images
{
    public static class ImageUtils
    {
        internal static Vector3 ItemIconRotation { get; private set; }
        public static int PendingItemCount = 0;

        public static void CaptureVehicleImages(IEnumerable<VehicleAsset> vehicleAssets, Vector3? vehicleAngles = null)
        {
            var basePath = Path.Combine(ReadWrite.PATH, "Extras", "Vehicles");
            foreach (var asset in vehicleAssets)
            {
                string modPathSection = WorkshopHelper.IsWorkshop(asset) 
                    ? Path.Combine("Workshop", WorkshopHelper.GetWorkshopId(asset).ToString()) 
                    : "Official";
                var fullPath = Path.Combine(basePath, modPathSection, asset.GUID.ToString());
                CustomVehicleTool.QueueVehicleIcon(asset, fullPath, 1024, 1024, vehicleAngles);
            }
        }

        public static void CaptureItemImages(IEnumerable<ItemAsset> itemAssets, Vector3? itemIconRotation = null)
        {
            ItemIconRotation = itemIconRotation ?? Vector3.zero;
            var basePath = Path.Combine(ReadWrite.PATH, "Extras", "Items");

            foreach (var asset in itemAssets)
            {
                string modPathSection = WorkshopHelper.IsWorkshop(asset) 
                    ? Path.Combine("Workshop", WorkshopHelper.GetWorkshopId(asset).ToString()) 
                    : "Official";
                var path = Path.Combine(basePath, modPathSection, asset.GUID.ToString());
                string finalPath = path + ".png";

                // Incremental processing: skip if already exists
                if (File.Exists(finalPath)) continue;

                PendingItemCount++;
                ItemTool.getIcon(asset.id, 0, 100, asset.getState(), asset, null, string.Empty,
                    string.Empty, asset.size_x * 512, asset.size_y * 512, false, true,
                    (handle, texture) =>
                    {
                        try {
                            if (texture != null) {
                                byte[] pngBytes = texture.EncodeToPNG();
                                UnityEngine.Object.Destroy(texture);
                                Task.Run(() => {
                                    try {
                                        string finalPath = path + ".png";
                                        string dir = Path.GetDirectoryName(finalPath);
                                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                                        File.WriteAllBytes(finalPath, pngBytes);
                                    } catch { }
                                });
                            }
                        } finally {
                            PendingItemCount--;
                        }
                    });
            }
        }

        public static void CaptureAllVehicleImages(Vector3? vehicleAngles = null)
        {
            List<VehicleAsset> assets = new List<VehicleAsset>();
            Assets.find(assets);
            CaptureVehicleImages(assets, vehicleAngles);
        }

        public static void CaptureAllItemImages(Vector3? itemAngles = null)
        {
            List<ItemAsset> assets = new List<ItemAsset>();
            Assets.find(assets);
            CaptureItemImages(assets, itemAngles);
        }

        public static void CaptureModItemImages(ulong mod, Vector3? itemAngles = null)
        {
            List<ItemAsset> assets = new List<ItemAsset>();
            Assets.find(assets);
            var modAssets = assets.Where(x => WorkshopHelper.GetWorkshopIdSafe(x) == mod).ToList();
            CaptureItemImages(modAssets, itemAngles);
        }

        public static void CaptureModVehicleImages(ulong mod, Vector3? vehicleAngles = null)
        {
            List<VehicleAsset> assets = new List<VehicleAsset>();
            Assets.find(assets);
            var modAssets = assets.Where(x => WorkshopHelper.GetWorkshopIdSafe(x) == mod).ToList();
            CaptureVehicleImages(modAssets, vehicleAngles);
        }

        private static FieldInfo? _itemToolIconsField;
        public static int GetInternalItemQueueCount()
        {
            if (_itemToolIconsField == null)
                _itemToolIconsField = typeof(ItemTool).GetField("icons", BindingFlags.NonPublic | BindingFlags.Static);
            
            var queue = _itemToolIconsField?.GetValue(null) as System.Collections.IEnumerable;
            if (queue == null) return 0;
            
            int count = 0;
            foreach (var item in queue) count++;
            return count;
        }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        [HarmonyPatch]
        private static class UnturnedPatches
        {
            private static Vector3 _iconPosition;
            private static bool _isInsideTurbo = false;

            [HarmonyPatch(typeof(ItemTool), "captureIcon")]
            [HarmonyPrefix]
            public static void ItemToolCaptureIconPre(Transform model, Transform icon, ushort id, int width, int height, ref float orthoSize)
            {
                _iconPosition = icon.position;
                icon.RotateAround(model.position, icon.right, ItemIconRotation.x);
                icon.RotateAround(model.position, icon.up, ItemIconRotation.y);
                icon.RotateAround(model.position, icon.forward, ItemIconRotation.z);

                var itemAsset = Assets.find(EAssetType.ITEM, id) as ItemAsset;
                if (itemAsset != null)
                {
                    orthoSize = CustomImageTool.CalculateOrthographicSize(itemAsset, model.gameObject, icon, width, height, out var position);
                    icon.position = position;
                }
            }

            [HarmonyPatch(typeof(ItemTool), "captureIcon")]
            [HarmonyPostfix]
            public static void ItemToolCaptureIconPost(Transform icon)
            {
                icon.position = _iconPosition;
            }

            [HarmonyPatch(typeof(ItemTool), "Update")]
            [HarmonyPrefix]
            public static bool ItemToolUpdateTurbo(ItemTool __instance)
            {
                if (!AutoExporter.IsExporting) return true;
                if (_isInsideTurbo) return true;
                return false; 
            }
        }
        
        private static FieldInfo? _isInsideTurboField;
        public static void ForceDrive(ItemTool instance, MethodInfo updateMethod)
        {
            if (_isInsideTurboField == null)
                _isInsideTurboField = typeof(UnturnedPatches).GetField("_isInsideTurbo", BindingFlags.NonPublic | BindingFlags.Static);

            _isInsideTurboField?.SetValue(null, true);
            try {
                updateMethod.Invoke(instance, null);
            } finally {
                _isInsideTurboField?.SetValue(null, false);
            }
        }
    }
}
