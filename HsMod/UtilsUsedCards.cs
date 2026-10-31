using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static HsMod.PluginConfig;

namespace HsMod
{
    public partial class Utils
    {
        // 读取 tools/used_cards.py 生成的 HsUsedCards.json，找出“没用”的卡里可以分解的卡
        public static class UsedCards
        {
            public const string FileName = "HsUsedCards.json";

            public class UsedCardEntry
            {
                public string name;
                public Dictionary<string, int> games = new Dictionary<string, int>();
                public Dictionary<string, Dictionary<string, int>> decks = new Dictionary<string, Dictionary<string, int>>();   // 赛季 -> 卡组类型 -> 对局数
                public string firstSeen;
                public string lastSeen;
            }

            public class UsedCardsFile
            {
                public List<Dictionary<string, object>> runs = new List<Dictionary<string, object>>();
                public Dictionary<string, UsedCardEntry> cards = new Dictionary<string, UsedCardEntry>();
            }

            // 页面上的一行：某张卡的某个版本（普通或金卡）
            public struct Candidate
            {
                public string CardId;
                public string Name;
                public TAG_PREMIUM Premium;
                public int Count;          // 这个版本拥有的张数，也就是要分解的张数
                public int NormalCount;
                public int GoldenCount;
                public int SellValue;      // 单张分解价
                public bool KeepNormal;    // “金卡”范围：分解前要求普通版仍在
                public TAG_RARITY Rarity;
                public TAG_CARD_SET Set;
                public TAG_CARDTYPE CardType;
                public int Cost;
                public string Stats;       // 随从“攻/血”、武器“攻/耐久”、地标“耐久”，其他为空
                public string Text;        // 卡牌描述（含 <b>/<i> 等标记，网页显示前再清理）
                public int MaxGames;       // 单个模式单个赛季里的最高对局数，0 表示从没出现过
                public string GamesKey;    // 最高对局数对应的“模式:赛季”
                public List<KeyValuePair<string, int>> Decks;   // 那个赛季里用到这张卡最多的卡组类型
            }

            private static readonly object cacheLock = new object();
            private static UsedCardsFile cachedFile = null;
            private static DateTime cachedFileTime = DateTime.MinValue;

            public static string FilePath => Path.Combine(BepInEx.Paths.ConfigPath, FileName);

            // 网页请求各自在线程池里跑，缓存需要加锁
            public static UsedCardsFile Load()
            {
                if (!File.Exists(FilePath)) return null;
                lock (cacheLock)
                {
                    DateTime time = File.GetLastWriteTimeUtc(FilePath);
                    if (cachedFile == null || time != cachedFileTime)
                    {
                        UsedCardsFile file = JsonConvert.DeserializeObject<UsedCardsFile>(File.ReadAllText(FilePath));
                        if (file?.cards == null)
                            throw new InvalidDataException($"{FilePath} 是空的或格式不对，请重新运行 tools/used_cards.py。");
                        cachedFile = file;
                        cachedFileTime = time;
                    }
                    return cachedFile;
                }
            }

            private static string GetStats(EntityDef def)
            {
                if (def == null) return "";
                if (def.IsMinion() || def.IsWeapon()) return $"{def.GetATK()}/{def.GetHealth()}";   // 武器耐久度现在存在 HEALTH 里
                if (def.IsLocation()) return def.GetHealth().ToString();
                if (def.IsHero() && def.GetArmor() > 0) return $"护甲 {def.GetArmor()}";
                return "";
            }

            // 网页线程里只读卡牌数据库的原始描述：GetCardTextInHand 会经过各种 CardTextBuilder，可能用到只能在主线程访问的游戏状态。
            // 原始描述里 $3（法术伤害）、#3（治疗）只是在数字前加了标记，去掉标记即可
            private static string GetText(EntityDef def)
            {
                try
                {
                    if (def == null) return "";
                    string text = CardTextBuilder.GetRawCardTextInHand(def.GetCardId()) ?? "";
                    // “@”分隔的是条件触发后的替代文字，只显示第一段
                    int at = text.IndexOf('@');
                    if (at >= 0) text = text.Substring(0, at);
                    // 和游戏一样：$/# 后可带 a（攻击力加成）或 d（护甲加成），后面跟数字；其余单独的 $/# 丢掉
                    text = System.Text.RegularExpressions.Regex.Replace(text, @"[$#][ad]?(\d+)", "$1");
                    text = text.Replace("$", "").Replace("#", "");
                    // |4(单数,复数) 取复数形式；{0} 之类由对局数据填的占位符显示为 X
                    text = System.Text.RegularExpressions.Regex.Replace(text, @"\|4\(([^,()]*),([^()]*)\)", "$2");
                    return System.Text.RegularExpressions.Regex.Replace(text, @"\{\d+\}", "X");
                }
                catch (Exception ex)
                {
                    MyLogger(BepInEx.Logging.LogLevel.Warning, $"UsedCards: card text for {def?.GetCardId()}: {ex.Message}");
                    return "";
                }
            }

