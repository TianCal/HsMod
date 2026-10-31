using Assets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static HsMod.PluginConfig;

namespace HsMod
{
    public class WebPage
    {
        public const string Viewport = "<meta name=\"viewport\" content=\"width=device-width, user-scalable=no, initial-scale=1.0, maximum-scale=1.0, minimum-scale=1.0\">";
        private static string GenerateBtn()
        {
            string btn = @" <a href=""/info""><button class=""btn_li"">主要信息</button><br/></a><br />";
            btn += @"<a href=""/pack""><button class=""btn_li"">卡包信息</button><br/></a><br />";
            btn += @"<a href=""/collection""><button class=""btn_li"">卡牌收藏</button><br /></a><br />";
            btn += @"<a href=""/disenchant""><button class=""btn_li"">卡牌分解</button><br /></a><br />";
            btn += @"<a href=""/skins""><button class=""btn_li"">皮肤信息</button><br /></a><br />";
            btn += @"<a href=""/lettuce""><button class=""btn_li"">佣兵关卡</button><br /></a><br />";
            btn += @"<a href=""/mercenaries""><button class=""btn_li"">佣兵收藏</button><br /></a><br />";
            if (System.IO.File.Exists(CommandConfig.hsMatchLogPath)) btn += @"<a href=""/matchlog""><button class=""btn_li"">炉石对局</button><br /></a><br />";
            string configUrl = File.Exists(Path.Combine(PluginConfig.HsModWebSite, "config", "index.html")) ? "/config/index.html" : "/config";
            btn += $@"<a href=""{configUrl}""><button class=""btn_li"">{LocalizationManager.GetLangValue("config.page.button")}</button><br /></a><br />";
            btn += @"<a href=""/about""><button class=""btn_li"">关&emsp;&emsp;于</button><br /></a><br />";
            return btn;
        }

        private static string GenerateNav(string title)
        {
            string nav = string.Empty;
            if (title != "index")
            {
                nav = "<center><ul class=\"nav_ui\">";
                var btns = GenerateBtn().Replace("<br/>", "").Split("<br />");
                foreach (string btn in btns)
                {
                    nav += $@"<li class=""nav_li"">{btn}</li>";
                }
                nav += "</ul></center><br />";
            }

            return nav;
        }

        public static StringBuilder Template(string title = "", string body = "", bool useViewport = true)
        {
            StringBuilder builder = new StringBuilder();

            string templateContent = FileManager.ReadEmbeddedFile("./WebResources/HsMod.template.html");
            string nav = GenerateNav(title);

            templateContent = templateContent
                .Replace("{PluginInfo.PLUGIN_GUID}", PluginInfo.PLUGIN_GUID)
                .Replace("{useViewport}", useViewport ? Viewport : "")
                .Replace("{title}", title)
                .Replace("{nav}", nav)
                .Replace("{body}", body)
                .Replace("{webPageBackImg}", webPageBackImg.Value);
            builder.Append(templateContent);

            return builder;
        }

        public static StringBuilder Template(StringBuilder body, string title = "", bool useViewport = true)
        {
            StringBuilder builder = new StringBuilder();

            string templateContent = FileManager.ReadEmbeddedFile("./WebResources/HsMod.template.html");

            string nav = GenerateNav(title);

            templateContent = templateContent
                .Replace("{PluginInfo.PLUGIN_GUID}", PluginInfo.PLUGIN_GUID)
                .Replace("{useViewport}", useViewport ? Viewport : "")
                .Replace("{title}", title)
                .Replace("{nav}", nav)
                .Replace("{body}", body.ToString())
                .Replace("{webPageBackImg}", webPageBackImg.Value);
            builder.Append(templateContent);
            return builder;
        }

        public static StringBuilder HomePage()
        {
            var btn = GenerateBtn();
            string body = @"<h1 style=""text-align: center; opacity: 0.6;"">HsMod</h1>";
            body += $@"<div style=""text-align: center; width: auto; position: absolute; top: 50%; left: 50%; transform: translate(-50%,-50%);"">{btn}</div>";
            return Template("index", body);
        }


        public static StringBuilder AboutPage()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(@"<h3 style=""text-align: center;"">关于HsMod</h3>");
            builder.AppendLine($"<p>Author: <a href='https://github.com/Pik-4'>Pik_4</a><br />Page Last Updated: 2024.11.05<br />HsMod Version:{PluginInfo.PLUGIN_VERSION}</p><br />");
            builder.AppendLine(FileManager.ReadEmbeddedFile($"./WebResources/about.{pluginInitLanague.Value}.html"));
            return Template(builder, "About");
        }

