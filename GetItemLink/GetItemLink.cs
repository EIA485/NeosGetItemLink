﻿using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine;
using FrooxEngine.UIX;
using Elements.Core;
using System.Reflection;
using System;

namespace GetItemLink
{
    public class GetItemLink : ResoniteMod
    {
        public override string Name => "GetItemLink";
        public override string Author => "eia485";
        public override string Version => "1.4.2";
        public override string Link => "https://github.com/eia485/ResoniteGetItemLink/";
        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("net.eia485.GetItemLink");
            harmony.PatchAll();
        }

        static FieldInfo itemInfo = typeof(InventoryItemUI).GetField("Item", BindingFlags.Instance | BindingFlags.NonPublic);
        static FieldInfo directoryInfo = typeof(InventoryItemUI).GetField("Directory", BindingFlags.Instance | BindingFlags.NonPublic);
        const InventoryBrowser.SpecialItemType UniqueSIT = (InventoryBrowser.SpecialItemType)(-1);// doing this so the buttons show up on component init

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
                        Slot buttonRoot = ____buttonsRoot.Target[0];
                        AddButton((IButton button, ButtonEventData eventData) =>
                        {
                            ItemLink(button, __instance.SelectedInventoryItem, false);
                        },
                        "AssetURI",
                        colorX.Purple,
                        OfficialAssets.Graphics.Badges.Cheese,
                        buttonRoot);

                        AddButton((IButton button, ButtonEventData eventData) =>
                        {
                            ItemLink(button, __instance.SelectedInventoryItem, true);
                        },
                        "URL",
                        colorX.Brown,
                        OfficialAssets.Graphics.Badges.potato,
                        buttonRoot);
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
                                editForm = overlayMngr.OpenModalOverlay(new float2(.25f, .8f), "Edit Record").Slot.AttachComponent<RecordEditForm>();
                            }
                            Record r = GetRecord(__instance.SelectedInventoryItem);
                            if (r == null) return;
                            AccessTools.Method(typeof(RecordEditForm), "Setup").Invoke(editForm, new object[] { null, r});
                        },
                        "EditRecord",
                        colorX.Orange,
                        OfficialAssets.Graphics.Icons.Dash.Settings,
                        buttonRoot);
                    }
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch("OnChanges")]
            public static void OnChangesPrefix(InventoryBrowser __instance, SyncRef<Slot> ____buttonsRoot)
            {
                if (__instance.World == Userspace.UserspaceWorld)
                {
                    Slot buttonRoot = ____buttonsRoot.Target[0];
                    bool enableButtons = __instance.SelectedInventoryItem != null;
                    foreach (var child in buttonRoot.Children)
                    {
                        if (child.Tag == "AssetURI")
                        {
                            child.GetComponent<Button>().Enabled = enableButtons && (GetLink(__instance.SelectedInventoryItem, false) != null);
                            child[0].GetComponent<Image>().Tint.Value = colorX.Black;
                        }
                        else if (child.Tag == "URL")
                        {
                            child.GetComponent<Button>().Enabled = enableButtons && (GetLink(__instance.SelectedInventoryItem, true) != null);
                            child[0].GetComponent<Image>().Tint.Value = colorX.Black;
                        }
                        else if (child.Tag == "EditRecord")
                        {
                            child.GetComponent<Button>().Enabled = enableButtons && GetRecord(__instance.SelectedInventoryItem) != null;
                            child[0].GetComponent<Image>().Tint.Value = colorX.Black;
                        }
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch("OnAwake")]
            public static void InitializeSyncMembersPostfix(InventoryBrowser __instance, Sync<InventoryBrowser.SpecialItemType> ____lastSpecialItemType)
            {
                if (__instance.World == Userspace.UserspaceWorld)
                {
                    ____lastSpecialItemType.Value = UniqueSIT;
                }
            }
        }

        public static void AddButton(ButtonEventHandler onPress, string tag, colorX tint, Uri sprite, Slot buttonRoot)
        {
            Slot buttonSlot = buttonRoot.AddSlot("Button");
            buttonSlot.Tag = tag;
            buttonSlot.AttachComponent<LayoutElement>().PreferredWidth.Value = 96 * .6f;
            Button userButton = buttonSlot.AttachComponent<Button>();
            Image frontimage = buttonSlot.AttachComponent<Image>(true, null);
            frontimage.Tint.Value = tint;
            userButton.SetupBackgroundColor(frontimage.Tint);
            Slot imageSlot = buttonSlot.AddSlot("Image");
            Image image = imageSlot.AttachComponent<Image>();
            image.Sprite.Target = imageSlot.AttachSprite(sprite);
            image.Tint.Value = colorX.Black;
            image.RectTransform.AddFixedPadding(2f);
            userButton.LocalPressed += onPress;

            // https://github.com/Psychpsyo/Tooltippery Support, implemented based on the readme
            buttonSlot.AttachComponent<Comment>().Text.Value = "TooltipperyLabel:" + tag;
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

        static Record GetRecord(InventoryItemUI item)
        {
            return (Record)itemInfo.GetValue(item) ?? ((RecordDirectory)directoryInfo.GetValue(item)).EntryRecord;
        }

        static string GetLink(InventoryItemUI item, bool type)
        {
            Record record = GetRecord(item);
            return type ? record?.GetUrl(Engine.Current.PlatformProfile).ToString() : record?.AssetURI;
        }
    }
}