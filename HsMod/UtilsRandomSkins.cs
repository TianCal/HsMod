using System;
using System.Collections.Generic;
using System.Linq;
using static HsMod.PluginConfig;

namespace HsMod
{
    public partial class Utils
    {
        public enum RandomPremiumSkinMode
        {
            //[Description("禁用")]
            Disabled,
            //[Description("仅已拥有")]
            Owned,
            //[Description("全部")]
            All
        }

        // 每局为友方英雄从同职业的钻石/神话皮肤中随机选一个
        public static class RandomPremiumHeroSkin
        {
            // 可收藏的钻石/神话英雄皮肤 dbId，按职业分组（不含变身形态）。
            // 来源：HearthstoneJSON CardDefs.xml build 255790，英雄卡带 HERO_FRAME_TYPE(3495) 为钻石，
            // 带 CORNER_REPLACEMENT_TYPE(3564) 为神话。新皮肤上线后需要手动补充。
            public static readonly Dictionary<TAG_CLASS, List<int>> PremiumHeroSkins = new Dictionary<TAG_CLASS, List<int>>
            {
                { TAG_CLASS.DEATHKNIGHT, new List<int> {
                    112782, // diamond HERO_11z Bolvar Fordragon / 伯瓦尔·弗塔根
                    119017, // diamond HERO_11ae Ursoc / 乌索克
                    127654, // diamond HERO_11aw Undead Katrana / 亡灵卡特拉娜
                    119727, // mythic HERO_11aj Kerrigan / 凯瑞甘
                    120228, // mythic HERO_11ai Arachnid Kerrigan / 蜘蛛女王凯瑞甘
                    133683, // mythic HERO_11be Lich Lord Kel'Thuzad / 巫妖领主克尔苏加德
                    133685, // mythic HERO_11be_necro Lich Lord Kel'Thuzad / 巫妖领主克尔苏加德
                } },
                { TAG_CLASS.DEMONHUNTER, new List<int> {
                    114192, // diamond HERO_10am Metamorphosis Illidan / 恶魔变形伊利丹
                    116137, // mythic HERO_10aw Kerrigan / 凯瑞甘
                    119500, // mythic HERO_10az Felfire Ragnaros / 邪火拉格纳罗斯
                    121713, // mythic HERO_10bd Arachnid Kerrigan / 蜘蛛女王凯瑞甘
                } },
                { TAG_CLASS.DRUID, new List<int> {
                    119049, // diamond HERO_06ay Winter Queen / 寒冬女王
                    126110, // mythic HERO_06bj Empress Alexstrasza / 女皇阿莱克丝塔萨
                    131947, // mythic HERO_06bw Dragonqueen Alexstrasza / 红龙女王阿莱克丝塔萨
                } },
                { TAG_CLASS.HUNTER, new List<int> {
                    121587, // diamond HERO_05ba Gonk / 贡克
                    131078, // diamond HERO_05bz Qixi Alleria / 七夕奥蕾莉亚
                    119730, // mythic HERO_05az Kerrigan / 凯瑞甘
                    121716, // mythic HERO_05bf Arachnid Kerrigan / 蜘蛛女王凯瑞甘
                    130591, // mythic HERO_05bm Lion's Landing Varian / 青龙降世瓦里安
                    130592, // mythic HERO_05bm_gold Lion's Landing Varian / 御天赤龙瓦里安
                } },
                { TAG_CLASS.MAGE, new List<int> {
                    112753, // diamond HERO_08bc Malygos / 玛里苟斯
                    127657, // diamond HERO_08cb Duality Azshara / 双面艾萨拉
                    121614, // mythic HERO_08bn Darkmoon Yogg-Saron / 暗月尤格-萨隆
                    127935, // mythic HERO_08ch Dragonqueen Alexstrasza / 红龙女王阿莱克丝塔萨
                    131091, // mythic HERO_08cs Lich Lord Kel'Thuzad / 巫妖领主克尔苏加德
                    133684, // mythic HERO_08cs_necro Lich Lord Kel'Thuzad / 巫妖领主克尔苏加德
                } },
                { TAG_CLASS.PALADIN, new List<int> {
                    116108, // diamond HERO_04ay Tirion Fordring / 提里奥·弗丁
                    131099, // diamond HERO_04cc Explorer Liadrin / 探险者莉亚德琳
                    123730, // mythic HERO_04bk Turalyon / 图拉扬
                    129950, // mythic HERO_04bo Lightlord Ragnaros / 光耀之主拉格纳罗斯
                } },
                { TAG_CLASS.PRIEST, new List<int> {
                    112705, // diamond HERO_09ar Talanji / 塔兰吉
                    121626, // diamond HERO_09bd Xal'atath / 萨拉塔斯
                    121646, // mythic HERO_09be Empress Alexstrasza / 女皇阿莱克丝塔萨
                    128792, // mythic HERO_09bq Turalyon / 图拉扬
                    132172, // mythic HERO_09bw Maiev, Hand of Vengeance / 复仇之手玛维
                    133167, // mythic HERO_09ce Y'Shaarj / 亚煞极
                } },
                { TAG_CLASS.ROGUE, new List<int> {
                    121643, // diamond HERO_03bg Tess Greymane / 苔丝·格雷迈恩
                    123698, // diamond HERO_03bi Mob Boss Valeera / 黑帮老大瓦莉拉
                    127745, // diamond HERO_03bm Mathias Shaw / 马迪亚斯·肖尔
                    130927, // mythic HERO_03bs Maiev, Hand of Vengeance / 复仇之手玛维
                } },
                { TAG_CLASS.SHAMAN, new List<int> {
                    111235, // diamond HERO_02at_Nerzhul_hl Ner'zhul / 耐奥祖
                    123695, // diamond HERO_02bj Farseer Wo / 先知者沃
                    127938, // diamond HERO_02bu Al'Akir / 奥拉基尔
                    112742, // mythic HERO_02ax Ragnaros the Firelord / 炎魔之王拉格纳罗斯
                    131113, // mythic HERO_02bx Deathwing / 死亡之翼
                    133170, // mythic HERO_02cd Y'Shaarj / 亚煞极
                } },
                { TAG_CLASS.WARLOCK, new List<int> {
                    127950, // diamond HERO_07bs Twilight God Cho'gall / 暮光之神古加尔
                    129094, // diamond HERO_07bt Murozond the Inevitable / 宿命巨龙姆诺兹多
                    119437, // mythic HERO_07bc Felfire Ragnaros / 邪火拉格纳罗斯
                    119733, // mythic HERO_07bd Kerrigan / 凯瑞甘
                    121724, // mythic HERO_07bg Arachnid Kerrigan / 蜘蛛女王凯瑞甘
                    124589, // mythic HERO_07bl Darkmoon Yogg-Saron / 暗月尤格-萨隆
                    131844, // mythic HERO_07ca Qixi Turalyon / 七夕图拉扬
                } },
                { TAG_CLASS.WARRIOR, new List<int> {
                    116160, // diamond HERO_01az Genn Greymane / 吉恩·格雷迈恩
                    119076, // diamond HERO_01bd Wrathion / 拉希奥
                    120231, // diamond HERO_01be Mecha'thun / 机械克苏恩
                    130930, // diamond HERO_01bw Dr. Boom / 砰砰博士
                    133173, // diamond HERO_01cb Diablo, Lord of Terror / 迪亚波罗，恐惧之王
                    117487, // mythic HERO_01bb Ragnaros the Firelord / 炎魔之王拉格纳罗斯
                    123765, // mythic HERO_01bk Lion's Landing Varian / 青龙降世瓦里安
                    127651, // mythic HERO_01bn Deathwing / 死亡之翼
                    129325, // mythic HERO_01bk_gold Lion's Landing Varian / 御天赤龙瓦里安
                } },
            };

