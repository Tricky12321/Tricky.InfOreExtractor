using System.Collections.Generic;
using MadVandal.FortressCraft;
using UnityEngine;

namespace Tricky.InfiniteOreExtractor
{
    public class InfExtractorMachineWindow : BaseMachineWindow
    {
        public const string InterfaceName = "InfExtractorMachineWindow";

        public const string InterfaceTakeOres = "TakeOres";

        public const string InterfaceSwapCutterHead = "UpgradeDrill";

        public const string InterfaceSwapMotor = "UpgradeMotor";

        public const string InterfaceAddPower = "AddPower";

        public const string InterfaceDropOres = "DropOres";

        public const string InterfaceToggleReport = "ToggleReport";

        private static bool mDirty;

        private InfOreExtractor.eState mLastState;

        private float mLastOreTime;

        private int mLastDurability;

        private int mLastCount;

        private float mPowerPeriod;


        public override void SpawnWindow(SegmentEntity targetEntity)
        {
            GenericMachinePanelHelper.SetPanelSizeAndPosition(365, 568, 0, 323);

            InfOreExtractor infOreExtractor = targetEntity as InfOreExtractor;
            if (infOreExtractor == null)
                return;

            float x = GenericMachinePanelScript.instance.Label_Holder.transform.position.x;
            float y = GenericMachinePanelScript.instance.Label_Holder.transform.position.y;
            GenericMachinePanelScript.instance.Label_Holder.transform.position = new Vector3(x, y, 69.3f);
            manager.SetTitle("Infinite Ore Extractor");
            manager.AddPowerBar("power", 0, 0);
            manager.AddButton("power_button", "Add Power", 180, 0); 

            manager.AddIcon("cutter_head", "empty", Color.white, 0, 75);
            manager.AddBigLabel("cutter_head_name", string.Empty, Color.white, 60, 65);
            manager.AddLabel(GenericMachineManager.LabelType.OneLineFullWidth, "cutter_head_durability", "100%", Color.white, false, 60, 85);
            manager.AddLabel(GenericMachineManager.LabelType.OneLineFullWidth, "cutter_head_hint", "Click icon to upgrade", Color.white, false, 160, 85);

            manager.AddIcon("drill_motor", "empty", Color.white, 0, 143);
            manager.AddBigLabel("drill_motor_name", string.Empty, Color.white, 60, 133);
            manager.AddLabel(GenericMachineManager.LabelType.OneLineFullWidth, "drill_motor_speed", "2x speed", Color.white, false, 60, 153);
            manager.AddLabel(GenericMachineManager.LabelType.OneLineFullWidth, "drill_motor_hint", "Click icon to upgrade", Color.white, false, 160, 153);

            manager.AddLabel(GenericMachineManager.LabelType.OneLineFullWidth, "efficiency", "100% efficiency", Color.white, false, 80, 195);
            manager.AddLabel(GenericMachineManager.LabelType.OneLineFullWidth, "opm", "8 ore per minute", Color.white, false, 80, 215);
            manager.AddLabel(GenericMachineManager.LabelType.OneLineFullWidth, "power_usage", "3 per sec", Color.white, false, 80, 235);

            manager.AddBigLabel("status", "Drill Stuck", Color.white, 0, 268);

            manager.AddHugeLabel("storage_count", "99", Color.white, 0, 316);
            manager.AddIcon("storage_icon", "Copper Ore", Color.white, 60, 309);
            manager.AddRadialFillIcon("storage_next_icon", "Copper Ore", 1f, Color.white, 61, 313);
            manager.AddBigLabel("storage_type", "Copper Ore", Color.white, 115, 307);
            manager.AddLabel(GenericMachineManager.LabelType.OneLineFullWidth, "storage_hint", "Click icon to retrieve", Color.white, false, 115, 330);
            mDirty = true;
            mLastOreTime = 30f;
            mLastDurability = 10000;
            mLastCount = 99;
        }


