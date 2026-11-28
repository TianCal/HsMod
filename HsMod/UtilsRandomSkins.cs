using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
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
            //[Description("混搭：钻石/神话 + 精选")]
            Mix,
            //[Description("全部钻石/神话")]
            All
        }

        public enum RandomPetSkinMode
        {
            //[Description("禁用")]
            Disabled,
            //[Description("仅已拥有")]
            Owned,
            //[Description("全部")]
            All
        }

        // 每局为友方英雄从同职业的钻石/神话皮肤中随机选一个；混搭模式下再加上精选皮肤
        public static class RandomPremiumHeroSkin
        {
            // 混搭模式额外加入的精选皮肤，启动时从 HsMixHeroSkins.json 读取，按职业分组。
            // 每项是 dbId（数字）或 cardId（字符串），cardId 要等卡牌数据加载后才能换成 dbId，所以选皮肤时再解析。
            public const string MixFileName = "HsMixHeroSkins.json";
            public static string MixFilePath => Path.Combine(BepInEx.Paths.ConfigPath, MixFileName);
            public static Dictionary<TAG_CLASS, List<string>> MixHeroSkins = new Dictionary<TAG_CLASS, List<string>>();

            private const string DefaultMixFile = @"// 随机高级皮肤“混搭（Mix）”模式额外加入的精选皮肤，HsMod 每次启动时读取，改完要重启游戏。
// 键是职业：DEATHKNIGHT DEMONHUNTER DRUID HUNTER MAGE PALADIN PRIEST ROGUE SHAMAN WARLOCK WARRIOR
// 值是皮肤列表，每项可以写 dbId（数字）或 cardId（字符串，如 ""HERO_04bf""）。
// 同职业的钻石和神话皮肤已经内置，不用写在这里。
{
  ""PALADIN"": [
    123699, // HERO_04bf 格尔宾·梅卡托克
    83395,  // HERO_04u 火车王里诺艾
    112745, // HERO_04au 奥伯妮·亮鬃
    131822, // HERO_04bx Corrupted Leeroy / 腐化的火车王
    131081, // HERO_04cb Arator the Redeemer / 救赎者阿拉托尔
    133206  // HERO_04cg Bounty Hunter Finley / 赏金猎人芬利
  ],
  ""PRIEST"": [
    107730, // HERO_09aj_EliseStarseeker 队伍领袖伊莉斯
    67710,  // HERO_09e 军情七处安度因
    77320,  // HERO_09n 神圣巨兽安度因
    101767, // HERO_09ae 学徒赫达尼斯
    101701, // HERO_09ad 电子摇摆怀特迈恩
    127672, // HERO_09bl Twilight Father Benedictus / 暮光教父本尼迪塔斯
    127683  // HERO_09bm Twilight Cultist Whitemane / 暮光信徒怀特迈恩
  ],
  ""WARRIOR"": [
    114180, // HERO_01aq 魔铁瓦里安
    101666, // HERO_01ac 精英牛头人酋长
    107749, // HERO_01aj_Denathrius 沙龙贵宾德纳修斯
    79966,  // HERO_01q 冬天爷爷麦格尼
    112788, // HERO_01au 督军德拉卡
    78174,  // HERO_01o 范达尔·雷矛
    121638  // HERO_01bg Magni Reforged / 重塑的麦格尼
  ],
  ""DRUID"": [
    78180,  // HERO_06p 钢铁守护者玛法里奥
    83398,  // HERO_06u 费林大使
    123761, // HERO_06bi Chao Feng Lunara / 嘲风望月露娜拉
    131396, // HERO_06bx Zin-Azshari Faelin / 辛艾萨莉的费林
    114329, // HERO_06as Leyara / 莱雅娜
    123708  // HERO_06bh Old Ways Ulfar / 老派德鲁伊奥尔法
  ],
  ""WARLOCK"": [
    103774, // HERO_07af 白骨占卜师塔姆辛
    108386, // HERO_07am_Godfrey 高弗雷勋爵
    93724   // HERO_07y 加拉肯斯坦
  ],
  ""SHAMAN"": [
    105083, // HERO_02an 青玉闪电之王
    101641, // HERO_02ag 因扎
    77248,  // HERO_02n 青蛙王子萨尔
    123717  // HERO_02bm Earthen Ring Akama / 大地之环的阿卡玛
  ],
  ""MAGE"": [
    101775, // HERO_08am Kit Waxwhisker / 基特·蜡须
    130936, // HERO_08ck Millhouse Manastorm / 米尔豪斯·法力风暴
    123771, // HERO_08bv Qiu Niu Tsulong / 囚牛弄弦烛龙
    98729,  // HERO_08al Saraad / 萨兰德
    127751, // HERO_08ce Tarecgosa / 泰蕾苟萨
    103768  // HERO_08ao Celeste-ial / 星宿灵龙
  ],
  ""DEATHKNIGHT"": [
    133179, // HERO_11bl Teron Gorefiend / 塔隆·血魔
    127666, // HERO_11ax Lord Victor Nefarius / 维克多·奈法里奥斯
    119053, // HERO_11ag The Jailer / 典狱长
    130386  // HERO_11az The Future King / 未来之王
  ],
  ""DEMONHUNTER"": [
    78177,  // HERO_10l Infernal Armor Illidan / 狱火护甲伊利丹
    98361,  // HERO_10x Omen / 年兽
    133194  // HERO_10ca Wild God Omen / 荒野之神年兽
  ],
  ""ROGUE"": [
    109340, // HERO_03ak_Patches Patches the Pirate / 海盗帕奇斯
    123768, // HERO_03bk Sinestra / 希奈丝特拉
    67846,  // HERO_03f Gladiator Valeera / 角斗士瓦莉拉
    103776, // HERO_03af V-07-TR-0N / V-07-TR-0N
    127961, // HERO_03bn Dalaran Warden Maiev / 达拉然典狱官玛维
    101654  // HERO_03ab Center Stage Valeera / 舞台焦点瓦莉拉
  ],
  ""HUNTER"": [
    123758, // HERO_05bi King Maluk / 穆拉克
    127663, // HERO_05bn Magmaw / 熔喉
    130942, // HERO_05bt Wuxia Sylvanas / 女侠希尔瓦娜斯
    121603, // HERO_05bb Dino Hunter Lor'themar / 恐龙猎人洛瑟玛
    105081, // HERO_05ae Stepfather Lor'themar / 义父洛瑟玛
    84155   // HERO_05v Flameshot Sylvanas / 火焰射手希尔瓦娜斯
  ]
}
";

            public static void LoadMixSkins()
            {
                var result = new Dictionary<TAG_CLASS, List<string>>();
                try
                {
                    if (!File.Exists(MixFilePath))
                    {
                        File.WriteAllText(MixFilePath, DefaultMixFile, new System.Text.UTF8Encoding(false));
                        MyLogger(BepInEx.Logging.LogLevel.Info, $"RandomPremiumHeroSkin: created {MixFilePath}");
                    }
                    var raw = JsonConvert.DeserializeObject<Dictionary<string, List<JToken>>>(File.ReadAllText(MixFilePath));
                    foreach (var pair in raw ?? new Dictionary<string, List<JToken>>())
                    {
                        if (!Enum.TryParse(pair.Key.Trim(), true, out TAG_CLASS heroClass))
                        {
                            MyLogger(BepInEx.Logging.LogLevel.Warning, $"RandomPremiumHeroSkin: unknown class \"{pair.Key}\" in {MixFileName}");
                            continue;
                        }
                        var list = (pair.Value ?? new List<JToken>())
                            .Where(x => x != null && (x.Type == JTokenType.Integer || x.Type == JTokenType.String))
                            .Select(x => x.ToString().Trim())
                            .Where(x => x.Length > 0).ToList();
                        if (!result.TryGetValue(heroClass, out List<string> existing))
                            result[heroClass] = list;
                        else
                            existing.AddRange(list);
                    }
                    MyLogger(BepInEx.Logging.LogLevel.Info, $"RandomPremiumHeroSkin: loaded {result.Values.Sum(x => x.Count)} mix skins from {MixFileName}");
                }
                catch (Exception ex)
                {
                    MyLogger(BepInEx.Logging.LogLevel.Error, $"RandomPremiumHeroSkin: failed to read {MixFilePath}: {ex.Message}");
                }
                MixHeroSkins = result;
            }

            private static int ResolveMixSkin(string entry)
            {
                if (int.TryParse(entry, out int dbId)) return dbId;
                dbId = GameUtils.TranslateCardIdToDbId(entry);
                if (dbId <= 0)
                    MyLogger(BepInEx.Logging.LogLevel.Warning, $"RandomPremiumHeroSkin: unknown cardId \"{entry}\" in {MixFileName}");
                return dbId;
            }

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
                    // 抽皮肤出错时用原英雄，不能让异常冒到 Entity.LoadCard 里卡住对局加载
                    try { heroDbId = Pick(heroClass); }
                    catch (Exception ex)
                    {
                        heroDbId = -1;
                        MyLogger(BepInEx.Logging.LogLevel.Error, $"RandomPremiumHeroSkin: draw failed for {heroClass}, keeping the original hero: {ex}");
                    }
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
                List<int> candidates = new List<int>();
                if (PremiumHeroSkins.TryGetValue(heroClass, out List<int> premium))
                    candidates.AddRange(premium);
                if (randomPremiumHeroSkin.Value == RandomPremiumSkinMode.Mix && MixHeroSkins.TryGetValue(heroClass, out List<string> mix))
                {
                    foreach (int dbId in mix.Select(ResolveMixSkin))
                        if (dbId > 0 && !candidates.Contains(dbId)) candidates.Add(dbId);
                }
                if (candidates.Count == 0) return -1;
                return DrawFromBag(heroClass, candidates);
            }

            // 伪随机：每个职业一个“袋子”，一轮里每个皮肤各出现一次、顺序随机，抽完再洗下一轮。
            // 进度存在 HsHeroSkinBag.json，每次抽之前读、抽完写，所以重启炉石后接着上一轮；
            // 文件删掉或损坏就从新的一轮开始。
            public const string BagFileName = "HsHeroSkinBag.json";
            public static string BagFilePath => Path.Combine(BepInEx.Paths.ConfigPath, BagFileName);

            public class BagState
            {
                public List<int> bag = new List<int>();   // 这一轮还没出现的皮肤，按出现顺序
                public int last = -1;                     // 上一次抽到的皮肤
            }

            private static Dictionary<string, BagState> LoadBags()
            {
                try
                {
                    if (File.Exists(BagFilePath))
                    {
                        var file = JsonConvert.DeserializeObject<Dictionary<string, BagState>>(File.ReadAllText(BagFilePath));
                        if (file != null) return file;
                    }
                }
                catch (Exception ex)
                {
                    MyLogger(BepInEx.Logging.LogLevel.Warning, $"RandomPremiumHeroSkin: {BagFileName} unreadable, starting fresh: {ex.Message}");
                }
                return new Dictionary<string, BagState>();
            }

            private static void SaveBags(Dictionary<string, BagState> bags)
            {
                try
                {
                    string tmp = BagFilePath + ".tmp";
                    // 游戏自带的 Newtonsoft.Json 是裁剪版，没有 SerializeObject(object, Formatting)
                    File.WriteAllText(tmp, JsonConvert.SerializeObject(bags), new System.Text.UTF8Encoding(false));
                    if (File.Exists(BagFilePath)) File.Delete(BagFilePath);
                    File.Move(tmp, BagFilePath);
                }
                catch (Exception ex)
                {
                    MyLogger(BepInEx.Logging.LogLevel.Error, $"RandomPremiumHeroSkin: failed to save {BagFilePath}: {ex.Message}");
                }
            }

            private static int DrawFromBag(TAG_CLASS heroClass, List<int> candidates)
            {
                var bags = LoadBags();
                if (!bags.TryGetValue(heroClass.ToString(), out BagState state) || state == null)
                    bags[heroClass.ToString()] = state = new BagState();
                if (state.bag == null) state.bag = new List<int>();
                List<int> bag = state.bag;
                // 切换模式后候选可能变少，去掉已经不在候选里的
                bag.RemoveAll(x => !candidates.Contains(x));
                if (bag.Count == 0)
                {
                    bag.AddRange(candidates);
                    for (int i = bag.Count - 1; i > 0; i--)
                    {
                        int j = random.Next(i + 1);
                        (bag[i], bag[j]) = (bag[j], bag[i]);
                    }
                    // 避免上一轮最后一个和这一轮第一个相同，连续两局同一个皮肤
                    if (bag.Count > 1 && bag[0] == state.last)
                        (bag[0], bag[bag.Count - 1]) = (bag[bag.Count - 1], bag[0]);
                    MyLogger(BepInEx.Logging.LogLevel.Info, $"RandomPremiumHeroSkin: new round for {heroClass}, {bag.Count} skins");
                }
                int pick = bag[0];
                bag.RemoveAt(0);
                state.last = pick;
                SaveBags(bags);
                MyLogger(BepInEx.Logging.LogLevel.Info, $"RandomPremiumHeroSkin: {heroClass} {bag.Count} left in this round");
                return pick;
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

        // 每局开局（收到 CREATE_GAME）时随机选一个宠物变体，只改内存中的“宠物”设置，不写回配置文件，
        // 这样伪造宠物、酒馆宠物、棋盘角落等读取 skinPet 的功能都会用上这个随机宠物。
        public static class RandomPetSkin
        {
            private static readonly System.Random random = new System.Random();

            public static void OnPowerHistory(List<Network.PowerHistory> powerList)
            {
                if (randomPetSkin.Value == RandomPetSkinMode.Disabled || powerList == null) return;
                if (!powerList.Any(x => x != null && x.Type == Network.PowerType.CREATE_GAME)) return;
                MyLogger(BepInEx.Logging.LogLevel.Info, "RandomPetSkin: CREATE_GAME received");
                try
                {
                    int petVariantId = Pick();
                    if (petVariantId == -1)
                    {
                        MyLogger(BepInEx.Logging.LogLevel.Warning, "RandomPetSkin: no pet variant to pick");
                        return;
                    }
                    bool saveOnConfigSet = skinPet.ConfigFile.SaveOnConfigSet;
                    skinPet.ConfigFile.SaveOnConfigSet = false;
                    skinPet.Value = petVariantId;
                    skinPet.ConfigFile.SaveOnConfigSet = saveOnConfigSet;
                    MyLogger(BepInEx.Logging.LogLevel.Info, $"RandomPetSkin: => {petVariantId} {GameDbf.PetVariant.GetRecord(petVariantId)?.Name?.GetString()}");
                }
                catch (Exception ex)
                {
                    MyLogger(BepInEx.Logging.LogLevel.Error, ex);
                }
            }

            private static int Pick()
            {
                var records = GameDbf.PetVariant.GetRecords();
                List<int> pool = records
                    .Where(x => x != null && x.Enabled && x.PetId > 0)
                    .Select(x => x.ID).ToList();
                MyLogger(BepInEx.Logging.LogLevel.Info, $"RandomPetSkin: {records.Count} variants, {pool.Count} enabled");
                if (randomPetSkin.Value == RandomPetSkinMode.Owned)
                {
                    PetsManager petsManager = PetsManager.Get();
                    if (petsManager != null)
                        pool = pool.Where(x => petsManager.IsPetVariantOwned(x)).ToList();
                }
                if (pool.Count == 0) return -1;
                return pool[random.Next(pool.Count)];
            }
        }
    }
}