            // 只要某个模式的某个赛季中达到最少对局数，就算“有用”；名单里没有的卡从没出现过，最高对局数为 0
            private static int GetMaxGames(UsedCardEntry entry, out string key)
            {
                key = null;
                int max = 0;
                if (entry?.games == null) return 0;
                foreach (var season in entry.games)
                {
                    if (key == null || season.Value > max)
                    {
                        max = season.Value;
                        key = season.Key;
                    }
                }
                return max;
            }

            public static bool IsUsed(UsedCardEntry entry, int minGames)
            {
                return entry?.games != null && entry.games.Count > 0 && GetMaxGames(entry, out _) >= minGames;
            }

            // 列出没用的卡里可以分解的行：
            //   “金卡”范围：同时有普通版和金卡的卡，只列金卡（普通版保留）
            //   “全部”范围：拥有的普通版和金卡各占一行，只要有一张就列出
            public static List<Candidate> GetCandidates(UsedCardsFile file, int minGames, bool allCards)
            {
                var result = new List<Candidate>();
                CollectionManager collectionManager = CollectionManager.Get();
                foreach (var record in collectionManager.GetOwnedCards())
                {
                    if (record == null || record.OwnedCount <= 0) continue;
                    if (record.PremiumType != TAG_PREMIUM.NORMAL && record.PremiumType != TAG_PREMIUM.GOLDEN) continue;
                    if (!allCards && record.PremiumType != TAG_PREMIUM.GOLDEN) continue;
                    if (!record.IsCraftable || record.IsHeroSkin || record.IsMercenaryAbility) continue;
                    // 游戏自己把经典模式（VAN_）的卡视为不可分解
                    if (GameUtils.IsClassicCard(record.CardId)) continue;
                    file.cards.TryGetValue(GameUtils.TranslateCardIdToDbId(record.CardId).ToString(), out UsedCardEntry entry);
                    if (IsUsed(entry, minGames)) continue;
                    int normalCount = collectionManager.GetNumCopiesInCollection(record.CardId, TAG_PREMIUM.NORMAL);
                    int goldenCount = collectionManager.GetNumCopiesInCollection(record.CardId, TAG_PREMIUM.GOLDEN);
                    if (!allCards && normalCount <= 0) continue;
                    // 不要在网页线程里调用 CraftingManager.Get()：对象不存在时会在当前线程创建、加载资源，导致游戏崩溃
                    int sellValue = CraftingManager.GetCardValue(record.CardId, record.PremiumType)?.GetSellValue() ?? 0;
                    if (sellValue <= 0) continue;
                    EntityDef def = record.GetEntityDef();
                    int maxGames = GetMaxGames(entry, out string gamesKey);
                    var decks = new List<KeyValuePair<string, int>>();
                    if (gamesKey != null && entry.decks != null && entry.decks.TryGetValue(gamesKey, out var seasonDecks) && seasonDecks != null)
                        decks = seasonDecks.OrderByDescending(x => x.Value).ToList();
                    result.Add(new Candidate
                    {
                        CardId = record.CardId,
                        Name = record.Name,
                        Premium = record.PremiumType,
                        Count = record.OwnedCount,
                        NormalCount = normalCount,
                        GoldenCount = goldenCount,
                        SellValue = sellValue,
                        KeepNormal = !allCards,
                        Rarity = record.Rarity,
                        Set = record.Set,
                        CardType = def?.GetCardType() ?? TAG_CARDTYPE.INVALID,
                        Cost = def?.GetCost() ?? 0,
                        Stats = GetStats(def),
                        Text = GetText(def),
                        MaxGames = maxGames,
                        GamesKey = gamesKey,
                        Decks = decks
                    });
                }
                return result;
            }
        }

