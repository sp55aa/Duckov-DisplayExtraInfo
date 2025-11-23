using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
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
        // 在什么物品显示Boss列表
        public string bossListItemIDs = "389,1252";
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
            LevelManager.OnLevelInitialized += UpdateStorageCache;
        }

        void OnDisable()
        {
            ItemHoveringUI.onSetupItem -= OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;
            LevelManager.OnLevelInitialized -= UpdateStorageCache;

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
            ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "bossListItemIDs", isChinese ? "显示Boss的物品ID (默认389,1252)" : "Boss List Item IDs (default 389,1252)", typeof(string), config.bossListItemIDs, null);

            ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "fontSize", isChinese ? "字体大小" : "Font Size", typeof(float), config.fontSize, new Vector2(10f, 40f));

            ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "showBossList",      isChinese ? "显示Boss列表"     : "Show Boss List",       config.showBossList);
            ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "showItemCount",     isChinese ? "显示数量"         : "Show Item Value",      config.showItemCount);
            ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "showItemKgValue",   isChinese ? "显示每Kg物品价值" : "Show Item Kg Value",   config.showItemKgValue);
            ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "showItemSlotValue", isChinese ? "显示每格物品价值" : "Show Item Slot Value", config.showItemSlotValue);
            ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "showItemValue",     isChinese ? "显示物品价值"     : "Show Item Value",      config.showItemValue);

            Debug.Log($"{MOD_NAME}: ModConfig setup completed");
        }

        private void LoadConfigFromModConfig()
        {
            // 使用新的 LoadConfig 方法读取所有配置
            config.bossListItemIDs   = ModConfigAPI.SafeLoad<string>(MOD_NAME, "bossListItemIDs",   config.bossListItemIDs);
            config.fontSize          = ModConfigAPI.SafeLoad<float> (MOD_NAME, "fontSize",          config.fontSize);
            config.showBossList      = ModConfigAPI.SafeLoad<bool>  (MOD_NAME, "showBossList",      config.showBossList);
            config.showItemCount     = ModConfigAPI.SafeLoad<bool>  (MOD_NAME, "showItemCount",     config.showItemCount);
            config.showItemKgValue   = ModConfigAPI.SafeLoad<bool>  (MOD_NAME, "showItemKgValue",   config.showItemKgValue);
            config.showItemSlotValue = ModConfigAPI.SafeLoad<bool>  (MOD_NAME, "showItemSlotValue", config.showItemSlotValue);
            config.showItemValue     = ModConfigAPI.SafeLoad<bool>  (MOD_NAME, "showItemValue",     config.showItemValue);
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
            if (config.showItemCount)
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
            if (config.showItemValue)
                DisplayItemValue(item);
            if (config.showItemCount)
                DisplayItemCount(item.TypeID);
            if (config.showBossList && IsSpecialItem(item.TypeID))
                DisplayBossList();
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
            if (IsChinese())
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

        private void UpdateStorageCache()
        {
            if (PlayerStorage.Inventory == null)
                return;
            Debug.Log($"{MOD_NAME}: UpdateStorageCache");
            storage_cache = new List<(int, int)>();
            int type_id = 0;
            CountInventory(PlayerStorage.Inventory, type_id, storage_cache);
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
                    //Debug.Log($"{MOD_NAME}: icon: {preset.characterIconType}, name: {preset.name}, DisplayName: {preset.DisplayName}, damageMultiplier: {preset.damageMultiplier}");
                    if (preset.characterIconType == CharacterIconTypes.boss || preset.name == "EnemyPreset_BossMelee_SchoolBully")
                        Text.text += $"\n\t{preset.DisplayName}";
                }
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

        private bool IsSpecialItem(int type_id)
        {
            try
            {
                var ids = new List<int>(Array.ConvertAll(config.bossListItemIDs.Split(","), int.Parse));
                return ids.Contains(type_id);
            }
            catch
            {
                Debug.LogError($"{MOD_NAME}: invalid bossListItemIDs: {config.bossListItemIDs}, use default 389,1252");
                return type_id == 389 || type_id == 1252; // 自动售货机神秘留言/橘子耳机
            }
        }
    }
}
