using System;
using System.Collections.Generic;
using static HsMod.PluginConfig;

namespace HsMod
{
    public partial class Utils
    {
        private static GameState randomPremiumGame;
        private static readonly Dictionary<string, bool> randomPremiumPicks = new Dictionary<string, bool>();
        private static readonly Random randomPremium = new Random();

        // 同时有钻石和异画的卡，按“实体 ID + 卡牌 ID”在本局内只抽一次，之后一直沿用。
        // 变形后卡牌 ID 不同会重新抽；新的一局（新的 GameState）清空记录。
        private static bool RandomPicksSignature(EntityBase entity)
        {
            GameState gameState = GameState.Get();
            if (!ReferenceEquals(gameState, randomPremiumGame))
            {
                randomPremiumPicks.Clear();
                randomPremiumGame = gameState;
            }
            string key = entity.GetEntityId() + ":" + entity.GetCardId();
            if (!randomPremiumPicks.TryGetValue(key, out bool pickSignature))
            {
                pickSignature = randomPremium.Next(2) == 0;
                randomPremiumPicks[key] = pickSignature;
            }
            return pickSignature;
        }

        public static bool GetPremiumType<T>(ref T ___m_entity, ref TAG_PREMIUM __result)
            where T : EntityBase
        {
            try
            {
                if (GameMgr.Get() != null && GameState.Get() != null && ___m_entity != null && GameState.Get().IsGameCreatedOrCreating())
                {
                    //跳过酒馆随从
                    if (GameMgr.Get().IsBattlegrounds())
                    {
                        if (!isBgsGoldenEnable.Value || ___m_entity.IsMinion() || ___m_entity.IsQuest())
                            return true;
                    }

                    Utils.CardState mGolden = goldenCardState.Value;
                    Utils.CardState mMaxState = maxCardState.Value;

                    //佣兵镀金
                    int dbid = GameUtils.TranslateCardIdToDbId(___m_entity.GetCardId());
                    bool mercDiamond = false;
                    bool isMerc = false;
                    if (Utils.CheckInfo.IsMercenarySkin(___m_entity.GetCardId(), out Utils.MercenarySkin skin))
                    {
                        isMerc = true;
                        if (dbid == skin.Diamond)
                        {
                            mercDiamond = true;
                        }
                    }

                    var controllerSide = GameState.Get()?.GetPlayer(___m_entity.GetControllerId())?.GetSide() ?? Player.Side.NEUTRAL;
                    bool isControlledByOpposingSidePlayer = controllerSide == Player.Side.OPPOSING;
                    bool isControlledByFriendlySidePlayer = controllerSide == Player.Side.FRIENDLY;

                    //屏蔽对手特效
                    if (isControlledByOpposingSidePlayer && (!isOpponentGoldenCardShow.Value) && (!GameMgr.Get().IsBattlegrounds()))
                    {
                        __result = TAG_PREMIUM.NORMAL;
                        if (isMerc)
                        {
                            ___m_entity.SetTag(GAME_TAG.PREMIUM, TAG_PREMIUM.NORMAL);
                            ___m_entity.SetTag(GAME_TAG.HAS_DIAMOND_QUALITY, false);
                        }
                        return false;
                    }


                    //其他品质
                    if (___m_entity.HasTag(GAME_TAG.HAS_DIAMOND_QUALITY) || ___m_entity.HasTag(GAME_TAG.HAS_SIGNATURE_QUALITY) || mercDiamond)
                    {
                        if (___m_entity.HasTag(GAME_TAG.HAS_DIAMOND_QUALITY) || mercDiamond)
                        {
                            if (mMaxState == Utils.CardState.All || (mMaxState == Utils.CardState.OnlyMy && isControlledByFriendlySidePlayer))
                            {
                                if (mercDiamond)
                                {
                                    ___m_entity.SetTag(GAME_TAG.PREMIUM, TAG_PREMIUM.DIAMOND);
                                    ___m_entity.SetTag(GAME_TAG.HAS_DIAMOND_QUALITY, true);
                                }

                                // 异画优先；开启钻石异画随机时，每张实体卡每局随机选一个
                                if (___m_entity.HasTag(GAME_TAG.HAS_SIGNATURE_QUALITY) &&
                                    isSignatureCardStateEnable.Value &&
                                    (signatureDiamondRandom.Value ? RandomPicksSignature(___m_entity) : signatureFirst.Value))
                                {
                                    ___m_entity.SetTag(GAME_TAG.PREMIUM, TAG_PREMIUM.SIGNATURE);
                                    __result = TAG_PREMIUM.SIGNATURE;
                                    return false;
                                }

                                __result = TAG_PREMIUM.DIAMOND;
                                return false;
                            }
                        }

                        if (___m_entity.HasTag(GAME_TAG.HAS_SIGNATURE_QUALITY) && isSignatureCardStateEnable.Value)
                        {
                            if (mMaxState == Utils.CardState.All || (mMaxState == Utils.CardState.OnlyMy && isControlledByFriendlySidePlayer))
                            {
                                __result = TAG_PREMIUM.SIGNATURE;
                                ___m_entity.SetTag(GAME_TAG.PREMIUM, TAG_PREMIUM.SIGNATURE);
                                return false;
                            }
                        }

                        if ((mMaxState == Utils.CardState.Disabled) && (mGolden == Utils.CardState.Disabled))
                        {
                            __result = TAG_PREMIUM.NORMAL;
                            if (isMerc)
                            {
                                ___m_entity.SetTag(GAME_TAG.PREMIUM, TAG_PREMIUM.NORMAL);
                                ___m_entity.SetTag(GAME_TAG.HAS_DIAMOND_QUALITY, false);
                            }
                            return false;
                        }
                    }
                    //金卡特效
                    if (mGolden == Utils.CardState.All || (mGolden == Utils.CardState.OnlyMy && isControlledByFriendlySidePlayer))
                    {
                        ___m_entity.SetTag(GAME_TAG.PREMIUM, TAG_PREMIUM.GOLDEN);
                        __result = TAG_PREMIUM.GOLDEN;
                        return false;
                    }
                    //禁用特效
                    if (mGolden == Utils.CardState.Disabled)
                    {
                        __result = TAG_PREMIUM.NORMAL;
                        if (isMerc)
                        {
                            ___m_entity.SetTag(GAME_TAG.PREMIUM, TAG_PREMIUM.NORMAL);
                            ___m_entity.SetTag(GAME_TAG.HAS_DIAMOND_QUALITY, false);
                        }
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex);
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex.StackTrace);
            }
            return true;
        }