        public override void UpdateMachine(SegmentEntity targetEntity) 
        {
            InfOreExtractor infOreExtractor = targetEntity as InfOreExtractor;
            manager.UpdatePowerBar("power", infOreExtractor.mrCurrentPower, infOreExtractor.mrMaxPower);
            if (infOreExtractor.meState != mLastState)
            {
                mDirty = true;
            }
            if (targetEntity.mbNetworkUpdated)
            {
                mDirty = true;
                targetEntity.mbNetworkUpdated = false;
            }
            if (mDirty)
            {
                UpdateState(infOreExtractor);
                mDirty = false;
            }
            if (infOreExtractor.meState == InfOreExtractor.eState.eMining || infOreExtractor.meState == InfOreExtractor.eState.eVeinDepleted)
            {
                UpdateMining(infOreExtractor);
            }
        }


        public override void OnClose(SegmentEntity targetEntity)
        {
            base.OnClose(targetEntity);
            GenericMachinePanelHelper.RestoreOriginalWindowState();
        }


        private void UpdateState(InfOreExtractor extractor)
        {
            mLastState = extractor.meState;
            string text = "empty";
            int cutterHeadID = extractor.GetCutterHeadID();
            if (cutterHeadID >= 0)
            {
                text = ItemEntry.mEntries[cutterHeadID].Sprite;
            }
            manager.UpdateIcon("cutter_head", text, Color.white);
            manager.UpdateLabel("cutter_head_name", extractor.GetCutterHeadName(), Color.white);
            text = "empty";
            int drillMotorID = extractor.GetDrillMotorID();
            if (drillMotorID >= 0)
            {
                text = ItemEntry.mEntries[drillMotorID].Sprite;
            }
            manager.UpdateIcon("drill_motor", text, Color.white);
            manager.UpdateLabel("drill_motor_name", extractor.GetDrillMotorName(), Color.white);
            string label = ((float)extractor.mnDrillRate / 1f).ToString("F2") + "x " + PersistentSettings.GetString("Speed");
            manager.UpdateLabel("drill_motor_speed", label, Color.white);
            string label2 = "Error:" + extractor.meState;
            Color color = Color.white;
            switch (extractor.meState)
            {
                case InfOreExtractor.eState.eDrillStuck:
                    label2 = PersistentSettings.GetString("Drill_Stuck");
                    color = Color.red;
                    break;
                case InfOreExtractor.eState.eFetchingEntryPoint:
                case InfOreExtractor.eState.eFetchingExtractionPoint:
                case InfOreExtractor.eState.eSearchingForOre:
                case InfOreExtractor.eState.eIdle:
                    label2 = PersistentSettings.GetString("OE_Searching");
                    break;
                case InfOreExtractor.eState.eMining:
                case InfOreExtractor.eState.eVeinDepleted:
                    label2 = PersistentSettings.GetString("OE_Mining");
                    color = Color.green;
                    break;
                case InfOreExtractor.eState.eOutOfPower:
                case InfOreExtractor.eState.eOutOfPowerVeinDepleted:
                    label2 = PersistentSettings.GetString("OE_Out_of_Power");
                    color = Color.red;
                    break;
                case InfOreExtractor.eState.eOutOfStorage:
                case InfOreExtractor.eState.eOutOfStorageVeinDepleted:
                    label2 = PersistentSettings.GetString("OE_Out_of_Storage");
                    color = Color.red;
                    break;
            }
            manager.UpdateLabel("status", label2, color);
            manager.UpdateLabel("efficiency", string.Format("{0:P0} {1}", extractor.mrEfficiency, PersistentSettings.GetString("Efficiency")), Color.white);
            int num = extractor.mnDrillRate + extractor.mnBonusOre;
            int num2 = (int)((float)num * 2f);
            manager.UpdateLabel("opm", string.Format("{0} {1}", num2, PersistentSettings.GetString("Ore_Per_Min")), Color.white);
            float num3 = extractor.mrPowerUsage * DifficultySettings.mrResourcesFactor;
            manager.UpdateLabel("power_usage", string.Format("{0} {1}", num3.ToString("N2"), PersistentSettings.GetString("Power_Per_Second")), Color.white);
            string newIcon = "empty";
            string label3 = string.Empty;
            if (extractor.mnOreType != 0)
            {
                newIcon = TerrainData.GetIconNameForValue(extractor.mnOreType, 0);
                label3 = TerrainData.GetNameForValue(extractor.mnOreType, 0);
                if (!WorldScript.mLocalPlayer.mResearch.IsKnown(extractor.mnOreType, 0))
                {
                    newIcon = "Unknown";
                    label3 = "Unknown Material";
                }
            }
            manager.UpdateLabel("storage_type", label3, Color.white);
            if (extractor.meState == InfOreExtractor.eState.eMining || extractor.meState == InfOreExtractor.eState.eVeinDepleted)
            {
                manager.UpdateIcon("storage_icon", newIcon, Color.grey);
                manager.UpdateIcon("storage_next_icon", newIcon, Color.white);
            }
            else
            {
                manager.UpdateIcon("storage_icon", newIcon, Color.white);
                manager.UpdateIcon("storage_next_icon", "empty", Color.white);
                mLastDurability = extractor.mnCutterDurability;
                float num4 = (float)mLastDurability / 10000f;
                if (extractor.mnCutterDurability == 0)
                {
                    manager.UpdateLabel("cutter_head_durability", string.Empty, Color.white);
                }
                else
                {
                    manager.UpdateLabel("cutter_head_durability", extractor.mnCutterDurability.ToString("F0"), Color.white);
                }
                mLastCount = extractor.mnStoredOre;
                Color color2 = Color.white;
                if (mLastCount == 0)
                {
                    color2 = Color.grey;
                }
                manager.UpdateLabel("storage_count", string.Format("{0,2:##}", mLastCount), color2);
            }
        }

