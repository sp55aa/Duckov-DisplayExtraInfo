using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Duckov.Modding;
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
    [System.Serializable]
    public class ModuleConfig
    {
        // 是否显示物品价值
        public bool showItemValue = true;
        // 显示每格价值
        public bool showItemSlotValue = true;
        // 显示每kg价值
        public bool showItemKgValue = true;
        // 显示物品数量
        public bool showItemCount = true;
        // 显示boss列表
        public bool showBossList = true;
        // 文字大小
        public float fontSize = 20f;
        // 强制更新配置文件token
        public string configToken = "display_extra_info_v1";
    }

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public static string MOD_NAME = "DisplayExtraInfo";

        ModuleConfig config = new ModuleConfig();

        private static string persistentConfigPath => Path.Combine(Application.streamingAssetsPath, $"{MOD_NAME}Config.txt");

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
            Debug.Log($"{MOD_NAME} Loaded!!!");
        }

        void OnDestroy()
        {
            if (_text != null)
                Destroy(_text);
            storage_cache.Clear();
        }

        void OnEnable()
        {
            ModManager.OnModActivated += OnModActivated;
            // 立即检查一次，防止 ModConfig 已经加载但事件错过了
            if (ModConfigAPI.IsAvailable())
            {
                Debug.Log($"{MOD_NAME}: ModConfig already available!");
                SetupModConfig();
                LoadConfigFromModConfig();
            }

            ItemHoveringUI.onSetupItem += OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta += OnSetupMeta;
        }

        void OnDisable()
        {
            ItemHoveringUI.onSetupItem -= OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;

            ModManager.OnModActivated -= OnModActivated;
            ModConfigAPI.SafeRemoveOnOptionsChangedDelegate(OnModConfigOptionsChanged);
        }

        private void OnModActivated(ModInfo info, Duckov.Modding.ModBehaviour behaviour)
        {
            if (info.name == ModConfigAPI.ModConfigName)
            {
                Debug.Log($"{MOD_NAME}: ModConfig activated!");
                SetupModConfig();
                LoadConfigFromModConfig();
            }
        }
        private bool IsChinese()
        {
            // 根据当前语言设置描述文字
            SystemLanguage[] chineseLanguages = {
                SystemLanguage.Chinese,
                SystemLanguage.ChineseSimplified,
                SystemLanguage.ChineseTraditional
            };
            return chineseLanguages.Contains(LocalizationManager.CurrentLanguage);
        }

        private void SetupModConfig()
        {
            if (!ModConfigAPI.IsAvailable())
            {
                Debug.LogWarning($"{MOD_NAME}: ModConfig not available");
                return;
            }

            Debug.Log($"{MOD_NAME}: 准备添加ModConfig配置项");

            // 添加配置变更监听
            ModConfigAPI.SafeAddOnOptionsChangedDelegate(OnModConfigOptionsChanged);

            bool isChinese = IsChinese();

            // 不知道为啥, 添加的顺序和游戏里面看到的顺序反过来了
            ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "fontSize", isChinese ? "字体大小" : "Font Size", typeof(float), config.fontSize, new Vector2(10f, 40f));

            ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "showBossList",      isChinese ? "显示Boss列表"     : "Show Boss List",          config.showBossList);
            ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "showItemCount", isChinese ? "显示数量" : "Show Item Value", config.showItemCount);
            ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "showItemKgValue", isChinese ? "显示每Kg物品价值" : "Show Item Kg Value", config.showItemKgValue);
            ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "showItemSlotValue", isChinese ? "显示每格物品价值" : "Show Item Slot Value", config.showItemSlotValue);
            ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "showItemValue", isChinese ? "显示物品价值" : "Show Item Value", config.showItemValue);

            Debug.Log($"{MOD_NAME}: ModConfig setup completed");
        }

        private void LoadConfigFromModConfig()
        {
            // 使用新的 LoadConfig 方法读取所有配置
            config.showItemValue     = ModConfigAPI.SafeLoad<bool> (MOD_NAME, "showItemValue",     config.showItemValue);
            config.showItemSlotValue = ModConfigAPI.SafeLoad<bool> (MOD_NAME, "showItemSlotValue", config.showItemSlotValue);
            config.showItemKgValue   = ModConfigAPI.SafeLoad<bool> (MOD_NAME, "showItemKgValue",   config.showItemKgValue);
            config.showItemCount     = ModConfigAPI.SafeLoad<bool> (MOD_NAME, "showItemCount",     config.showItemCount);
            config.showBossList      = ModConfigAPI.SafeLoad<bool> (MOD_NAME, "showBossList",      config.showBossList);
            config.fontSize          = ModConfigAPI.SafeLoad<float>(MOD_NAME, "fontSize",          config.fontSize);
        }

        private void OnModConfigOptionsChanged(string key)
        {
            if (!key.StartsWith(MOD_NAME + "_"))
                return;
            // 使用新的 LoadConfig 方法读取配置
            LoadConfigFromModConfig();
            // 保存到本地配置文件
            SaveConfig(config);
            // 更新当前显示的文本样式（如果正在显示）
            UpdateTextStyle();
            Debug.Log($"{MOD_NAME}: ModConfig updated - {key}");
        }

        private void SaveConfig(ModuleConfig config)
        {
            try
            {
                string json = JsonUtility.ToJson(config, true);
                File.WriteAllText(persistentConfigPath, json);
                Debug.Log($"{MOD_NAME}: Config saved");
            }
            catch (Exception e)
            {
                Debug.LogError($"{MOD_NAME}: Failed to save config: {e}");
            }
        }

        private void UpdateTextStyle()
        {
            Text.fontSize = config.fontSize;
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
            Text.fontSize = config.fontSize;
            Text.text = "";
        }

        private void DisplayItemValue(Item item)
        {
            if (!config.showItemValue)
                return;
            if (Text.text.Length != 0)
                Text.text += "\n";
            Text.text += $"${item.GetTotalRawValue() / 2}";
            if (config.showItemSlotValue && item.Stackable && item.StackCount != item.MaxStackCount)
                Text.text += $" / ${item.Value * item.MaxStackCount / 2}";
            if (config.showItemKgValue && item.TotalWeight >= 0.001)
                Text.text += $"\n${(int)(item.GetTotalRawValue() / item.TotalWeight / 2)} / kg";
        }

        private void DisplayItemCount(int type_id)
        {
            if (!config.showItemCount)
                return;
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

        /*private void DisplayDifficulty()
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
        }*/

        private void DisplayBossList()
        {
            if (!config.showBossList)
                return;
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
                    //Debug.Log($"{MOD_NAME}: icon: {preset.characterIconType}, name: {preset.name}, DisplayName: {preset.DisplayName}");
                    if (preset.characterIconType == CharacterIconTypes.boss || preset.name == "EnemyPreset_BossMelee_SchoolBully")
                        Text.text += $"\n\t{preset.DisplayName}";
                }
            }
        }
    }
}
