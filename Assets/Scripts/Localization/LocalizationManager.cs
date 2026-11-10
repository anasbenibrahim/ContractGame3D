using System;
using System.Collections.Generic;
using System.IO;
using Contract.Core;
using UnityEngine;

namespace Contract.Localization
{
    /// <summary>
    /// Localization system for multi-language support (Korean/English).
    /// </summary>
    public sealed class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        [Header("Languages")]
        [SerializeField] private string defaultLanguage = "en";
        [SerializeField] private TextAsset[] languageFiles;

        private Dictionary<string, Dictionary<string, string>> localizations = new Dictionary<string, Dictionary<string, string>>();
        private string currentLanguage;

        public string CurrentLanguage => currentLanguage;
        public IReadOnlyList<string> AvailableLanguages => new List<string>(localizations.Keys);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadLanguages();
            SetLanguage(defaultLanguage);
        }

        private void LoadLanguages()
        {
            foreach (TextAsset file in languageFiles)
            {
                if (file == null) continue;

                string langCode = file.name.Replace("localization_", "");
                Dictionary<string, string> dict = ParseJSON(file.text);
                if (dict.Count > 0)
                {
                    localizations[langCode] = dict;
                }
            }

            // If no files, create defaults
            if (localizations.Count == 0)
            {
                CreateDefaultLanguages();
            }
        }

        private void CreateDefaultLanguages()
        {
            // English
            var en = new Dictionary<string, string>
            {
                // Menus
                ["menu.play"] = "PLAY",
                ["menu.continue"] = "CONTINUE",
                ["menu.character"] = "CHARACTER",
                ["menu.settings"] = "SETTINGS",
                ["menu.credits"] = "CREDITS",
                ["menu.quit"] = "QUIT",

                // Character Selection
                ["character.select"] = "SELECT CHARACTER",
                ["character.speed"] = "Speed",
                ["character.stealth"] = "Stealth",
                ["character.intelligence"] = "Intelligence",
                ["character.locked"] = "LOCKED",

                // Mission Selection
                ["mission.select"] = "SELECT MISSION",
                ["mission.reward"] = "Reward: ${0}",
                ["mission.difficulty"] = "Difficulty: {0}",
                ["mission.completed"] = "COMPLETED",

                // HUD
                ["hud.objective"] = "OBJECTIVE: {0}",
                ["hud.money"] = "${0}",
                ["hud.suspicion"] = "SUSPICION",
                ["hud.interact"] = "[E] INTERACT",

                // Pause
                ["pause.resume"] = "RESUME",
                ["pause.settings"] = "SETTINGS",
                ["pause.main_menu"] = "MAIN MENU",
                ["pause.quit"] = "QUIT",

                // Settings
                ["settings.audio"] = "AUDIO",
                ["settings.graphics"] = "GRAPHICS",
                ["settings.controls"] = "CONTROLS",
                ["settings.accessibility"] = "ACCESSIBILITY",
                ["settings.master_volume"] = "Master Volume",
                ["settings.music_volume"] = "Music Volume",
                ["settings.sfx_volume"] = "SFX Volume",
                ["settings.quality"] = "Graphics Quality",
                ["settings.vsync"] = "VSync",

                // Gameplay
                ["game.new_game"] = "NEW GAME",
                ["game.save"] = "SAVE",
                ["game.load"] = "LOAD",
                ["game.victory"] = "VICTORY!",
                ["game.mission_complete"] = "MISSION COMPLETE",
                ["game.reward"] = "Reward: ${0}",
                ["game.contract_complete"] = "CONTRACT COMPLETE",
                ["game.extracting"] = "EXTRACTING...",

                // Accessibility
                ["access.colorblind"] = "Colorblind Mode",
                ["access.subtitles"] = "Subtitles",
                ["access.high_contrast"] = "High Contrast",
                ["access.reduce_motion"] = "Reduce Motion",
            };

            // Korean
            var ko = new Dictionary<string, string>
            {
                ["menu.play"] = "플레이",
                ["menu.continue"] = "이어하기",
                ["menu.character"] = "캐릭터",
                ["menu.settings"] = "설정",
                ["menu.credits"] = "크레딧",
                ["menu.quit"] = "종료",

                ["character.select"] = "캐릭터 선택",
                ["character.speed"] = "속도",
                ["character.stealth"] = "은신",
                ["character.intelligence"] = "지능",
                ["character.locked"] = "잠금",

                ["mission.select"] = "미션 선택",
                ["mission.reward"] = "보상: ${0}",
                ["mission.difficulty"] = "난이도: {0}",
                ["mission.completed"] = "완료",

                ["hud.objective"] = "목표: {0}",
                ["hud.money"] = "${0}",
                ["hud.suspicion"] = "의심도",
                ["hud.interact"] = "[E] 상호작용",

                ["pause.resume"] = "다시하기",
                ["pause.settings"] = "설정",
                ["pause.main_menu"] = "메인 메뉴",
                ["pause.quit"] = "종료",

                ["settings.audio"] = "오디오",
                ["settings.graphics"] = "그래픽",
                ["settings.controls"] = "조작",
                ["settings.accessibility"] = "접근성",
                ["settings.master_volume"] = "마스터 볼륨",
                ["settings.music_volume"] = "음악 볼륨",
                ["settings.sfx_volume"] = "효과음 볼륨",
                ["settings.quality"] = "그래픽 품질",
                ["settings.vsync"] = "수직 동기화",

                ["game.new_game"] = "새 게임",
                ["game.save"] = "저장",
                ["game.load"] = "불러오기",
                ["game.victory"] = "승리!",
                ["game.mission_complete"] = "미션 완료",
                ["game.reward"] = "보상: ${0}",
                ["game.contract_complete"] = "계약 완료",
                ["game.extracting"] = "추출 중...",

                ["access.colorblind"] = "색약 모드",
                ["access.subtitles"] = "자막",
                ["access.high_contrast"] = "고대비",
                ["access.reduce_motion"] = "모션 감소",
            };

            localizations["en"] = en;
            localizations["ko"] = ko;
        }

        private Dictionary<string, string> ParseJSON(string json)
        {
            var dict = new Dictionary<string, string>();
            try
            {
                // Simple JSON parsing for flat key-value objects
                json = json.Trim().Trim('{', '}');
                string[] pairs = json.Split(',');
                foreach (string pair in pairs)
                {
                    string[] kv = pair.Split(':');
                    if (kv.Length == 2)
                    {
                        string key = kv[0].Trim().Trim('"');
                        string value = kv[1].Trim().Trim('"');
                        dict[key] = value;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Localization] Failed to parse {e.Message}");
            }
            return dict;
        }

        public void SetLanguage(string langCode)
        {
            if (localizations.ContainsKey(langCode))
            {
                currentLanguage = langCode;
                GameEvents.RaiseLanguageChanged(langCode);
            }
            else
            {
                Debug.LogWarning($"[Localization] Language '{langCode}' not found, using default.");
                currentLanguage = defaultLanguage;
            }
        }

        public string Get(string key, params object[] args)
        {
            if (localizations.TryGetValue(currentLanguage, out Dictionary<string, string> dict))
            {
                if (dict.TryGetValue(key, out string value))
                {
                    return args.Length > 0 ? string.Format(value, args) : value;
                }
            }

            // Fallback to English
            if (currentLanguage != "en" && localizations.TryGetValue("en", out Dictionary<string, string> enDict))
            {
                if (enDict.TryGetValue(key, out string enValue))
                {
                    return args.Length > 0 ? string.Format(enValue, args) : enValue;
                }
            }

            return key; // Return key if not found
        }

        public bool HasKey(string key)
        {
            return localizations.TryGetValue(currentLanguage, out Dictionary<string, string> dict) && dict.ContainsKey(key);
        }
    }
}