            private static readonly System.Random random = new System.Random();
            private static readonly Dictionary<TAG_CLASS, int> picks = new Dictionary<TAG_CLASS, int>();
            private static GameState cachedGameState = null;
            private static HashSet<string> basicHeroPowers = null;

            public static bool TryGetHero(TAG_CLASS heroClass, out int heroDbId)
            {
                heroDbId = -1;
                if (randomPremiumHeroSkin.Value == RandomPremiumSkinMode.Disabled) return false;
                GameMgr gameMgr = GameMgr.Get();
                if (gameMgr == null || gameMgr.IsBattlegrounds() || gameMgr.IsMercenaries()) return false;
                GameState gameState = GameState.Get();
                if (gameState == null) return false;

                if (!ReferenceEquals(gameState, cachedGameState))
                {
                    picks.Clear();
                    cachedGameState = gameState;
                }
                if (!picks.TryGetValue(heroClass, out heroDbId))
                {
                    heroDbId = Pick(heroClass);
                    picks[heroClass] = heroDbId;
                    MyLogger(BepInEx.Logging.LogLevel.Info, $"RandomPremiumHeroSkin: {heroClass} => {heroDbId} {(heroDbId == -1 ? "" : GameUtils.TranslateDbIdToCardId(heroDbId))}");
                }
                return heroDbId != -1;
            }

            // 只替换英雄的基础技能，避免覆盖英雄牌等带来的新技能
            public static bool TryGetHeroPower(string heroPowerCardId, TAG_CLASS heroClass, out string newHeroPowerCardId)
            {
                newHeroPowerCardId = null;
                if (randomPremiumHeroSkin.Value == RandomPremiumSkinMode.Disabled) return false;
                if (!IsBasicHeroPower(heroPowerCardId)) return false;
                if (!TryGetHero(heroClass, out int heroDbId)) return false;
                newHeroPowerCardId = GameUtils.GetHeroPowerCardIdFromHero(heroDbId);
                return !string.IsNullOrEmpty(newHeroPowerCardId);
            }

            private static int Pick(TAG_CLASS heroClass)
            {
                if (!PremiumHeroSkins.TryGetValue(heroClass, out List<int> pool) || pool.Count == 0) return -1;
                List<int> candidates = pool;
                if (randomPremiumHeroSkin.Value == RandomPremiumSkinMode.Owned)
                {
                    try
                    {
                        CollectionManager collectionManager = CollectionManager.Get();
                        candidates = pool.Where(x => collectionManager.IsCardOwned(GameUtils.TranslateDbIdToCardId(x))).ToList();
                    }
                    catch (Exception ex)
                    {
                        MyLogger(BepInEx.Logging.LogLevel.Error, ex);
                    }
                }
                if (candidates.Count == 0) return -1;
                return candidates[random.Next(candidates.Count)];
            }

            private static bool IsBasicHeroPower(string heroPowerCardId)
            {
                if (basicHeroPowers == null)
                {
                    var powers = new HashSet<string>();
                    foreach (CardHeroDbfRecord record in GameDbf.CardHero.GetRecords())
                    {
                        if (record.HeroType != Assets.CardHero.HeroType.VANILLA && record.HeroType != Assets.CardHero.HeroType.HONORED)
                            continue;
                        string power = GameUtils.GetHeroPowerCardIdFromHero(record.CardId);
                        if (!string.IsNullOrEmpty(power)) powers.Add(power);
                    }
                    basicHeroPowers = powers;
                }
                return basicHeroPowers.Contains(heroPowerCardId);
            }
        }
    }
}
