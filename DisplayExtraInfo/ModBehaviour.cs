using System;
using System.Collections.Generic;
using Duckov.Rules;
using Duckov.UI;
using Duckov.Utilities;
using ItemStatsSystem;
using SodaCraft.Localizations;
using TMPro;
using UnityEngine;
using UnityEngine.Lumin;
using UnityEngine.UI;

namespace DisplayExtraInfo
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        TextMeshProUGUI _text = null;
        TextMeshProUGUI Text
        {
            get
            {
                if (_text == null)
                {
                    _text = Instantiate(GameplayDataSettings.UIStyle.TemplateTextUGUI);
                }
                return _text;
            }
        }

        private static List<(int, int)> storage_cache = new List<(int, int)>();

        void Awake()
        {
            /*
            macOS:
                ~/Library/Logs/TeamSoda/Duckov/Player.log       # Current session log
                ~/Library/Logs/TeamSoda/Duckov/Player-prev.log  # Previous session log (auto-backup)
            Windows:
                C:\Users\<YourUsername>\AppData\LocalLow\TeamSoda\Duckov\Player.log
                C:\Users\<YourUsername>\AppData\LocalLow\TeamSoda\Duckov\Player-prev.log
            */
            Debug.Log("DisplayItemValue Loaded!!!");
        }

        void OnDestroy()
        {
            if (_text != null)
                Destroy(_text);
        }

        void OnEnable()
        {
            ItemHoveringUI.onSetupItem += OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta += OnSetupMeta;
        }

        void OnDisable()
        {
            ItemHoveringUI.onSetupItem -= OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;
        }

        private void OnSetupMeta(ItemHoveringUI uiInstance, ItemMetaData data)
        {
            SetupText(uiInstance);
            DisplayItemCount(data.id);
        }

        private void OnSetupItemHoveringUI(ItemHoveringUI uiInstance, Item item)
        {
            if (item == null)
            {
                Text.gameObject.SetActive(false);
                return;
            }

            SetupText(uiInstance);
            DisplayItemValue(item);
            DisplayItemCount(item.TypeID);
            if (item.TypeID == 389 || item.TypeID == 1252) // 自动售货机神秘留言/橘子耳机
            {
                //DisplayDifficulty();
                DisplayBossList();
            }
        }

        private void SetupText(ItemHoveringUI uiInstance)
        {
            Text.gameObject.SetActive(true);
            Text.transform.SetParent(uiInstance.LayoutParent);
            Text.transform.localScale = Vector3.one;
            Text.fontSize = 20f;
            Text.text = "";
        }

        private void DisplayItemValue(Item item)
        {
            if (Text.text.Length != 0)
                Text.text += "\n";
            Text.text += $"${item.GetTotalRawValue() / 2}";
            if (item.Stackable && item.StackCount != item.MaxStackCount)
                Text.text += $" / ${item.Value * item.MaxStackCount / 2}";
            if (item.TotalWeight >= 0.001)
                Text.text += $"\n${(int)(item.GetTotalRawValue() / item.TotalWeight / 2)} / kg";
        }

        private void DisplayItemCount(int type_id)
        {
            if (Text.text.Length != 0)
                Text.text += "\n";
            // 背包
            int count_char = 0;
            count_char += CountItem(LevelManager.Instance.MainCharacter.CharacterItem, type_id);
            count_char += CountInventory(LevelManager.Instance.PetProxy.Inventory, type_id);
            // 仓库
            int count_storage = 0;
            if (PlayerStorage.Inventory == null)
            {
                foreach (var (type_id1,count) in storage_cache)
                {
                    if (type_id1 == type_id)
                        count_storage += count;
                }
            }
            else
            {
                storage_cache = new List<(int, int)>();
                count_storage += CountInventory(PlayerStorage.Inventory, type_id, storage_cache);
            }
            int count_buffer = 0;
            // 自提柜
            foreach (var item_tree_data in PlayerStorageBuffer.Buffer)
                foreach (var e in item_tree_data.entries)
                    if (e.typeID == type_id)
                        count_buffer += e.StackCount;

            string backpack = "Backpack", storage = "Storage", buffer = "Buffer";
            if (LocalizationManager.CurrentLanguage == SystemLanguage.ChineseSimplified)
            {
                backpack = "背包"; storage = "仓库"; buffer = "自提柜";
            }

            Text.text += $"{backpack}: {count_char} {storage}: {count_storage}";
            if (count_buffer != 0)
                Text.text += $" {buffer}: {count_buffer}";
        }

        private int CountInventory(Inventory inventory, int type_id, List<(int, int)> cache = null)
        {
            if (inventory == null)
                return 0;

            int count = 0;
            foreach (var item in inventory)
                count += CountItem(item, type_id, cache);
            return count;
        }

        private int CountItem(Item item, int type_id, List<(int, int)> cache = null)
        {
            if (cache != null)
                cache.Add((item.TypeID, item.StackCount));
            int count = 0;
            if (item.TypeID == type_id)
            {
                count += item.StackCount;
            }
            if (item.Inventory != null)
                foreach (var sub_item in item.Inventory)
                    count += CountItem(sub_item, type_id, cache);
            if (item.Slots != null)
                foreach (var slot in item.Slots)
                    if (slot.Content != null)
                        count += CountItem(slot.Content, type_id, cache);
            return count;
        }

        private void DisplayDifficulty()
        {
            if (Text.text.Length != 0)
                Text.text += "\n[\n";
            var rules = GameRulesManager.Current;
            Text.text += $"难度:\t\t\t{rules.DisplayName}\n";
            Text.text += $"战争迷雾:\t\t\t{rules.FogOfWar}\n";
            Text.text += $"对玩家伤害:\t\t{rules.DamageFactor_ToPlayer}\n";
            Text.text += $"敌人血量:\t\t\t{rules.EnemyHealthFactor}\n";
            Text.text += $"后坐力系数:\t\t{rules.RecoilMultiplier}\n";
            Text.text += $"生成遗失物:\t\t{rules.SpawnDeadBody}\n";
            Text.text += $"遗失物数量:\t\t{rules.SaveDeadbodyCount}\n";
            Text.text += $"敌人反应时间:\t\t{rules.EnemyReactionTimeFactor}\n";
            Text.text += $"敌人攻击间隔:\t\t{rules.EnemyAttackTimeSpaceFactor}\n";
            Text.text += $"敌人攻击持续时间:\t{rules.EnemyAttackTimeFactor}\n";
            Text.text += $"高级负面效果:\t\t{rules.AdvancedDebuffMode}";
        }

        private void DisplayBossList()
        {
            if (Text.text.Length != 0)
                Text.text += "\n";
            Text.text += "Boss:";
            CharacterSpawnerRoot[] roots = Resources.FindObjectsOfTypeAll<CharacterSpawnerRoot>();
            foreach (var root in roots)
            {
                var list = root?.createdCharacters;
                if (list == null)
                    continue;
                foreach (var character in list)
                {
                    if (character == null)
                        continue;
                    var preset = character.characterPreset;
                    if (preset == null)
                        continue;
                    Debug.Log($"icon: {preset.characterIconType}, name: {preset.name}, DisplayName: {preset.DisplayName}");
                    if (preset.characterIconType == CharacterIconTypes.boss || preset.name == "EnemyPreset_BossMelee_SchoolBully")
                    {
                        Text.text += $"\n\t{preset.DisplayName}";
                    }
                }
            }
        }
    }
}