        // /disenchant 页面自己的设置，存在 BepInEx/config/HsDisenchant.json，不放进 HsMod 的通用设置里
        public static class DisenchantSettings
        {
            public const string FileName = "HsDisenchant.json";

            public class Values
            {
                public int minGames = 0;
                public bool allCards = false;   // false：只看金卡（保留普通版）；true：所有没用的卡
            }

            private static readonly object settingsLock = new object();
            private static Values cached = null;

            public static string FilePath => Path.Combine(BepInEx.Paths.ConfigPath, FileName);

            public static Values Get()
            {
                lock (settingsLock)
                {
                    if (cached == null)
                    {
                        try
                        {
                            cached = File.Exists(FilePath) ? JsonConvert.DeserializeObject<Values>(File.ReadAllText(FilePath)) : null;
                        }
                        catch (Exception ex)
                        {
                            MyLogger(BepInEx.Logging.LogLevel.Warning, $"DisenchantSettings: {ex.Message}");
                        }
                        cached = cached ?? new Values();
                    }
                    return new Values { minGames = cached.minGames, allCards = cached.allCards };
                }
            }

            public static void Save(Values values)
            {
                lock (settingsLock)
                {
                    var clean = new Values { minGames = Math.Max(0, values.minGames), allCards = values.allCards };
                    File.WriteAllText(FilePath, JsonConvert.SerializeObject(clean, Formatting.Indented));
                    cached = clean;
                }
            }
        }

        // 分解没用的卡。网页提交的是用户在页面上确认过的那份清单（卡牌 ID + 版本 + 张数），
        // 真正的分解在主线程逐张进行：发出一张卡的请求后，等这张卡这个版本的服务器回复，再隔一段时间发下一张。
        public static class Disenchanter
        {
            private const float IntervalSeconds = 2f;
            private const float TimeoutSeconds = 15f;

            public class RequestedCard
            {
                public string id;
                public string premium;   // "NORMAL" 或 "GOLDEN"
                public int count;
            }

            private static readonly object stateLock = new object();
            private static List<RequestedCard> pendingRequest = null;
            private static readonly List<UsedCards.Candidate> queue = new List<UsedCards.Candidate>();   // 用 List 当队列：Queue<T> 在这里和 unstripped mscorlib 冲突
            private static bool waiting = false;
            private static string waitingCardId = null;
            private static TAG_PREMIUM waitingPremium = TAG_PREMIUM.NORMAL;
            private static string timedOutCardId = null;   // 超时后在收到这张卡的回复（或重启游戏）前不允许再分解
            private static TAG_PREMIUM timedOutPremium = TAG_PREMIUM.NORMAL;
            private static float waitStart = 0f;
            private static float nextSendTime = 0f;
            private static string lastSoldCardId = null;   // 同一张卡的普通版和金卡是两行：等收藏数量更新后再发第二行
            private static TAG_PREMIUM lastSoldPremium = TAG_PREMIUM.NORMAL;
            private static float lastSoldTime = 0f;
            private const float CollectionSyncSeconds = 10f;
            private static bool handlerRegistered = false;
            private static readonly List<string> results = new List<string>();

            public static bool IsRunning
            {
                get { lock (stateLock) return pendingRequest != null || queue.Count > 0 || waiting; }
            }

            public static string BlockedReason
            {
                get { lock (stateLock) return timedOutCardId == null ? null : $"{timedOutCardId}（{timedOutPremium}）的分解请求超时，还没收到服务器回复。为免记错结果，重启游戏前不能再分解。"; }
            }

            public static List<string> GetResults()
            {
                lock (stateLock) return new List<string>(results);
            }

            public static int Remaining
            {
                get { lock (stateLock) return queue.Count + (waiting ? 1 : 0) + (pendingRequest?.Count ?? 0); }
            }

