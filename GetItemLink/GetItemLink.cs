using BepInEx;
using BepInEx.Configuration;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.Store;
using FrooxEngine.UIX;
using HarmonyLib;
using System.Reflection;

namespace GetItemLink
{
    [BepInDependency(BepInExResoniteShim.PluginMetadata.GUID)]
    [ResonitePlugin(PluginMetadata.GUID, PluginMetadata.NAME, PluginMetadata.VERSION, PluginMetadata.AUTHORS, PluginMetadata.REPOSITORY_URL)]
    public class GetItemLink : BasePlugin
    {
        public override void Load() => HarmonyInstance.PatchAll();

        static FieldInfo itemInfo = typeof(InventoryItemUI).GetField("Item", BindingFlags.Instance | BindingFlags.NonPublic);
        static FieldInfo directoryInfo = typeof(InventoryItemUI).GetField("Directory", BindingFlags.Instance | BindingFlags.NonPublic);
        const InventoryBrowser.SpecialItemType UniqueSIT = (InventoryBrowser.SpecialItemType)(-1);// doing this so the buttons show up on component init

        const string ButtonsRootName = "GetItemLink Buttons";
        const string GetAssetTag = "Get Asset URI";
        const string GetRecordTag = "Get Record URI";
        const string EditRecordTag = "Edit Record";

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
                        UIBuilder ui = new(buttonRoot);
                        RadiantUI_Constants.SetupDefaultStyle(ui);
                        var hori = ui.HorizontalLayout(4);
                        hori.Slot.Name = ButtonsRootName;
                        
                        // Weird workaround to force UIX reflow, otherwise buttons are invisible
                        hori.PaddingLeft.Value = 1;
                        __instance.RunInUpdates(0, () =>
                        {
                            hori.PaddingLeft.Value = 0;
                        });

                        AddButton(
                            (IButton button, ButtonEventData eventData) => ItemLink(button, __instance.SelectedInventoryItem, false),
                            GetAssetTag, colorX.Purple, OfficialAssets.Graphics.Badges.Cheese,
                            ui
                        );

                        AddButton(
                            (IButton button, ButtonEventData eventData) => ItemLink(button, __instance.SelectedInventoryItem, true),
                            GetRecordTag, colorX.Brown, OfficialAssets.Graphics.Badges.potato,
                            ui
                        );

                        AddButton(
                            (IButton button, ButtonEventData eventData) =>
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
                                AccessTools.Method(typeof(RecordEditForm), "Setup").Invoke(editForm, new object[] { null, r });
                            },
                            EditRecordTag, colorX.Orange, OfficialAssets.Graphics.Icons.Dash.Settings,
                            ui
                        );
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
                    Slot buttons = buttonRoot.FindChild(ButtonsRootName);
                    if (buttons != null)
                    {
                        foreach (var child in buttons.Children)
                        {
                            if (child.Tag == GetAssetTag)
                            {
                                child.GetComponent<Button>().Enabled = enableButtons && (GetLink(__instance.SelectedInventoryItem, false) != null);
                                child[0].GetComponent<Image>().Tint.Value = colorX.Black;
                            }
                            else if (child.Tag == GetRecordTag)
                            {
                                child.GetComponent<Button>().Enabled = enableButtons && (GetLink(__instance.SelectedInventoryItem, true) != null);
                                child[0].GetComponent<Image>().Tint.Value = colorX.Black;
                            }
                            else if (child.Tag == EditRecordTag)
                            {
                                child.GetComponent<Button>().Enabled = enableButtons && GetRecord(__instance.SelectedInventoryItem) != null;
                                child[0].GetComponent<Image>().Tint.Value = colorX.Black;
                            }
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

        public static void AddButton(ButtonEventHandler onPress, string tag, colorX tint, Uri sprite, UIBuilder ui)
        {
            var userButton = ui.Button(sprite, tint);
            var buttonSlot = userButton.Slot;
            buttonSlot.Tag = tag;
            userButton.LocalPressed += onPress;
            userButton.ColorDrivers.RemoveAt(userButton.ColorDrivers.Count - 1);
            buttonSlot[0].GetComponent<Image>().Tint.Value = colorX.Black;

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
