using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using FrooxEngine.UIX;
using BaseX;
using System.Reflection;
using System;

namespace GetItemLink
{
    public class GetItemLink : NeosMod
    {
        public override string Name => "GetItemLink";
        public override string Author => "eia485";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/eia485/NeosGetItemLink/";
        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("net.eia485.GetItemLink");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(InventoryBrowser))]
        class GetItemLinkPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch("OnItemSelected")]
            public static void OnItemSelectedPostFix(InventoryBrowser __instance)
            {
                if (__instance.World == Userspace.UserspaceWorld)
                {
                    Slot buttonRoot = (typeof(InventoryBrowser).GetField("_buttonsRoot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance) as SyncRef<Slot>).Target[0];
                    AddButton((IButton button, ButtonEventData eventData) =>
                    {
                        ItemLink(button, __instance.SelectedInventoryItem, false);
                    },
                    "AssetURI",
                    color.Purple,
                    NeosAssets.Graphics.Badges.Cheese,
                    buttonRoot);

                    AddButton((IButton button, ButtonEventData eventData) =>
                    {
                        ItemLink(button, __instance.SelectedInventoryItem, true);
                    },
                    "URL",
                    color.Brown,
                    NeosAssets.Graphics.Badges.potato,
                    buttonRoot);

                }
            }

            [HarmonyPrefix]
            [HarmonyPatch("OnChanges")]
            public static void OnChangesPreFix(InventoryBrowser __instance)
            {
                if (__instance.World == Userspace.UserspaceWorld)
                {
                    Slot buttonRoot = (typeof(InventoryBrowser).GetField("_buttonsRoot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance) as SyncRef<Slot>).Target[0];
                    if (buttonRoot != null)
                    {
                        bool buttonState = __instance.SelectedInventoryItem != null;
                        if (buttonState)
                        {
                            RecordDirectory directory = Traverse.Create(__instance.SelectedInventoryItem).Field("Directory")
                                .GetValue<RecordDirectory>();
                            buttonState = directory == null;
                            if (!buttonState)
                            {
                                buttonState = (directory.LinkRecord != null);
                            }
                        }
                        for (int i = 0; i < buttonRoot.ChildrenCount; i++)
                        {
                            if (buttonRoot[i].Name == "AssetURI" | buttonRoot[i].Name == "URL")
                            {
                                
                                Button button = buttonRoot[i].GetComponent<Button>();
                                if (button != null)
                                {
                                    button.Enabled = buttonState;
                                }
                                    
                            }
                        }
                    }

                }
            }

        }
        public static Slot findButtonSlot(Slot slot, String name)
        {
            for (int i = 0; i < slot.ChildrenCount; i++)
            {
                if (slot[i].Name == name)
                {
                    return slot[i];
                }
            }
            return null;
        }

        public static void AddButton(ButtonEventHandler onPress, string name, color tint, Uri sprite, Slot buttonRoot)
        {
            Slot foundButtonSlot = findButtonSlot(buttonRoot, name);
            if (foundButtonSlot != null)
            {
                Image buttonImage = foundButtonSlot[0].GetComponent<Image>();
                if (buttonImage != null)
                    buttonImage.Tint.Value = color.Black;
                return;
            }
            Slot buttonSlot = buttonRoot.AddSlot(name);
            buttonSlot.AttachComponent<LayoutElement>().PreferredWidth.Value = 57.6f;
            Button userButton = buttonSlot.AttachComponent<Button>();
            Image frontimage = buttonSlot.AttachComponent<Image>(true, null);
            frontimage.Tint.Value = tint;
            userButton.SetupBackgroundColor(frontimage.Tint);
            Slot imageSlot = buttonSlot.AddSlot("image");
            Image image = imageSlot.AttachComponent<Image>();
            image.Sprite.Target = imageSlot.AttachSprite(sprite);
            image.Tint.Value = color.Black;
            image.RectTransform.AddFixedPadding(2f);
            userButton.LocalPressed += onPress;
        }

        public static void ItemLink(IButton button, InventoryItemUI Item, bool type)
        {
            string link;
            Record record = (typeof(InventoryItemUI).GetField("Item", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Item) as Record);
            if (record == null)
            {
                RecordDirectory Directory = (typeof(InventoryItemUI).GetField("Directory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Item) as RecordDirectory);
                if (Directory != null)
                    record = Directory.LinkRecord;
            }
            if (record != null)
            {
                if (type)
                    link = (record.URL.ToString());
                else
                    link = (record.AssetURI);

                if (link != null)
                {
                    System.Windows.Forms.Clipboard.SetText(link);
                    button.Slot[0].GetComponent<Image>().Tint.Value = color.White;
                }
                else
                    button.Slot[0].GetComponent<Image>().Tint.Value = color.Red;
            }
        }
    }
}