        public static void UpdateHeroTag(string cardId)
        {
            if (!Utils.CheckInfo.IsHero(cardId, out _))
            {
                return;
            }
            try
            {
                var defLoader = DefLoader.Get();
                if (defLoader == null)
                {
                    return;
                }
                //if (!defLoader.HasLoadedEntityDefs())
                //{
                //    defLoader.LoadAllEntityDefs();
                //}
                EntityDef entityDef = defLoader.GetEntityDef(cardId);
                if (entityDef == null)
                {
                    return;
                }
                if (entityDef.HasTag(GAME_TAG.EMOTECHARACTER) && (entityDef?.GetTag(GAME_TAG.EMOTECHARACTER) > 0))
                {
                    GameState gameState = GameState.Get();
                    if (gameState == null)
                    {
                        return;
                    }
                    int entityId = gameState.GetPlayerBySide(Player.Side.FRIENDLY).GetEntityId();
                    int tag = gameState.GetEntity(entityId).GetTag(GAME_TAG.HERO_ENTITY);
                    gameState.GetEntity(tag)?.SetTag(GAME_TAG.EMOTECHARACTER, entityDef.GetTag(GAME_TAG.EMOTECHARACTER));
                }
                // fix 神话皮远程攻击
                if (entityDef.HasTag(GAME_TAG.HERO_DOESNT_MOVE_ON_ATTACK) &&
                    (entityDef?.GetTag(GAME_TAG.HERO_DOESNT_MOVE_ON_ATTACK) > 0))
                {
                    GameState gameState = GameState.Get();
                    if (gameState == null)
                    {
                        return;
                    }
                    int entityId = gameState.GetPlayerBySide(Player.Side.FRIENDLY).GetEntityId();
                    int tag = gameState.GetEntity(entityId).GetTag(GAME_TAG.HERO_ENTITY);
                    gameState.GetEntity(tag)?.SetTag(GAME_TAG.HERO_DOESNT_MOVE_ON_ATTACK, entityDef.GetTag(GAME_TAG.HERO_DOESNT_MOVE_ON_ATTACK));
                }
                if (entityDef.HasTag(GAME_TAG.CORNER_REPLACEMENT_TYPE) && (entityDef?.GetTag(GAME_TAG.CORNER_REPLACEMENT_TYPE) > 0))
                {
                    GameState gameState2 = GameState.Get();
                    if (gameState2 == null)
                    {
                        return;
                    }
                    int entityId2 = gameState2.GetPlayerBySide(Player.Side.FRIENDLY).GetEntityId();
                    gameState2.GetEntity(entityId2)?.SetTag(GAME_TAG.CORNER_REPLACEMENT_TYPE, entityDef.GetTag(GAME_TAG.CORNER_REPLACEMENT_TYPE));
                    gameState2.UpdateCornerReplacements();
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex);
            }
        }

