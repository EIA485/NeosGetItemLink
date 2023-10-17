using HarmonyLib;
using FrooxEngine;
using FrooxEngine.UIX;
using System.Reflection;
using System;
using ResoniteModLoader;
using Elements.Core;
using System.Numerics;
using Elements.Assets;

namespace GetItemLink
{
    public class GetItemLink : ResoniteMod
    {
        public override string Name => "GetItemLink";
        public override string Author => "eia485";
        public override string Version => "1.4.2";
        public override string Link => "https://github.com/eia485/NeosGetItemLink/";

        // Ported by LeCloutPanda

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("net.eia485.GetItemLink");
            harmony.PatchAll();
        }

        static FieldInfo itemInfo = typeof(InventoryItemUI).GetField("Item", BindingFlags.Instance | BindingFlags.NonPublic);
        static FieldInfo directoryInfo = typeof(InventoryItemUI).GetField("Directory", BindingFlags.Instance | BindingFlags.NonPublic);
        const InventoryBrowser.SpecialItemType UniqueSIT = (InventoryBrowser.SpecialItemType)(-1);

        [HarmonyPatch(typeof(InventoryBrowser))]
        class GetItemLinkPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch("OnItemSelected")]
            public static void OnItemSelectedPrefix(ref InventoryBrowser.SpecialItemType __state, Sync<InventoryBrowser.SpecialItemType> ____lastSpecialItemType)
            {
                __state = ____lastSpecialItemType.Value;
            }

            [HarmonyPostfix]
            [HarmonyPatch("OnItemSelected")]
            public static void OnItemSelectedPostfix(InventoryBrowser __instance, BrowserItem currentItem, InventoryBrowser.SpecialItemType __state, SyncRef<Slot> ____buttonsRoot)
            {
                if (__instance.World == Userspace.UserspaceWorld)
                {
                    if (__state != InventoryBrowser.ClassifyItem(currentItem as InventoryItemUI) || __state == UniqueSIT)
                    {

                        AddButton(delegate (IButton button, ButtonEventData eventData) { ItemLink(button, __instance.SelectedInventoryItem, type: false); }, "AssetURI", "Get resdb", OfficialAssets.Graphics.Badges.Cheese, ____buttonsRoot.Target);
                        AddButton(delegate (IButton button, ButtonEventData eventData) { ItemLink(button, __instance.SelectedInventoryItem, type: true); }, "URL", "Get resrec", OfficialAssets.Graphics.Badges.potato, ____buttonsRoot.Target);
                        AddButton((IButton button, ButtonEventData eventData) =>
                        {
                            RecordEditForm editForm;
                            var overlayMngr = __instance.Slot.GetComponentInParents<ModalOverlayManager>();
                            if (overlayMngr == null)
                            {
                                var slot = __instance.LocalUserSpace.AddSlot("Record Edit Form");
                                slot.PositionInFrontOfUser(float3.Backward, float3.Right * 0.5f);
                                editForm = RecordEditForm.OpenDialogWindow(slot);
                            }
                            else
                            {
                                editForm = overlayMngr.OpenModalOverlay(new float2(.25f, .8f), "Settings").Slot.AttachComponent<RecordEditForm>();
                            }
                            Record r = GetRecord(__instance.SelectedInventoryItem);
                            if (r == null) return;
                            AccessTools.Method(typeof(RecordEditForm), "Setup").Invoke(editForm, new object[] { null, r });
                        },
                        "EditRecord", "Edit record", OfficialAssets.Graphics.Icons.Dash.Settings, ____buttonsRoot.Target);
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch("OnAwake")]
            public static void InitializeSyncMembersPostfix(InventoryBrowser __instance, Sync<InventoryBrowser.SpecialItemType> ____lastSpecialItemType)
            {
                if (__instance.World == Userspace.UserspaceWorld) ____lastSpecialItemType.Value = (InventoryBrowser.SpecialItemType)(-1);
            }
        }

        public static void AddButton(ButtonEventHandler onPress, string tag, string text, Uri sprite, Slot buttonRoot)
        {
            try
            {
                Slot slot = buttonRoot.GetComponentInChildren<GridLayout>().Slot.AddSlot("Button");
                slot.Tag = tag;
                slot.AttachComponent<LayoutElement>().PreferredWidth.Value = 266f;
                Button button = slot.AttachComponent<Button>();
                SpriteProvider bsprite = slot.AttachSprite(new Uri("resdb:///3ee5c0335455c19970d877e2b80f7869539df43fccb8fc64b38e320fc44c154f.png"));
                bsprite.Borders.Value = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
                bsprite.Scale.Value = 1f;
                bsprite.FixedSize.Value = 16f;
                Image image = slot.AttachComponent<Image>();
                image.Sprite.Value = bsprite.ReferenceID;
                image.NineSliceSizing.Value = NineSliceSizing.FixedSize;
                button.ColorDrivers.Add();
                button.ColorDrivers[0].ColorDrive.Value = image.Tint.ReferenceID;
                button.ColorDrivers[0].NormalColor.Value = new colorX(0.17f, 0.18f, 0.21f);
                button.ColorDrivers[0].HighlightColor.Value = new colorX(0.37f, 0.41f, 0.48f);
                button.ColorDrivers[0].PressColor.Value = new colorX(0.43f, 0.54f, 0.71f);
                button.ColorDrivers[0].DisabledColor.Value = new colorX(0.07f, 0.08f, 0.11f);

                Slot left = slot.AddSlot("Left");
                SpriteProvider leftSprite = left.AttachSprite(sprite);
                Image leftImage = left.AttachComponent<Image>();
                leftImage.Tint.Value = colorX.White;
                leftImage.Sprite.Value = leftSprite.ReferenceID;
                RectTransform leftRect = left.GetComponent<RectTransform>();
                leftRect.AnchorMax.Value = new Vector2(0.3083f, 1f);
                leftRect.OffsetMin.Value = new Vector2(2f, 2f);
                leftRect.OffsetMax.Value = new Vector2(-2f, -2f);

                Slot right = slot.AddSlot("Right");
                Text rightText = right.AttachComponent<Text>();
                rightText.Content.Value = text;
                rightText.HorizontalAlign.Value = TextHorizontalAlignment.Center;
                rightText.VerticalAlign.Value = TextVerticalAlignment.Middle;
                rightText.AlignmentMode.Value = AlignmentMode.Geometric;
                rightText.HorizontalAutoSize.Value = true;
                rightText.VerticalAutoSize.Value = true;
                rightText.AutoSizeMax.Value = 40f;
                rightText.Color.Value = colorX.White;
                RectTransform rightRect = right.GetComponent<RectTransform>();
                rightRect.AnchorMin.Value = new Vector2(0.3583f, 0f);
                rightRect.AnchorMax.Value = new Vector2(1, 1f);
                rightRect.OffsetMin.Value = new Vector2(2f, 2f);
                rightRect.OffsetMax.Value = new Vector2(-2f, -2f);

                button.LocalPressed += onPress;
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        public static void ItemLink(IButton button, InventoryItemUI Item, bool type)
        {
            string link = GetLink(Item, type);
            if (link != null)
            {
                Engine.Current.InputInterface.Clipboard.SetText(link);
                button.Slot[0].GetComponent<Image>().Tint.Value = colorX.White;
            }
            else
            {
                button.Slot[0].GetComponent<Image>().Tint.Value = colorX.Red;
            }
        }

        private static Record GetRecord(InventoryItemUI item)
        {
            return ((Record)itemInfo.GetValue(item)) ?? ((RecordDirectory)directoryInfo.GetValue(item)).EntryRecord;
        }

        private static string GetLink(InventoryItemUI item, bool type)
        {
            Record record = GetRecord(item);
            if (!type)
            {
                return record?.AssetURI;
            }
            return record?.GetUrl(Engine.Current.Cloud.Platform).ToString();
        }
    }
}