            // 网页线程调用
            public static bool Request(List<RequestedCard> cards, out string message)
            {
                lock (stateLock)
                {
                    if (timedOutCardId != null)
                    {
                        message = BlockedReason;
                        return false;
                    }
                    if (pendingRequest != null || queue.Count > 0 || waiting)
                    {
                        message = "已经在分解中，请等当前任务结束。";
                        return false;
                    }
                    pendingRequest = cards;
                    results.Clear();
                }
                message = $"已提交：{cards.Count} 行。刷新页面查看进度。";
                MyLogger(BepInEx.Logging.LogLevel.Warning, $"Disenchant: {message}");
                return true;
            }

            private static void RegisterHandler()
            {
                if (handlerRegistered) return;
                Network.Get().RegisterNetHandler(PegasusUtil.BoughtSoldCard.PacketID.ID, new Network.NetHandler(OnCardSaleResult), null);
                handlerRegistered = true;
            }

            // 主线程每帧调用（Main.Update）
            public static void Update()
            {
                try
                {
                    float now = UnityEngine.Time.realtimeSinceStartup;
                    lock (stateLock)
                    {
                        if (pendingRequest != null) StartQueue();
                        if (waiting)
                        {
                            if (now - waitStart > TimeoutSeconds)
                            {
                                timedOutCardId = waitingCardId;
                                timedOutPremium = waitingPremium;
                                AddResult($"超时：{waitingCardId}（{waitingPremium}）没有收到服务器回复，已停止剩余 {queue.Count} 行。重启游戏前不能再分解。");
                                queue.Clear();
                                waiting = false;
                            }
                            return;
                        }
                        if (queue.Count == 0 || now < nextSendTime) return;
                        if (SceneMgr.Get()?.GetMode() != SceneMgr.Mode.COLLECTIONMANAGER)
                        {
                            AddResult($"已离开收藏界面，停止剩余 {queue.Count} 行。");
                            queue.Clear();
                            return;
                        }
                        var next = queue[0];
                        // 刚卖掉这张卡的另一个版本：收藏里的数量要等服务器另发通知才更新，
                        // 否则这一行会带着旧数量发出去被服务器拒绝。最多等 CollectionSyncSeconds 秒
                        if (next.CardId == lastSoldCardId && now - lastSoldTime < CollectionSyncSeconds
                            && CollectionManager.Get().GetNumCopiesInCollection(lastSoldCardId, lastSoldPremium) > 0)
                            return;
                        queue.RemoveAt(0);
                        SendNext(next, now);
                        if (!waiting && queue.Count == 0) AddResult("结束。");
                    }
                }
                catch (Exception ex)
                {
                    MyLogger(BepInEx.Logging.LogLevel.Error, ex);
                    lock (stateLock)
                    {
                        AddResult($"出错，已停止：{ex.Message}");
                        queue.Clear();
                        pendingRequest = null;
                        if (waiting)
                        {
                            timedOutCardId = waitingCardId;
                            timedOutPremium = waitingPremium;
                        }
                        waiting = false;
                    }
                }
            }

            // 只分解用户确认过的行：每行都必须仍在当前清单里（按当前设置），且张数和页面上一致
            private static void StartQueue()
            {
                List<RequestedCard> requested = pendingRequest;
                pendingRequest = null;
                if (SceneMgr.Get()?.GetMode() != SceneMgr.Mode.COLLECTIONMANAGER)
                {
                    AddResult("请先进入收藏界面。");
                    return;
                }
                var file = UsedCards.Load();
                if (file == null)
                {
                    AddResult($"找不到 {UsedCards.FilePath}。");
                    return;
                }
                var settings = DisenchantSettings.Get();
                var current = UsedCards.GetCandidates(file, settings.minGames, settings.allCards)
                    .GroupBy(x => $"{x.CardId}|{x.Premium}").ToDictionary(x => x.Key, x => x.First());
                foreach (var card in requested)
                {
                    if (card?.id != null && Enum.TryParse(card.premium, out TAG_PREMIUM premium)
                        && current.TryGetValue($"{card.id}|{premium}", out var candidate) && candidate.Count == card.count)
                        queue.Add(candidate);
                    else
                        AddResult($"跳过 {card?.id}（{card?.premium}）：已不在当前清单里，或张数和页面上不一致。");
                }
                RegisterHandler();
                AddResult($"开始：{queue.Count} 行。");
                if (queue.Count == 0) AddResult("结束。");
            }