        public static class CheckInfo
        {
            public static bool IsCoin()
            {
                if (CacheCoin.Count == 0) CacheInfo.UpdateCoin();
                if (CacheCoin.Contains(skinCoin.Value)) return true;
                else return false;
            }
            public static bool IsCoin(string cardId)
            {
                int dbId = GameUtils.TranslateCardIdToDbId(cardId);
                if (CacheCoinCard.Count == 0) CacheInfo.UpdateCoinCard();
                if (CacheCoinCard.Contains(dbId)) return true;
                else return false;
            }
            public static bool IsBoard()
            {
                if (CacheGameBoard.Count == 0) CacheInfo.UpdateGameBoard();
                if (CacheGameBoard.Contains(skinBoard.Value)) return true;
                else return false;
            }
            public static bool IsBgsBoard()
            {
                if (CacheBgsBoard.Count == 0) CacheInfo.UpdateBgsBoard();
                if (CacheBgsBoard.Contains(skinBgsBoard.Value)) return true;
                else return false;
            }
            public static bool IsHero(int DbID, out Assets.CardHero.HeroType heroType)
            {
                if (CacheHeroes.Count == 0) CacheInfo.UpdateHeroes();
                if (CacheHeroes.ContainsKey(DbID))
                {
                    heroType = CacheHeroes[DbID];
                    return true;
                }
                if (DefLoader.Get()?.GetEntityDef(DbID)?.GetCardType() == TAG_CARDTYPE.HERO)
                {
                    heroType = Assets.CardHero.HeroType.UNKNOWN;
                    return true;
                }
                else
                {
                    heroType = Assets.CardHero.HeroType.UNKNOWN;
                    return false;
                }
            }
            public static bool IsCardBack()
            {
                if (CacheCardBack.Count == 0) CacheInfo.UpdateCardBack();
                if (CacheCardBack.Contains(skinCardBack.Value)) return true;
                else return false;
            }
            public static bool IsBgsFinisher()
            {
                if (CacheBgsFinisher.Count == 0) CacheInfo.UpdateBgsFinisher();
                if (CacheBgsFinisher.Contains(skinBgsFinisher.Value)) return true;
                else return false;
            }

            public static bool IsHero(string cardID, out Assets.CardHero.HeroType heroType)
            {
                if (CacheHeroes.Count == 0) CacheInfo.UpdateHeroes();
                int dbid = GameUtils.TranslateCardIdToDbId(cardID);
                if (CacheHeroes.ContainsKey(dbid))
                {
                    heroType = CacheHeroes[dbid];
                    return true;
                }
                else
                {
                    heroType = Assets.CardHero.HeroType.UNKNOWN;
                    return false;
                }
            }

            public static bool IsMercenarySkin(string cardID, out MercenarySkin skin)
            {
                if (CacheMercenarySkin.Count == 0) CacheInfo.UpdateMercenarySkin();
                int dbid = GameUtils.TranslateCardIdToDbId(cardID);

                foreach (var mercSkin in CacheMercenarySkin)
                {
                    if (mercSkin.Id.Contains(dbid))
                    {
                        skin = mercSkin;
                        return true;
                    }
                }
                skin = new MercenarySkin();
                return false;
            }

        }
    }

}