        private void UpdateMining(InfOreExtractor extractor)
        {
            if (extractor.mrTimeUntilNextOre != mLastOreTime)
            {
                mLastOreTime = extractor.mrTimeUntilNextOre;
                float fill = mLastOreTime / 30f;
                manager.UpdateIconFill("storage_next_icon", fill);
            }
            if (extractor.mnCutterDurability != mLastDurability)
            {
                mLastDurability = extractor.mnCutterDurability;
                float num = (float)mLastDurability / 10000f;
                manager.UpdateLabel("cutter_head_durability", extractor.mnCutterDurability.ToString("F0"), Color.white);
            }
            if (extractor.mnStoredOre != mLastCount)
            {
                mLastCount = extractor.mnStoredOre;
                Color color = Color.white;
                if (mLastCount == 0)
                {
                    color = Color.grey;
                }
                manager.UpdateLabel("storage_count", string.Format("{0,2:##}", mLastCount), color);
            }
        }

        public override bool ButtonClicked(string name, SegmentEntity targetEntity)
        {
            InfOreExtractor infOreExtractor = targetEntity as InfOreExtractor;
            if (name.Equals("drill_motor"))
            {
                if (infOreExtractor.AttemptUpgradeDrillMotor(WorldScript.mLocalPlayer))
                {
                    AudioHUDManager.instance.Pick();
                    mDirty = true;
                    if (!WorldScript.mbIsServer)
                    {
                        ItemBase motor = infOreExtractor.GetMotor();
                        NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSwapMotor, null, motor, infOreExtractor, 0f);
                    }
                    return true;
                }
            }
            else if (name.Equals("cutter_head"))
            {
                if (infOreExtractor.AttemptUpgradeCutterHead(WorldScript.mLocalPlayer))
                {
                    AudioHUDManager.instance.Pick();
                    mDirty = true;
                    if (!WorldScript.mbIsServer)
                    {
                        ItemBase cutterHead = infOreExtractor.GetCutterHead();
                        NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSwapCutterHead, null, cutterHead, infOreExtractor, 0f);
                    }
                    Debug.Log("cutter head upgraded");
                    return true;
                }
            }
            else if (name.Equals("storage_icon"))
            {
                if (infOreExtractor.PlayerExtractStoredOre(WorldScript.mLocalPlayer))
                {
                    AudioHUDManager.instance.OrePickup();
                    Achievements.instance.UnlockAchievement(Achievements.eAchievements.eExtractedOre, false);
                    if (!WorldScript.mbIsServer)
                    {
                        NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceTakeOres, null, null, infOreExtractor, 0f);
                    }
                    mDirty = true;
                    return true;
                }
            }
            else if (name.Equals("power_button") && !WorldScript.mbIsServer && mPowerPeriod > 0f)
            {
                NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceAddPower, null, null, infOreExtractor, mPowerPeriod);
                mPowerPeriod = 0f;
            }
            return false;
        }

        public static bool AddPower(Player player, InfOreExtractor extractor, float period)
        {
            if (extractor.mrCurrentPower < extractor.mrMaxPower)
            {
                float num = 60f * period;
                if (player == WorldScript.mLocalPlayer && SurvivalPowerPanel.mrSuitPower < num)
                {
                    num = SurvivalPowerPanel.mrSuitPower;
                }
                if (extractor.mrCurrentPower > 200f && WorldScript.meGameMode == eGameMode.eSurvival && SurvivalPlayerScript.meTutorialState == SurvivalPlayerScript.eTutorialState.PutPowerIntoExtractor)
                {
                    SurvivalPlayerScript.TutorialSectionComplete();
                }
                float remainingPowerCapacity = extractor.GetRemainingPowerCapacity();
                if (num > remainingPowerCapacity)
                {
                    num = remainingPowerCapacity;
                }
                if (num > 0f)
                {
                    if (player == WorldScript.mLocalPlayer)
                    {
                        SurvivalPowerPanel.mrSuitPower -= num;
                    }
                    extractor.mrCurrentPower += num;
                    extractor.MarkDirtyDelayed();
                    return true;
                }
            }
            return false;
        }

        public static void ToggleReport(Player player, InfOreExtractor extractor)
        {
            if (!WorldScript.mbIsServer)
            {
                NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceToggleReport, null, null, extractor, 0f);
            }
            else
            {
                extractor.mbReportOffline = !extractor.mbReportOffline;
            }
        }

        public static bool DropStoredOre(Player player, InfOreExtractor extractor)
        {
            if (extractor.DropStoredOre())
            {
                if (!WorldScript.mbIsServer)
                {
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceDropOres, null, null, extractor, 0f);
                }
                return true;
            }
            return false;
        }

        public override void ButtonDown(string name, SegmentEntity targetEntity)
        {
            InfOreExtractor infOreExtractor = targetEntity as InfOreExtractor;
            if (name.Equals("power_button") && AddPower(WorldScript.mLocalPlayer, infOreExtractor, Time.deltaTime) && !WorldScript.mbIsServer)
            {
                mPowerPeriod += Time.deltaTime;
                if (mPowerPeriod >= 1f)
                {
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceAddPower, null, null, infOreExtractor, 1f);
                    mPowerPeriod -= 1f;
                }
            }
        }

        public override ItemBase GetDragItem(string name, SegmentEntity targetEntity)
        {
            InfOreExtractor infOreExtractor = targetEntity as InfOreExtractor;
            if (name.Equals("drill_motor"))
            {
                return infOreExtractor.GetMotor();
            }
            if (name.Equals("cutter_head"))
            {
                return infOreExtractor.GetCutterHead();
            }
            if (name.Equals("storage_icon"))
            {
                return infOreExtractor.GetStoredOre();
            }
            return null;
        }

        public override bool RemoveItem(string name, ItemBase originalItem, ItemBase swapitem, SegmentEntity targetEntity)
        {
            InfOreExtractor infOreExtractor = targetEntity as InfOreExtractor;
            if (name.Equals("drill_motor"))
            {
                if (swapitem == null)
                {
                    infOreExtractor.SwapMotor(null);
                    mDirty = true;
                    if (!WorldScript.mbIsServer)
                    {
                        NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSwapMotor, null, null, infOreExtractor, 0f);
                    }
                    return true;
                }
                if (infOreExtractor.IsValidMotor(swapitem))
                {
                    ItemStack itemStack = swapitem as ItemStack;
                    if (itemStack.mnAmount > 1)
                    {
                        return false;
                    }
                    infOreExtractor.SwapMotor(swapitem);
                    mDirty = true;
                    if (!WorldScript.mbIsServer)
                    {
                        NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSwapMotor, null, swapitem, infOreExtractor, 0f);
                    }
                    return true;
                }
            }
            else if (name.Equals("cutter_head"))
            {
                if (swapitem == null)
                {
                    infOreExtractor.SwapCutterHead(null);
                    mDirty = true;
                    if (!WorldScript.mbIsServer)
                    {
                        NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSwapCutterHead, null, null, infOreExtractor, 0f);
                    }
                    return true;
                }
                if (infOreExtractor.IsValidCutterHead(swapitem))
                {
                    infOreExtractor.SwapCutterHead(swapitem);
                    mDirty = true;
                    if (!WorldScript.mbIsServer)
                    {
                        NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSwapCutterHead, null, swapitem, infOreExtractor, 0f);
                    }
                    return true;
                }
            }
            else if (name.Equals("storage_icon"))
            {
                if (swapitem != null)
                {
                    return false;
                }
                infOreExtractor.ClearStoredOre();
                if (!WorldScript.mbIsServer)
                {
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceTakeOres, null, null, infOreExtractor, 0f);
                }
                Achievements.instance.UnlockAchievement(Achievements.eAchievements.eExtractedOre, false);
                mDirty = true;
                return true;
            }
            return false;
        }

        public override void HandleItemDrag(string name, ItemBase draggedItem, DragAndDropManager.DragRemoveItem dragDelegate, SegmentEntity targetEntity)
        {
            InfOreExtractor infOreExtractor = targetEntity as InfOreExtractor;
            if (name.Equals("drill_motor"))
            {
                if (infOreExtractor.IsValidMotor(draggedItem))
                {
                    ItemStack itemStack = draggedItem as ItemStack;
                    ItemBase motor = infOreExtractor.GetMotor();
                    if (itemStack.mnAmount > 1)
                    {
                        if (motor != null && WorldScript.mLocalPlayer.mInventory.FitItem(motor) == 0)
                        {
                            Debug.Log("Trying to fit stack, but player inventory can't fit replacement motor");
                            return;
                        }
                        itemStack.mnAmount--;
                    }
                    else if (!dragDelegate(draggedItem, motor))
                    {
                        return;
                    }
                    infOreExtractor.SwapMotor(draggedItem);
                    mDirty = true;
                    InventoryPanelScript.MarkDirty();
                    if (!WorldScript.mbIsServer)
                    {
                        NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSwapMotor, null, draggedItem, infOreExtractor, 0f);
                    }
                }
            }
            else if (name.Equals("cutter_head") && infOreExtractor.IsValidCutterHead(draggedItem))
            {
                ItemBase cutterHead = infOreExtractor.GetCutterHead();
                if (dragDelegate(draggedItem, cutterHead))
                {
                    infOreExtractor.SwapCutterHead(draggedItem);
                    mDirty = true;
                    InventoryPanelScript.MarkDirty();
                    if (!WorldScript.mbIsServer)
                    {
                        NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSwapCutterHead, null, draggedItem, infOreExtractor, 0f);
                    }
                }
            }
        }

        public static NetworkInterfaceResponse HandleNetworkCommand(Player player, NetworkInterfaceCommand nic)
        {
            InfOreExtractor infOreExtractor = nic.target as InfOreExtractor;
            string command = nic.command;
            switch (command)
            {
                case InterfaceTakeOres:
                    infOreExtractor.PlayerExtractStoredOre(player);
                    break;
                case InterfaceSwapMotor:
                    infOreExtractor.SwapMotor(nic.itemContext);
                    break;
                case InterfaceSwapCutterHead:
                    infOreExtractor.SwapCutterHead(nic.itemContext);
                    break;
                case InterfaceAddPower:
                    AddPower(player, infOreExtractor, nic.holdPeriod);
                    break;
                case InterfaceDropOres:
                    infOreExtractor.ClearStoredOre();
                    break;
                case InterfaceToggleReport:
                    infOreExtractor.mbReportOffline = !infOreExtractor.mbReportOffline;
                    infOreExtractor.RequestImmediateNetworkUpdate();
                    break;
            }
            return new NetworkInterfaceResponse
            {
                entity = infOreExtractor,
                inventory = player.mInventory
            };
        }

        public override List<HandbookContextEntry> GetContextualHelp(SegmentEntity targetEntity)
        {
            List<HandbookContextEntry> list = new List<HandbookContextEntry>();
            InfOreExtractor infOreExtractor = targetEntity as InfOreExtractor;
            list.Add(HandbookContextEntry.Material(750, infOreExtractor.mCube, infOreExtractor.mValue, "Selected Machine"));
            ushort mnOreType = infOreExtractor.mnOreType;
            if (mnOreType != 0)
            {
                if (WorldScript.mLocalPlayer.mResearch.IsKnown(mnOreType, 0))
                {
                    list.Add(HandbookContextEntry.Material(641, infOreExtractor.mnOreType, 0, "Output of Ore Extractor"));
                }
                else
                {
                    list.Add(HandbookContextEntry.Guide(641, "unknown materials"));
                }
            }
            return list;
        }

        public static void SetDirty()
        {
            mDirty = true;
        }
    }
}