            private static void SendNext(UsedCards.Candidate card, float now)
            {
                // 发之前按当前收藏重新核对，数量或价格变了就跳过
                CollectionManager collectionManager = CollectionManager.Get();
                int normal = collectionManager.GetNumCopiesInCollection(card.CardId, TAG_PREMIUM.NORMAL);
                int golden = collectionManager.GetNumCopiesInCollection(card.CardId, TAG_PREMIUM.GOLDEN);
                int signature = collectionManager.GetNumCopiesInCollection(card.CardId, TAG_PREMIUM.SIGNATURE);
                int diamond = collectionManager.GetNumCopiesInCollection(card.CardId, TAG_PREMIUM.DIAMOND);
                int owned = card.Premium == TAG_PREMIUM.GOLDEN ? golden : normal;
                NetCache.CardValue value = CraftingManager.GetCardValue(card.CardId, card.Premium);
                if (owned != card.Count || (card.KeepNormal && normal <= 0) || value == null || value.GetSellValue() <= 0)
                {
                    AddResult($"跳过 {card.Name}（{card.Premium}）：现有 {owned} 张（确认时 {card.Count}），普通 {normal}，分解价 {value?.GetSellValue()}。");
                    return;
                }
                CraftingPendingTransaction transaction = new CraftingPendingTransaction
                {
                    CardID = card.CardId,
                    Premium = card.Premium,
                    CardValueOverridden = value.IsOverrideActive(),
                    NormalDisenchantCount = card.Premium == TAG_PREMIUM.NORMAL ? owned : 0,
                    GoldenDisenchantCount = card.Premium == TAG_PREMIUM.GOLDEN ? owned : 0
                };
                int expectedDustDelta = -(owned * value.GetSellValue());
                Network.Get().CraftingTransaction(transaction, expectedDustDelta, normal, golden, signature, diamond);
                waiting = true;
                waitingCardId = card.CardId;
                waitingPremium = card.Premium;
                waitStart = now;
                MyLogger(BepInEx.Logging.LogLevel.Warning, $"Disenchant: {card.CardId} {card.Name} {card.Premium} {owned} 张，预计 {-expectedDustDelta} 尘（分解前 普通 {normal}，金卡 {golden}）");
            }

            // 只认我们正在等的那张卡、那个版本的回复，其他（手动合成/分解、一键全额分解）一律忽略
            private static void OnCardSaleResult()
            {
                Network.CardSaleResult result = Network.Get().GetCardSaleResult();
                lock (stateLock)
                {
                    if (result == null)
                    {
                        if (!waiting) return;
                        waiting = false;
                        timedOutCardId = waitingCardId;
                        timedOutPremium = waitingPremium;
                        AddResult($"失败：{waitingCardId} 收到空回复，已停止剩余 {queue.Count} 行。重启游戏前不能再分解。");
                        queue.Clear();
                        return;
                    }
                    if (!waiting && timedOutCardId != null && result.AssetName == timedOutCardId && result.Premium == timedOutPremium)
                    {
                        AddResult($"迟到的回复：{timedOutCardId} => {result.Action}。现在可以再次分解。");
                        timedOutCardId = null;
                        return;
                    }
                    if (!waiting || result.AssetName != waitingCardId || result.Premium != waitingPremium) return;
                    waiting = false;
                    nextSendTime = UnityEngine.Time.realtimeSinceStartup + IntervalSeconds;
                    if (result.Action == Network.CardSaleResult.SaleResult.CARD_WAS_SOLD)
                    {
                        lastSoldCardId = waitingCardId;
                        lastSoldPremium = waitingPremium;
                        lastSoldTime = UnityEngine.Time.realtimeSinceStartup;
                        AddResult($"成功：{waitingCardId}（{waitingPremium}）");
                    }
                    else
                    {
                        AddResult($"失败：{waitingCardId}（{waitingPremium}）=> {result.Action}，已停止剩余 {queue.Count} 行。");
                        queue.Clear();
                    }
                    if (queue.Count == 0) AddResult("结束。");
                }
            }

            private static void AddResult(string text)
            {
                results.Add($"{DateTime.Now:HH:mm:ss} {text}");
                MyLogger(BepInEx.Logging.LogLevel.Warning, $"Disenchant: {text}");
            }
        }
    }
}