        public static StringBuilder ShellPage()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(@"<h3 style=""text-align: center;"">WebShell</h3>");
            builder.AppendLine(FileManager.ReadEmbeddedFile("./WebResources/shell.html"));
            return Template(builder, "WebShell");
        }

        public static StringBuilder InfoPage()
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(@"<h3 style=""text-align: center;"">进程信息</h3>");
            builder.Append("PID: ");
            builder.Append(System.Diagnostics.Process.GetCurrentProcess()?.Id.ToString());
            builder.Append($" ({System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture})");
            builder.Append("<br />");
            builder.Append("<hr />");
            builder.Append(@"<h3 style=""text-align: center;"">基本信息</h3>");
            NetCache netCache = NetCache.Get();
            try
            {
                builder.Append("账号：");
                builder.Append(BnetPresenceMgr.Get()?.GetMyPlayer()?.GetBattleTag()?.ToString());
                builder.Append("<br />");
                CurrencyManager currencyManager;
                if (Blizzard.T5.Services.ServiceManager.TryGet<CurrencyManager>(out currencyManager))
                {
                    currencyManager?.RefreshWallet();
                    builder.Append("金币：");
                    builder.Append(currencyManager?.GetBalance(CurrencyType.GOLD).ToString());
                    builder.Append("<br />");
                }
                builder.Append("奥数之尘：");
                builder.Append(netCache?.GetArcaneDustBalance().ToString());
                builder.Append("<br />");
                builder.Append("竞技场门票：");
                builder.Append(netCache?.GetArenaTicketBalance().ToString());
                builder.Append("<br />");
            }
            catch (Exception ex)
            {
                builder.Append($@"基本信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p><br />");
            }
            finally
            {
                builder.Append("<hr />");
            }
            builder.Append(@"<h3 style=""text-align: center;"">战令信息</h3>");
            try
            {
                Hearthstone.DataModels.RewardTrackDataModel trackDataModel = Hearthstone.Progression.RewardTrackManager.Get().GetRewardTrack(Global.RewardTrackType.GLOBAL).TrackDataModel;
                builder.Append($"炉石：{trackDataModel.Level}&emsp;&emsp;");
                builder.Append("进度：" + ((trackDataModel.Level == trackDataModel.LevelHardCap && trackDataModel.Xp == 0) ? "已满级！" : trackDataModel.XpProgress) + "<br />");
                trackDataModel = Hearthstone.Progression.RewardTrackManager.Get().GetRewardTrack(Global.RewardTrackType.BATTLEGROUNDS).TrackDataModel;
                builder.Append($"酒馆：{trackDataModel.Level}&emsp;&emsp;");
                builder.Append("进度：" + ((trackDataModel.Level == trackDataModel.LevelHardCap && trackDataModel.Xp == 0) ? "已满级！" : trackDataModel.XpProgress) + "<br />");
            }
            catch (Exception ex)
            {
                builder.Append($@"战令信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p><br />");
            }
            finally
            {
                builder.Append("<hr />");
            }
            builder.Append(@"<h3 style=""text-align: center;"">天梯信息</h3>");
            try
            {
                MedalInfoTranslator localPlayerMedalInfo = RankMgr.Get().GetLocalPlayerMedalInfo();
                TranslatedMedalInfo currentMedal = null;
                string rankMode = "";
                for (int i = 1; i <= 4; i++)
                {
                    switch (i)
                    {
                        case 1:
                            currentMedal = localPlayerMedalInfo.GetCurrentMedal(PegasusShared.FormatType.FT_CLASSIC);
                            rankMode = "经典";
                            break;
                        case 2:
                            currentMedal = localPlayerMedalInfo.GetCurrentMedal(PegasusShared.FormatType.FT_STANDARD);
                            rankMode = "标准";
                            break;
                        case 3:
                            currentMedal = localPlayerMedalInfo.GetCurrentMedal(PegasusShared.FormatType.FT_WILD);
                            rankMode = "狂野";
                            break;
                        case 4:
                            currentMedal = localPlayerMedalInfo.GetCurrentMedal(PegasusShared.FormatType.FT_TWIST);
                            rankMode = "幻变";
                            break;
                    }
                    string rankName = Utils.RankIdxToString(currentMedal.starLevel);
                    string detail = (rankName == "传说") ? currentMedal.legendIndex.ToString() + " 名" : currentMedal.earnedStars.ToString() + " 星";
                    builder.Append($@"{rankMode}：{rankName}&emsp;{detail}&emsp;&emsp;");
                    builder.Append($@"赛季场次：{currentMedal.seasonWins}胜 - {currentMedal.seasonGames}场");
                    builder.Append($@"（{string.Format("{0:P1}", (float)currentMedal.seasonWins / (float)currentMedal.seasonGames)}）<br />");
                }
                NetCache.NetCacheMercenariesPlayerInfo mercenariesPlayerInfo = NetCache.Get()?.GetNetObject<NetCache.NetCacheMercenariesPlayerInfo>();
                builder.Append($@"佣兵：{mercenariesPlayerInfo.PvpRating}（当前）- {mercenariesPlayerInfo.PvpSeasonHighestRating}（最高）&emsp;&emsp;");
                builder.Append($@"宝箱进度：{mercenariesPlayerInfo.PvpRewardChestWinsProgress}/{mercenariesPlayerInfo.PvpRewardChestWinsRequired} <br />");
            }
            catch (Exception ex)
            {
                builder.Append($@"天梯信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p><br />");

            }
            finally
            {
                builder.Append("<hr />");
            }
            builder.Append(@"<h3 style=""text-align: center;"">任务信息</h3>");
            try
            {
                Hearthstone.DataModels.QuestListDataModel dailyQuestListDataModel = new Hearthstone.DataModels.QuestListDataModel();
                Hearthstone.DataModels.QuestListDataModel weeklyQuestListDataModel = new Hearthstone.DataModels.QuestListDataModel();
                Hearthstone.DataModels.QuestListDataModel specialQuestListDataModel = new Hearthstone.DataModels.QuestListDataModel();
                Hearthstone.DataModels.QuestListDataModel battlegroundsQuestListDataModel = new Hearthstone.DataModels.QuestListDataModel();

                foreach (Hearthstone.DataModels.QuestDataModel item in Hearthstone.Progression.QuestManager.Get().CreateActiveQuestsDataModel(Assets.QuestPool.QuestPoolType.DAILY, QuestPool.RewardTrackType.GLOBAL, true).Quests)
                {
                    if (item == null || dailyQuestListDataModel.Quests.Count > 4)
                    {
                        break;
                    }
                    dailyQuestListDataModel.Quests.Add(item);
                }
                builder.Append(@"<h4>日常任务</h4>");
                foreach (Hearthstone.DataModels.QuestDataModel item in dailyQuestListDataModel.Quests)
                {
                    if (item != null)
                    {
                        if (item?.QuestId > 0)
                        {
                            builder.Append("<li>");
                            builder.Append($@"{item?.Status} {item?.Name}：{item?.Description}<br />进度：{item?.ProgressMessage}<br />");
                            builder.Append($@"经验奖励：{item?.RewardTrackXp}");
                            builder.Append((item?.RerollCount > 0) ? "（可刷新）" : "");
                            builder.Append("</li><br />");
                        }
                        else
                        {
                            builder.Append("<li>");
                            builder.Append($@"{item?.TimeUntilNextQuest}");
                            builder.Append("</li>");
                            break;
                        }
                    }
                }

                foreach (Hearthstone.DataModels.QuestDataModel item in Hearthstone.Progression.QuestManager.Get().CreateActiveQuestsDataModel(Assets.QuestPool.QuestPoolType.WEEKLY, QuestPool.RewardTrackType.GLOBAL, true).Quests)
                {
                    if (item == null || weeklyQuestListDataModel.Quests.Count > 4)
                    {
                        break;
                    }
                    weeklyQuestListDataModel.Quests.Add(item);
                }
                builder.Append(@"<h4>每周任务</h4>");
                foreach (Hearthstone.DataModels.QuestDataModel item in weeklyQuestListDataModel.Quests)
                {
                    if (item != null)
                    {
                        if (item?.QuestId > 0)
                        {
                            builder.Append("<li>");
                            builder.Append($@"{item?.Status} {item?.Name}：{item?.Description}<br />进度：{item?.ProgressMessage}<br />");
                            builder.Append($@"经验奖励：{item?.RewardTrackXp}");
                            builder.Append((item?.RerollCount > 0) ? "（可刷新）" : "");
                            builder.Append("</li><br />");
                        }
                        else
                        {
                            builder.Append("<li>");
                            builder.Append($@"{item?.TimeUntilNextQuest}");
                            builder.Append("</li>");
                            break;
                        }
                    }
                }

                foreach (var questsType in (QuestPool.RewardTrackType[])Enum.GetValues(typeof(QuestPool.RewardTrackType)))
                {
                    foreach (Hearthstone.DataModels.QuestDataModel item in Hearthstone.Progression.QuestManager.Get().CreateActiveQuestsDataModel(Assets.QuestPool.QuestPoolType.NONE, questsType, true)?.Quests)
                    {
                        if (item == null)
                        {
                            continue;
                        }
                        specialQuestListDataModel.Quests.Add(item);
                    }
                    foreach (Hearthstone.DataModels.QuestDataModel item in Hearthstone.Progression.QuestManager.Get().CreateActiveQuestsDataModel(Assets.QuestPool.QuestPoolType.EVENT, questsType, true)?.Quests)
                    {
                        if (item == null)
                        {
                            continue;
                        }
                        specialQuestListDataModel.Quests.Add(item);
                    }
                }

                if (specialQuestListDataModel.Quests.Count >= 1 && specialQuestListDataModel.Quests[0].QuestId > 0)
                {
                    builder.Append(@"<h4>活动任务</h4>");
                    foreach (Hearthstone.DataModels.QuestDataModel item in specialQuestListDataModel.Quests.ToList().Where((x, i) => specialQuestListDataModel.Quests.ToList().FindIndex(z => z.QuestId == x.QuestId) == i).ToList())
                    {
                        if (item != null)
                        {
                            if (item?.QuestId > 0)
                            {
                                builder.Append("<li>");
                                builder.Append($@"{item.PoolType} {item?.Status} {item?.Name}：{item?.Description}<br />");
                                builder.Append($@"奖励：{item?.Rewards?.Description}<br />");
                                builder.Append($@"经验：{item?.RewardTrackXp}<br />进度：{item?.ProgressMessage}<br />");
                                if (item.NextInChain != 0)
                                {
                                    int nextQuestID = item.NextInChain;
                                    builder.Append("任务链：<br />");
                                    while (nextQuestID != 0)
                                    {
                                        var nextQuest = GameDbf.Quest.GetRecord(nextQuestID);
                                        if (nextQuest == null) break;
                                        builder.Append("<li>");
                                        builder.Append($@"{nextQuestID} {nextQuest?.Name?.GetString()}：{nextQuest?.Description?.GetString()}<br />");
                                        builder.Append("</li>");
                                        nextQuestID = nextQuest.NextInChain;
                                    }
                                }
                                builder.Append($@"距离活动结束还剩：" + (!String.IsNullOrEmpty(item.TimeUntilExpiration) ? item.TimeUntilExpiration.ToString() : "未知"));
                                builder.Append(" ");
                                builder.Append(item.Abandonable ? "（可放弃）" : "");
                                builder.Append((item?.RerollCount > 0) ? "(可刷新）" : "");
                                builder.Append("</li><br />");
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }

                foreach (Hearthstone.DataModels.QuestDataModel item in Hearthstone.Progression.QuestManager.Get().CreateActiveQuestsDataModel(Assets.QuestPool.QuestPoolType.WEEKLY, QuestPool.RewardTrackType.BATTLEGROUNDS, true).Quests)
                {
                    if (item == null)
                    {
                        break;
                    }
                    battlegroundsQuestListDataModel.Quests.Add(item);
                }
                builder.Append(@"<h4>每周酒馆</h4>");
                foreach (Hearthstone.DataModels.QuestDataModel item in battlegroundsQuestListDataModel.Quests)
                {
                    if (item != null)
                    {
                        if (item?.QuestId > 0)
                        {
                            builder.Append("<li>");
                            builder.Append($@"{item?.Status} {item?.Name}：{item?.Description}<br />进度：{item?.ProgressMessage}<br />");
                            builder.Append($@"经验奖励：{item?.RewardTrackXp}");
                            builder.Append((item?.RerollCount > 0) ? "（可刷新）" : "");
                            builder.Append("</li><br />");
                        }
                        else
                        {
                            builder.Append("<li>");
                            builder.Append($@"{item?.TimeUntilNextQuest}");
                            builder.Append("</li>");
                            break;
                        }
                    }
                }
                builder.Append(@"<h4>佣兵任务</h4>");
                foreach (PegasusLettuce.MercenariesVisitorState mercenariesVisitorState in NetCache.Get().GetNetObject<NetCache.NetCacheMercenariesVillageVisitorInfo>().VisitorStates
                                                                                           .OrderByDescending(x => LettuceVillageDataUtil.CreateTaskModelFromTaskState(x.ActiveTaskState, null).TaskType)
                                                                                           .ThenBy(x => LettuceVillageDataUtil.CreateTaskModelFromTaskState(x.ActiveTaskState, null).MercenaryId)
                                                                                           .ToList())
                {
                    Hearthstone.DataModels.MercenaryVillageTaskItemDataModel mercenaryVillageTaskItemDataModel = LettuceVillageDataUtil.CreateTaskModelFromTaskState(mercenariesVisitorState.ActiveTaskState, null);
                    builder.Append("<li>");
                    builder.Append($"[{mercenaryVillageTaskItemDataModel.TaskType}] [{mercenaryVillageTaskItemDataModel.MercenaryName}]&emsp;");
                    if (mercenaryVillageTaskItemDataModel.TaskType == Assets.MercenaryVisitor.VillageVisitorType.STANDARD)
                    {
                        builder.Append($@"任务{mercenaryVillageTaskItemDataModel.TaskChainIndex + 1} - ");
                    }
                    builder.Append($"{mercenaryVillageTaskItemDataModel.Title}<br />{mercenaryVillageTaskItemDataModel.Description}<br />");
                    builder.Append($"任务奖励：{mercenaryVillageTaskItemDataModel.RewardList.Description}<br />");
                    builder.Append($"任务进度：{mercenaryVillageTaskItemDataModel.ProgressMessage}");
                    if (mercenaryVillageTaskItemDataModel.IsTimedEvent)
                        builder.Append($"<br />剩余时间：{mercenaryVillageTaskItemDataModel.RemainingEventTime}<br />");
                    builder.Append("</li><br />");
                }


            }
            catch (Exception ex)
            {
                builder.Append($@"任务信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p><br />");
            }
            finally
            {
                builder.Append("<hr />");
            }

            return Template(builder, "Info");
        }

        public static StringBuilder CollectionPage()
        {
            string body = "";
            body += @"<h3 style=""text-align: center;"">可分解卡牌信息</h3>";
            Utils.CardCount cards = new Utils.CardCount();
            List<Utils.CollectionCard> collectionCards = new List<Utils.CollectionCard>();
            try
            {
                if (SceneMgr.Get().GetMode() != SceneMgr.Mode.COLLECTIONMANAGER)
                {
                    throw new Exception();
                }
                string temp = @"<table border=0 style=""text-align: center;""><tr><th>卡牌名称</th><th>卡牌质量</th><th>卡牌数量</th></tr>";
                foreach (var record in CollectionManager.Get()?.GetOwnedCards())
                {
                    if (record != null
                        && record.IsCraftable
                        && (!record.IsHeroSkin)
                        && (!record.IsMercenaryAbility)
                        && (record.Set != TAG_CARD_SET.CORE)
                        && (record.OwnedCount > 0))
                    {
                        collectionCards.Add(new Utils.CollectionCard { Name = record.Name, Rarity = record.Rarity, Premium = record.PremiumType, Count = record.OwnedCount });
                    }

                }
                foreach (var card in collectionCards.Distinct().ToList())
                {
                    temp += "<tr>";
                    temp += $"<td>{card.Name}</td>";
                    temp += $@"{Utils.CardsCount(card.Rarity, card.Premium, card.Count, ref cards)}";
                    temp += "</tr>";
                }
                temp += "</table>";

                body += $"<li>全部卡牌数量：{cards.total}，其中金卡数量：{cards.gTotal}</li>";
                body += $"<li>普通卡牌数量：{cards.common + cards.gCommon}，其中金卡数量：{cards.gCommon}</li>";
                body += $"<li>稀有卡牌数量：{cards.rare + cards.gRare}，其中金卡数量：{cards.gRare}</li>";
                body += $"<li>史诗卡牌数量：{cards.epic + cards.gEpic}，其中金卡数量：{cards.gEpic}</li>";
                body += $"<li>传说卡牌数量：{cards.legendary + cards.gLegendary}，其中金卡数量：{cards.gLegendary}</li>";
                body += $"<li>能分解的奥术之尘：{cards.totalDust}</li>";
                body += "<br /><hr />";
                //body += temp;
            }
            catch (Exception ex)
            {
                body += $@"卡牌信息获取异常，请重新进入收藏模式。<br /><p style=""white-space: pre-line;"">{ex}</p><br />";
            }
            finally
            {
                //body += "<hr />";
            }
            return Template("Collection", body);
        }

        // 列出没用的卡里可以分解的卡；分解只在勾选、点按钮并确认后由 Utils.Disenchanter 执行
        public static StringBuilder DisenchantPage()
        {
            string body = @"<h3 style=""text-align: center;"">分解没用的卡</h3>";
            try
            {
                var settings = Utils.DisenchantSettings.Get();
                // 页面自己的设置：最少对局数、范围。保存走带校验的 POST，保存后刷新页面
                body += $@"<p>最少对局数 <input type=""number"" id=""hsMinGames"" min=""0"" value=""{settings.minGames}"" style=""width: 7em;"" />
范围 <select id=""hsScope"">
<option value=""golden""{(settings.allCards ? "" : " selected")}>金卡（只列同时有普通版的，普通版保留）</option>
<option value=""all""{(settings.allCards ? " selected" : "")}>所有没用的卡（普通版、金卡分开列）</option>
</select>
<button onclick=""hsSaveSettings()"">保存</button></p>
<script>
function hsSaveSettings() {{
  var body = {{ minGames: Math.max(0, parseInt(document.getElementById('hsMinGames').value || '0', 10) || 0), allCards: document.getElementById('hsScope').value === 'all' }};
  fetch('/disenchant/settings', {{ method: 'POST', headers: {{ 'Content-Type': 'application/json', 'X-HsMod-Disenchant': 'confirm' }}, body: JSON.stringify(body) }})
    .then(r => r.text()).then(t => {{ if (t.trim() !== 'ok') alert(t); location.reload(); }})
    .catch(e => alert('保存失败：' + e));
}}
</script>";

                if (SceneMgr.Get().GetMode() != SceneMgr.Mode.COLLECTIONMANAGER)
                {
                    throw new Exception("请先进入收藏模式。");
                }
                var file = Utils.UsedCards.Load();
                if (file == null)
                {
                    throw new Exception($"找不到 {Utils.UsedCards.FilePath}，请先运行 tools/used_cards.py。");
                }
                int minGames = settings.minGames;
                var candidates = Utils.UsedCards.GetCandidates(file, minGames, settings.allCards)
                    .OrderByDescending(x => x.SellValue * x.Count).ThenBy(x => x.Name).ThenBy(x => x.Premium).ToList();

                int totalCopies = candidates.Sum(x => x.Count);
                int totalDust = candidates.Sum(x => x.SellValue * x.Count);
                int usedCount = file.cards.Values.Count(x => Utils.UsedCards.IsUsed(x, minGames));
                string lastRun = file.runs.Count > 0 && file.runs.Last().TryGetValue("date", out object date) ? date?.ToString() : "?";
                body += $"<li>有用卡名单：{usedCount} 个卡牌版本（最少对局数 {minGames}，名单更新于 {lastRun}，共 {file.runs.Count} 次）</li>";
                body += settings.allCards
                    ? $"<li>没用的卡：{candidates.Count} 行（普通版、金卡分开），共 {totalCopies} 张</li>"
                    : $"<li>没用、且同时有普通版和金卡的卡：{candidates.Count} 种，金卡 {totalCopies} 张（普通版保留）</li>";
                body += $"<li>全部分解可得奥术之尘：{totalDust}</li>";
                if (minGames > 0)
                    body += $"<li>“出现过的卡组”只列出出现过但不到 {minGames} 局的卡：取对局数最多的那个模式和赛季里，用到这张卡最多的卡组类型。</li>";
                body += "<br />";

                // 分解：勾选、确认，再由游戏主线程逐行执行；刷新页面查看进度
                string blocked = Utils.Disenchanter.BlockedReason;
                bool running = Utils.Disenchanter.IsRunning;
                bool canSelect = !running && blocked == null && candidates.Count > 0;
                if (running)
                    body += $"<p><b>正在分解，剩余 {Utils.Disenchanter.Remaining} 行。刷新页面查看进度。</b></p>";
                else if (blocked != null)
                    body += $"<p><b>{System.Net.WebUtility.HtmlEncode(blocked)}</b></p>";
                else if (canSelect)
                {
                    // 提交的是页面上勾选的那几行（卡牌 ID + 版本 + 张数），服务器只分解其中仍在清单里、且张数没变的行
                    var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings { StringEscapeHandling = Newtonsoft.Json.StringEscapeHandling.EscapeHtml };
                    string cardsJson = Newtonsoft.Json.JsonConvert.SerializeObject(candidates.Select(x => new
                    {
                        id = x.CardId,
                        premium = x.Premium.ToString(),
                        count = x.Count,
                        name = x.Name + (x.Premium == TAG_PREMIUM.GOLDEN ? "（金）" : ""),
                        dust = x.SellValue * x.Count
                    }), jsonSettings);
                    string keep = settings.allCards ? "" : "普通版保留，";
                    body += $@"<p><span id=""hsSummary"">已选 0 行</span>
<button id=""hsGo"" onclick=""hsDisenchant()"" disabled>分解所选</button></p>
<script>
var hsCards = {cardsJson};
function hsSelected() {{
  return Array.from(document.querySelectorAll('.hsPick:checked')).map(x => hsCards[+x.value]);
}}
function hsUpdate() {{
  var sel = hsSelected();
  var copies = sel.reduce((a, c) => a + c.count, 0), dust = sel.reduce((a, c) => a + c.dust, 0);
  document.getElementById('hsSummary').textContent = '已选 ' + sel.length + ' 行、' + copies + ' 张、' + dust + ' 尘';
  document.getElementById('hsGo').disabled = sel.length === 0;
  var all = document.getElementById('hsAll');
  if (all) all.checked = sel.length === hsCards.length;
}}
function hsToggleAll(on) {{
  document.querySelectorAll('.hsPick').forEach(x => x.checked = on);
  hsUpdate();
}}
function hsDisenchant() {{
  var sel = hsSelected();
  if (sel.length === 0) return;
  var copies = sel.reduce((a, c) => a + c.count, 0), dust = sel.reduce((a, c) => a + c.dust, 0);
  var names = sel.slice(0, 20).map(c => c.name + ' ×' + c.count).join('\n') + (sel.length > 20 ? '\n……等 ' + sel.length + ' 行' : '');
  if (!confirm('确定分解以下 ' + sel.length + ' 行、共 ' + copies + ' 张（' + dust + ' 尘）吗？{keep}分解不能撤销。\n\n' + names)) return;
  fetch('/disenchant', {{ method: 'POST', headers: {{ 'Content-Type': 'application/json', 'X-HsMod-Disenchant': 'confirm' }}, body: JSON.stringify(sel.map(c => ({{ id: c.id, premium: c.premium, count: c.count }}))) }})
    .then(r => r.text()).then(t => {{ alert(t); location.reload(); }})
    .catch(e => alert('提交失败：' + e));
}}
// 浏览器刷新后可能按位置恢复勾选，而行已经变了：载入时一律清空
window.addEventListener('DOMContentLoaded', () => hsToggleAll(false));
</script>";
                }
                var results = Utils.Disenchanter.GetResults();
                if (results.Count > 0)
                {
                    body += "<p>本次分解记录：</p><ul>";
                    foreach (string line in results) body += $"<li>{System.Net.WebUtility.HtmlEncode(line)}</li>";
                    body += "</ul>";
                }
                body += "<hr />";

                string selectHeader = canSelect ? @"<th><input type=""checkbox"" id=""hsAll"" autocomplete=""off"" onclick=""hsToggleAll(this.checked)"" title=""全选"" /></th>" : "";
                // 出现过（对局数 > 0）的行标浅黄色；隔行底色、鼠标悬停整行高亮，方便横向对照
                string temp = @"<style>
table.hsTable tr.hsRow:nth-child(even) { background: rgba(128, 128, 128, 0.07); }
table.hsTable tr.hsRow.hsSeen { background: rgba(255, 200, 0, 0.22); }
table.hsTable tr.hsRow:hover { background: rgba(80, 160, 255, 0.25); }
table.hsTable th[draggable=true] { cursor: move; }
table.hsTable th.hsDragOver { outline: 2px dashed rgba(80, 160, 255, 0.9); }
</style>";
                temp += $@"<table class=""hsTable"" border=0 style=""text-align: center;""><tr>{selectHeader}<th>卡牌名称</th><th>版本</th><th>类型</th><th>费用</th><th>身材</th><th style=""min-width: 16em;"">描述</th><th class=""hsSort"" data-key=""rarity"" data-label=""稀有度"" onclick=""hsSortBy('rarity')"" style=""cursor: pointer;"">稀有度 ⇅</th><th class=""hsSort"" data-key=""set"" data-label=""扩展包"" onclick=""hsSortBy('set')"" style=""cursor: pointer;"">扩展包 ⇅</th><th>拥有（普通/金卡）</th><th>分解</th><th>可得尘</th><th class=""hsSort"" data-key=""games"" data-label=""最高对局数"" onclick=""hsSortBy('games')"" style=""cursor: pointer;"">最高对局数 ⇅</th><th>出现过的卡组</th></tr>";
                for (int i = 0; i < candidates.Count; i++)
                {
                    var card = candidates[i];
                    // data-* 用于点表头排序：稀有度按 普通<稀有<史诗<传说，扩展包按游戏里的扩展包编号（大致是发布顺序）
                    temp += $@"<tr class=""hsRow{(card.MaxGames > 0 ? " hsSeen" : "")}"" data-rarity=""{(int)card.Rarity}"" data-set=""{(int)card.Set}"" data-setname=""{System.Net.WebUtility.HtmlEncode(GameStrings.GetCardSetName(card.Set))}"" data-games=""{card.MaxGames}"" data-order=""{i}"">";
                    if (canSelect) temp += $@"<td><input type=""checkbox"" class=""hsPick"" autocomplete=""off"" value=""{i}"" onchange=""hsUpdate()"" /></td>";
                    temp += $"<td>{System.Net.WebUtility.HtmlEncode(card.Name)}</td>";
                    temp += $"<td>{(card.Premium == TAG_PREMIUM.GOLDEN ? "金卡" : "普通")}</td>";
                    temp += $"<td>{CardTypeName(card.CardType)}</td>";
                    temp += $"<td>{card.Cost}</td>";
                    temp += $"<td>{card.Stats}</td>";
                    temp += $@"<td style=""text-align: left; white-space: normal;"">{CardTextHtml(card.Text)}</td>";
                    temp += $"<td>{RarityName(card.Rarity)}</td>";
                    temp += $"<td>{System.Net.WebUtility.HtmlEncode(GameStrings.GetCardSetName(card.Set))}</td>";
                    temp += $"<td>{card.NormalCount}/{card.GoldenCount}</td>";
                    temp += $"<td>{card.Count}</td>";
                    temp += $"<td>{card.SellValue * card.Count}</td>";
                    temp += $"<td>{(card.MaxGames > 0 ? $"{card.MaxGames}（{System.Net.WebUtility.HtmlEncode(card.GamesKey)}）" : "0")}</td>";
                    // 只给“出现过但不到最少对局数”的卡列卡组
                    string decks = minGames > 0 && card.MaxGames > 0 && card.Decks.Count > 0
                        ? string.Join("<br />", card.Decks.Select(d => $"{System.Net.WebUtility.HtmlEncode(d.Key)}（{d.Value}）"))
                        : "";
                    temp += $@"<td style=""text-align: left; white-space: nowrap;"">{decks}</td>";
                    temp += "</tr>";
                }
                temp += "</table>";
                // 点“稀有度”“扩展包”“最高对局数”表头排序，再点一次反过来；勾选框跟着行走，value 还是原来的序号
                temp += @"<script>
var hsSortState = { key: null, desc: false };
function hsSortBy(key) {
  hsSortState.desc = hsSortState.key === key ? !hsSortState.desc : false;
  hsSortState.key = key;
  var rows = Array.from(document.querySelectorAll('tr.hsRow'));
  if (rows.length === 0) return;
  var parent = rows[0].parentNode;
  rows.sort(function (a, b) {
    var r = (+a.dataset[key]) - (+b.dataset[key]);
    if (r === 0 && key === 'set') r = a.dataset.setname.localeCompare(b.dataset.setname);
    if (hsSortState.desc) r = -r;
    return r !== 0 ? r : (+a.dataset.order) - (+b.dataset.order);
  });
  rows.forEach(function (row) { parent.appendChild(row); });
  document.querySelectorAll('th.hsSort').forEach(function (th) {
    th.textContent = th.dataset.label + (th.dataset.key === key ? (hsSortState.desc ? ' ▼' : ' ▲') : ' ⇅');
  });
}
// 拖动表头调整列顺序；顺序按列的原始序号存在浏览器里（localStorage），下次打开照旧
(function () {
  var table = document.querySelector('table.hsTable');
  if (!table) return;
  var header = table.rows[0];
  for (var c = 0; c < header.cells.length; c++) {
    header.cells[c].dataset.col = c;
    header.cells[c].draggable = true;
  }
  function moveColumn(from, to) {
    if (from === to) return;
    Array.from(table.rows).forEach(function (row) {
      var cell = row.cells[from], ref = row.cells[to];
      if (!cell || !ref) return;
      row.insertBefore(cell, from < to ? ref.nextSibling : ref);
    });
  }
  function saveOrder() {
    try { localStorage.setItem('hsDisenchantCols', JSON.stringify(Array.from(header.cells).map(function (th) { return th.dataset.col; }))); } catch (e) {}
  }
  try {
    var saved = JSON.parse(localStorage.getItem('hsDisenchantCols') || 'null');
    if (saved && saved.length === header.cells.length) {
      saved.forEach(function (col, pos) {
        var from = Array.from(header.cells).findIndex(function (th) { return th.dataset.col === String(col); });
        if (from >= 0) moveColumn(from, pos);
      });
    }
  } catch (e) {}
  var dragFrom = -1;
  Array.from(header.cells).forEach(function (th) {
    th.addEventListener('dragstart', function (e) { dragFrom = th.cellIndex; e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', ''); });
    th.addEventListener('dragover', function (e) { e.preventDefault(); th.classList.add('hsDragOver'); });
    th.addEventListener('dragleave', function () { th.classList.remove('hsDragOver'); });
    th.addEventListener('drop', function (e) {
      e.preventDefault();
      th.classList.remove('hsDragOver');
      if (dragFrom >= 0) { moveColumn(dragFrom, th.cellIndex); saveOrder(); }
      dragFrom = -1;
    });
  });
})();
</script>";
                body += temp;
            }
            catch (Exception ex)
            {
                body += $@"无法生成分解预览：<br /><p style=""white-space: pre-line;"">{System.Net.WebUtility.HtmlEncode(ex.Message)}</p><br />";
            }
            return Template("Disenchant", body);
        }

        private static string CardTypeName(TAG_CARDTYPE type)
        {
            switch (type)
            {
                case TAG_CARDTYPE.MINION: return "随从";
                case TAG_CARDTYPE.SPELL: return "法术";
                case TAG_CARDTYPE.WEAPON: return "武器";
                case TAG_CARDTYPE.HERO: return "英雄";
                case TAG_CARDTYPE.LOCATION: return "地标";
                default: return type.ToString();
            }
        }

        // 卡牌描述：去掉 [x] 和换行，只保留加粗/斜体，其余全部转义
        private static string CardTextHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("[x]", "").Replace("\r", "");
            // 和游戏一样：中日文的换行直接去掉，其他语言换成空格
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<=[\u3000-\u9fff\uff00-\uffef])\n|\n(?=[\u3000-\u9fff\uff00-\uffef])", "");
            text = text.Replace("\n", " ");
            // 只去掉真正像标签的 <xxx>（保留 b/i），单独的“<”不动，以免吞掉后面的文字
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<(?!/?[bi]>)/?[A-Za-z][^<>]*>", "");
            string html = System.Net.WebUtility.HtmlEncode(text);
            foreach (string tag in new[] { "b", "i", "/b", "/i" })
                html = html.Replace($"&lt;{tag}&gt;", $"<{tag}>");
            return html.Trim();
        }

        private static string RarityName(TAG_RARITY rarity)
        {
            switch (rarity)
            {
                case TAG_RARITY.COMMON: return "普通";
                case TAG_RARITY.RARE: return "稀有";
                case TAG_RARITY.EPIC: return "史诗";
                case TAG_RARITY.LEGENDARY: return "传说";
                default: return rarity.ToString();
            }
        }

        public static StringBuilder MercenariesLettucePage()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(@"<h3 style=""text-align: center;"">佣兵关卡</h3>");

            try
            {
                string temp = @"<table border=0 style=""text-align: center;""><tr>";
                temp += "<th>索引</th>";
                temp += "<th>位置</th>";
                temp += "<th>名称</th>";
                temp += "<th>难度</th>";
                temp += "<th>碎片1</th>";
                temp += "<th>碎片2</th>";
                temp += "<th>碎片3</th>";
                temp += "</tr>";
                builder.Append(temp);

                foreach (var record in GameDbf.LettuceBounty.GetRecords().OrderBy(x => x.ID).ToList())
                {
                    if (record != null)
                    {
                        bool isComplete = MercenariesDataUtil.IsBountyComplete(record.ID);
                        temp = "<tr>";
                        temp += (isComplete ? "<td>" : "<td style=\"color:#FF4136\">") + $"{record.ID}</td>";
                        temp += (isComplete ? "<td>" : "<td style=\"color:#FF4136\">") + $"{record.BountySetRecord.Name.GetString()}</td>";
                        temp += (isComplete ? "<td>" : "<td style=\"color:#FF4136\">") + $"{GameDbf.Card.GetRecord(record.FinalBossCardId).Name.GetString()}</td>";
                        temp += (isComplete ? "<td>" : "<td style=\"color:#FF4136\">") + (record.Heroic ? "英雄" : "普通") + "</td>";

                        foreach (var finalReward in record.FinalBossRewards)
                        {
                            foreach (var merc in GameDbf.LettuceMercenary.GetRecords())
                            {

                                if (merc != null && merc.ID == finalReward.RewardMercenaryId)
                                {
                                    switch ((TAG_RARITY)merc.Rarity)
                                    {
                                        case TAG_RARITY.LEGENDARY:
                                            temp += $@"<td style=""color:#FFCC00"">{merc.MercenaryArtVariations[0].CardRecord.Name.GetString()}</td>";
                                            break;
                                        case TAG_RARITY.EPIC:
                                            temp += $@"<td style=""color:#CC99CC"">{merc.MercenaryArtVariations[0].CardRecord.Name.GetString()}</td>";
                                            break;
                                        case TAG_RARITY.RARE:
                                            temp += $@"<td style=""color:#99CCFF"">{merc.MercenaryArtVariations[0].CardRecord.Name.GetString()}</td>";
                                            break;
                                        default:
                                            temp += $@"<td>{merc.MercenaryArtVariations[0].CardRecord.Name.GetString()}</td>";
                                            break;
                                    }
                                    break;
                                }
                            }

                        }

                        temp += "</tr>";
                        builder.Append(temp);
                    }

                }
                builder.Append("</table>");
            }
            catch (Exception ex)
            {
                builder.Append($@"佣兵关卡信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p><br />");
            }
            finally
            {
                Utils.LeakInfo.Mercenaries();
            }
            return Template(builder, "Mercenaries");
        }
        public static StringBuilder SkinsPage()
        {

            StringBuilder builder = new StringBuilder();
            string body = @"<h3 style=""text-align: center;"">皮肤信息</h3>";
            builder.Append(body);

            body = "<h4>宠物</h4>";
            try
            {
                string temp = @"<table border=0 style=""text-align: center;""><tr>";

                temp += "<th>索引</th>";
                temp += "<th>宠物</th>";
                temp += "<th>名称</th>";
                temp += "</tr>";

                foreach (var record in GameDbf.PetVariant.GetRecords().OrderBy(x => x.ID).ToList())
                {
                    if (record != null)
                    {
                        temp += "<tr>";
                        temp += $"<td>{record.ID}</td>";
                        temp += $"<td>{record.PetId}</td>";

                        temp += $"<td>{record.Name.GetString()}</td>";
                        temp += "</tr>";
                    }

                }

                temp += "</table>";
                body += temp;
            }
            catch (Exception ex)
            {
                body += $@"宠物信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p>";
            }
            finally
            {
                body += "<br /><hr />";
            }
            builder.Append(body);



            body = "<h4>幸运币</h4>";
            try
            {
                string temp = @"<table border=0 style=""text-align: center;""><tr>";
                temp += "<th>索引</th>";
                temp += "<th>名称</th>";
                temp += "</tr>";

                foreach (var record in GameDbf.CosmeticCoin.GetRecords().OrderBy(x => x.ID).ToList())
                {
                    if (record != null)
                    {
                        temp += "<tr>";
                        temp += $"<td>{record.CardId}</td>";
                        temp += $"<td>{record.Name.GetString()}</td>";
                        temp += "</tr>";
                    }

                }
                temp += "</table>";
                body += temp;
            }
            catch (Exception ex)
            {
                body += $@"幸运币信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p>";
            }
            finally
            {
                body += "<br /><hr />";
            }
            builder.Append(body);


            body = "<h4>卡背</h4>";
            try
            {
                string temp = @"<table border=0 style=""text-align: center;""><tr>";
                temp += "<th>索引</th>";
                temp += "<th>名称</th>";
                temp += "</tr>";

                foreach (var record in GameDbf.CardBack.GetRecords().OrderBy(x => x.ID).ToList())
                {
                    if (record != null)
                    {
                        temp += "<tr>";
                        temp += $"<td>{record.ID}</td>";
                        temp += $"<td>{record.Name.GetString()}</td>";
                        temp += "</tr>";
                    }
                }
                temp += "</table>";
                body += temp;
            }
            catch (Exception ex)
            {
                body += $@"卡背信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p>";
            }
            finally
            {
                body += "<br /><hr />";
            }
            builder.Append(body);


            body = "<h4>酒馆战斗面板</h4>";
            try
            {
                string temp = @"<table border=0 style=""text-align: center;""><tr>";
                temp += "<th>索引</th>";
                temp += "<th>名称</th>";
                temp += "</tr>";

                foreach (var record in GameDbf.BattlegroundsBoardSkin.GetRecords().OrderBy(x => x.ID).ToList())
                {
                    if (record != null)
                    {
                        temp += "<tr>";
                        temp += $"<td>{record.ID}</td>";
                        temp += $"<td>{record.CollectionName.GetString()}</td>";
                        temp += "</tr>";
                    }
                }
                temp += "</table>";
                body += temp;
            }
            catch (Exception ex)
            {
                body += $@"酒馆战斗面板信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p>";
            }
            finally
            {
                body += "<br /><hr />";
            }
            builder.Append(body);


            body = "<h4>酒馆终结特效</h4>";
            try
            {
                string temp = @"<table border=0 style=""text-align: center;""><tr>";
                temp += "<th>索引</th>";
                temp += "<th>名称</th>";
                temp += "</tr>";

                foreach (var record in GameDbf.BattlegroundsFinisher.GetRecords().OrderBy(x => x.ID).ToList())
                {
                    if (record != null)
                    {
                        temp += "<tr>";
                        temp += $"<td>{record.ID}</td>";
                        temp += $"<td>{record.CollectionName.GetString()}</td>";
                        temp += "</tr>";
                    }
                }
                temp += "</table>";
                body += temp;
            }
            catch (Exception ex)
            {
                body += $@"酒馆终结特效信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p>";
            }
            finally
            {
                body += "<br /><hr />";
                //Utils.LeakInfo.Skins();
            }
            builder.Append(body);


            body = "<h4>英雄</h4>";
            try
            {
                string temp = @"<table border=0 style=""text-align: center;""><tr>";
                temp += "<th>索引</th>";
                temp += "<th>名称</th>";
                temp += "<th>类型</th>";
                temp += "</tr>";

                foreach (var record in GameDbf.CardHero.GetRecords().OrderBy(x => x.HeroType).ToList())
                {
                    if (record != null)
                    {
                        temp += "<tr>";
                        temp += $"<td>{record.CardId}</td>";
                        temp += $"<td>{GameDbf.Card.GetRecord(record.CardId).Name.GetString()}</td>";
                        switch (record.HeroType)
                        {
                            case Assets.CardHero.HeroType.BATTLEGROUNDS_HERO:
                                temp += "<td>酒馆英雄</td>";
                                break;
                            case Assets.CardHero.HeroType.BATTLEGROUNDS_GUIDE:
                                temp += "<td>酒馆鲍勃</td>";
                                break;
                            default:
                                temp += "<td>对战英雄</td>";
                                break;
                        }
                        temp += "</tr>";
                    }
                }
                temp += "</table>";
                body += temp;
            }
            catch (Exception ex)
            {
                body += $@"英雄信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p>";
            }
            finally
            {
                body += "<br /><hr />";
                Utils.LeakInfo.Skins();
            }
            builder.Append(body);

            return Template(builder, "Skins");
        }

        public static StringBuilder MercenariesPage()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(@"<h3 style=""text-align: center;"">佣兵收藏</h3>");
            try
            {
                if (CollectionManager.Get() == null)
                {
                    throw new Exception();
                }
                string temp = @"<table border=0 style=""text-align: center;""><tr>";
                temp += "<th>名称</th>";
                temp += "<th>等级</th>";
                temp += "<th>硬币</th>";
                temp += "<th>状态</th>";
                temp += "<th>技能1</th>";
                temp += "<th>技能2</th>";
                temp += "<th>技能3</th>";
                temp += "<th>装备1</th>";
                temp += "<th>装备2</th>";
                temp += "<th>装备3</th>";
                temp += "</tr>";
                builder.Append(temp);

                foreach (var merc in CollectionManager.Get().FindMercenaries(null, null, null, null, null).m_mercenaries
                                    .OrderByDescending(x => x.m_owned)
                                    //.ThenByDescending(x => Utils.IsMercenaryFullyUpgraded(x))
                                    .ThenByDescending(x => x.m_isFullyUpgraded)
                                    .ThenByDescending(x => x.m_level)
                                    .ThenByDescending(x => Utils.CalcMercenaryCoinNeed(x))
                                    .ThenByDescending(x => x.m_currencyAmount)
                                    .ToList())
                {
                    if (merc != null)
                    {
                        temp = "<tr>";
                        switch (merc.m_rarity)
                        {
                            case TAG_RARITY.LEGENDARY:
                                temp += $@"<td style=""color:#FFCC00"">{merc.m_mercName}</td>";
                                break;
                            case TAG_RARITY.EPIC:
                                temp += $@"<td style=""color:#CC99CC"">{merc.m_mercName}</td>";
                                break;
                            case TAG_RARITY.RARE:
                                temp += $@"<td style=""color:#99CCFF"">{merc.m_mercName}</td>";
                                break;
                            default:
                                temp += $@"<td>{merc.m_mercName}</td>";
                                break;
                        }

                        temp += $"<td>{merc.m_level}</td>";

                        temp += $"<td>{merc.m_currencyAmount}</td>";
                        //temp += $"<td>{GameStrings.GetRoleName(merc.m_role)}</td>";
                        if (!merc.m_owned)
                        {
                            temp += "<td>" + ((merc.GetCraftingCost() - merc.m_currencyAmount > 0) ? $"制作需要{merc.GetCraftingCost() - merc.m_currencyAmount}硬币" : "可制作！") + "</td>";
                        }
                        //else if (Utils.IsMercenaryFullyUpgraded(merc))
                        else if (merc.m_isFullyUpgraded)
                        {
                            temp += "<td>全满！(+1+5)</td>";
                        }
                        else
                        {
                            long coinNeed = Utils.CalcMercenaryCoinNeed(merc);
                            if (coinNeed != 8192)
                                temp += $"<td>距离全满还需硬币：{coinNeed}</td>";
                            else
                            {
                                temp += $"<td>可升级至全满！</td>";
                            }
                        }

                        foreach (var ability in merc.m_abilityList)
                        {
                            if (ability != null)
                                temp += "<td>" + ability.GetCardName() + "</td>";
                        }
                        foreach (var equipment in merc.m_equipmentList)
                        {
                            if (equipment != null)
                                temp += (equipment.Owned ? "<td>" : "<td style=\"color:#FF4136\">") + equipment.GetCardName() + "</td>";
                        }

                        builder.Append(temp);

                        builder.Append("</tr>");
                    }
                }
                builder.Append("</table>");
            }
            catch (Exception ex)
            {
                builder.Append($@"佣兵收藏信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p>");
            }
            finally
            {
                builder.Append("<br />");
            }
            return Template(builder, "Mercenaries", false);
        }

        public static StringBuilder PackPage()
        {
            string body = @"<h3 style=""text-align: center;"">卡包信息</h3>";
            string temp = @"<table border=0 style=""text-align: center;""><tr>";
            temp += "<th>索引</th>";
            temp += "<th>名称</th>";
            temp += "<th>已开</th>";
            temp += "<th>剩余</th>";
            temp += "</tr>";

            int totalOpened = 0;
            int totalRemain = 0;

            try
            {
                foreach (var booster in GameDbf.Booster.GetRecords().OrderBy(x => x.ID).ToList())
                {
                    if (booster != null)
                    {
                        //Enum.GetValues(typeof(BoosterDbId))
                        string name = booster.Name.GetString();
                        name = (name != "" && name != null) ? name : Enum.GetName(typeof(BoosterDbId), booster.ID);
                        if (name == "" || name == null)
                        {
                            var Dbids = Enum.GetNames(typeof(BoosterDbId)).ToList();
                            if (booster.ID < Dbids.Count)
                            {
                                name = Dbids[booster.ID];
                            }
                            else
                            {
                                name = "未知";
                            }

                        }
                        temp += "<tr>";
                        temp += $"<td>{booster.ID}</td>";
                        temp += $"<td>{name}</td>";
                        int opened = BoosterPackUtils.GetBoosterOpenedCount((int)booster.ID);
                        int remainCount = BoosterPackUtils.GetBoosterCount((int)booster.ID);
                        totalOpened += opened;
                        totalRemain += remainCount;
                        temp += "<td>" + ((opened == 0) ? "-" : opened.ToString()) + "</td>";
                        temp += "<td>" + ((remainCount == 0) ? "-" : remainCount.ToString()) + "</td>";
                        temp += "</tr>";
                    }
                }
                body += temp;
                body += $"</table><br /><p>您一共开了{totalOpened}包，目前还有{totalRemain}包未开。</p>";
            }
            catch (Exception ex)
            {
                body += $@"卡包信息获取异常<br /><p style=""white-space: pre-line;"">{ex}</p>";
            }
            finally
            {
                body += "<br />";
            }
            return Template("Pack", body);
        }

        public static StringBuilder MatchLogPage()
        {
            StringBuilder builder = new StringBuilder();
            if (!System.IO.File.Exists(CommandConfig.hsMatchLogPath)) return Template(builder.Append("对局文件不存在！"), "MatchLog");
            else builder.Append(@"<h3 style=""text-align: center;"">对局记录</h3>");

            try
            {
                string temp = @"<table border=0 style=""text-align: center;""><tr>";
                temp += "<th>结束时间</th>";
                temp += "<th>对局结果</th>";
                temp += "<th>当前排名</th>";
                temp += "<th>游戏模式</th>";
                temp += "<th>你的对手</th>";
                temp += "<th>玩家信息</th>";
                temp += "</tr>";
                builder.Append(temp);

                foreach (string line in System.IO.File.ReadLines(CommandConfig.hsMatchLogPath).Reverse())
                {
                    temp = "";
                    if (line != String.Empty)
                    {
                        temp += "<tr>";
                        string[] lineSplit = line.Split(',');
                        for (int i = 0; i < lineSplit.Length; i++)
                        {
                            if (i == 1 && lineSplit[i].Length > 0)
                            {
                                if (lineSplit[i] == "胜利") temp += $"<td style=\"color:#01FF70\">胜利</td>";
                                else if (lineSplit[i] == "失败") temp += $"<td style=\"color:#FF4136\">失败</td>";
                                else if (lineSplit[i] == "未知" || lineSplit[i] == "平局") temp += $"<td>{lineSplit[i]}</td>";
                                else if (lineSplit[i] == "第一名") temp += $"<td style=\"color:#01FF70\">第一名</td>";
                                else if (lineSplit[i] == "第二名") temp += $"<td style=\"color:#01FF70\">第二名</td>";
                                else if (lineSplit[i] == "第三名") temp += $"<td style=\"color:#01FF70\">第三名</td>";
                                else if (lineSplit[i] == "第四名") temp += $"<td style=\"color:#01FF70\">第四名</td>";
                                else if (lineSplit[i] == "第五名") temp += $"<td style=\"color:#FF4136\">第五名</td>";
                                else if (lineSplit[i] == "第六名") temp += $"<td style=\"color:#FF4136\">第六名</td>";
                                else if (lineSplit[i] == "第七名") temp += $"<td style=\"color:#FF4136\">第七名</td>";
                                else if (lineSplit[i] == "第八名") temp += $"<td style=\"color:#FF4136\">第八名</td>";
                                else if (int.Parse(lineSplit[i]) > 0) temp += $"<td style=\"color:#01FF70\">+{int.Parse(lineSplit[i])}</td>";
                                else if (int.Parse(lineSplit[i]) < 0) temp += $"<td style=\"color:#FF4136\">{lineSplit[i]}</td>";
                                else temp += $"<td>{lineSplit[i]}</td>";
                            }
                            else temp += $"<td>{lineSplit[i]}</td>";
                        }
                        temp += "</tr>";
                    }
                    builder.Append(temp);
                }
            }
            catch (Exception ex)
            {
                builder.Append($@"</table>日志解析异常<br /><p style=""white-space: pre-line;"">{ex}</p>");
            }
            finally
            {
                builder.Append("</table>");
            }
            return Template(builder, "MatchLog", false);
        }

        public static StringBuilder AlivePage()
        {
            return new StringBuilder().Append($"{{\"pid\":{System.Diagnostics.Process.GetCurrentProcess()?.Id},\"login\":\"{Utils.CacheLoginStatus}\"}}");
        }

        public static StringBuilder ConfigPage()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(@"<h3 style=""text-align: center;"">" + LocalizationManager.GetLangValue("config.page.title") + "</h3>");

            string configHtml = FileManager.ReadEmbeddedFile("./WebResources/config.html");
            configHtml = configHtml
                .Replace("{config.page.language}", LocalizationManager.GetLangValue("config.page.language"))
                .Replace("{config.page.lang.auto}", LocalizationManager.GetLangValue("config.page.lang.auto"))
                .Replace("{config.page.search}", LocalizationManager.GetLangValue("config.page.search"))
                .Replace("{config.page.success}", LocalizationManager.GetLangValue("config.page.success"))
                .Replace("{config.page.error}", LocalizationManager.GetLangValue("config.page.error"))
                .Replace("{config.page.advanced}", LocalizationManager.GetLangValue("config.page.advanced"))
                .Replace("{config.page.warning}", LocalizationManager.GetLangValue("config.page.warning"))
                .Replace("{config.page.warning.desc}", LocalizationManager.GetLangValue("config.page.warning.desc"))
                .Replace("{config.page.cancel}", LocalizationManager.GetLangValue("config.page.cancel"))
                .Replace("{config.page.confirm}", LocalizationManager.GetLangValue("config.page.confirm"));

            builder.AppendLine(configHtml);
            return Template(builder, "Config");
        }

        public static StringBuilder BepInExLogPage(int lines = -1)
        {
            string logPath = Path.Combine(BepInEx.Paths.BepInExRootPath, "LogOutput.log");
            StringBuilder output = new StringBuilder().Append("");

            if (File.Exists(logPath))
            {
                try
                {
                    if (lines <= 0)
                    {
                        // 以共享模式打开文件
                        using (FileStream fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (StreamReader reader = new StreamReader(fs))
                            {
                                string content = reader.ReadToEnd();
                                output.Append(content);
                            }
                        }
                    }
                    else
                    {
                        output.Append(Utils.ReadLastLine(logPath, 666));
                    }
                }
                catch (Exception ex)
                {
                    output.Append(ex.Message);
                }
            }
            return output;
        }
        public static StringBuilder HsModCfgPage(string cfg)
        {
            string cfgPath = Path.Combine(BepInEx.Paths.ConfigPath, CommandConfig.GlobalHSUnitID, cfg);
            StringBuilder output = new StringBuilder().Append("");

            if (File.Exists(cfgPath))
            {
                try
                {
                    // 以共享模式打开文件
                    using (FileStream fs = new FileStream(cfgPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (StreamReader reader = new StreamReader(fs))
                        {
                            string content = reader.ReadToEnd();
                            output.Append(content);
                        }
                    }
                }
                catch (Exception ex)
                {
                    output.Append(ex.Message);
                }
            }
            return output;
        }
    }
}
