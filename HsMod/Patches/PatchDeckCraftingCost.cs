using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace HsMod
{
    public partial class Patcher
    {
        public static class PatchDeckCraftingCost
        {
            private const float PriceCacheSeconds = 15f;
            private static readonly ConditionalWeakTable<CollectionDeck, PriceCache> PriceCaches =
                new ConditionalWeakTable<CollectionDeck, PriceCache>();

            private sealed class PriceCache
            {
                public readonly Dictionary<string, int> Prices = new Dictionary<string, int>();
                public float ExpiresAt;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CollectionDeckTray), "OnCardCountUpdated")]
            private static bool OnCardCountUpdated(CollectionDeckTray __instance, int cardCount, int maxCount)
            {
                if (__instance.m_countText == null || !RefreshCostLabel(__instance))
                {
                    return true;
                }

                string count = GameStrings.Format("GLUE_DECK_TRAY_COUNT", cardCount, maxCount);
                if (__instance.m_countText.Text != count)
                {
                    __instance.m_countText.Text = count;
                }
                return false;
            }

            private static bool RefreshCostLabel(CollectionDeckTray tray)
            {
                if (tray.m_countLabelText == null ||
                    tray.GetCurrentContentType() != DeckTray.DeckContentTypes.Cards)
                {
                    return false;
                }

                var collection = CollectionManager.Get();
                var deck = collection != null ? tray.GetCardsContent()?.GetEditingDeck() : null;
                if (deck == null || !deck.IsConstructedDeck)
                {
                    return false;
                }

                PriceCache cache = PriceCaches.GetValue(deck, _ => new PriceCache());
                float now = Time.realtimeSinceStartup;
                if (now >= cache.ExpiresAt)
                {
                    cache.Prices.Clear();
                    cache.ExpiresAt = now + PriceCacheSeconds;
                }

                int dust = 0;
                bool hasUncraftableCards = false;
                AddMissingCardCosts(deck, collection, cache.Prices, ref dust, ref hasUncraftableCards);
                foreach (var sideboard in deck.GetAllSideboards().Values)
                {
                    AddMissingCardCosts(sideboard, collection, cache.Prices, ref dust, ref hasUncraftableCards);
                }

                string label;
                if (dust > 0)
                {
                    string key = hasUncraftableCards
                        ? "deck.craftableCardsDust"
                        : "deck.missingCardsDust";
                    label = string.Format(LocalizationManager.GetLangValue(key), dust);
                }
                else if (hasUncraftableCards)
                {
                    label = LocalizationManager.GetLangValue("deck.missingCardsUncraftable");
                }
                else
                {
                    label = GameStrings.Get("GLUE_DECK_TRAY_CARD_COUNT_LABEL");
                }
                if (tray.m_countLabelText.Text != label)
                {
                    tray.m_countLabelText.Text = label;
                }
                return true;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CollectionDeckTray), "OnSideboardCardCountUpdated")]
            private static void OnSideboardChanged(CollectionDeckTray __instance)
            {
                RefreshCostLabel(__instance);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CollectionDeckTray), nameof(CollectionDeckTray.HandleAddedCardDeckUpdate))]
            private static void OnCollectionCardAdded(CollectionDeckTray __instance)
            {
                RefreshCostLabel(__instance);
            }

            private static void AddMissingCardCosts(CollectionDeck deck, CollectionManager collection,
                Dictionary<string, int> prices, ref int dust, ref bool hasUncraftableCards)
            {
                foreach (var slot in deck.GetSlots())
                {
                    if (slot.Owned || slot.Count <= 0 || !GameUtils.IsCardCollectible(slot.CardID))
                    {
                        continue;
                    }

                    // Bundled cards are granted with their primary card.
                    if (slot.GetEntityDef()?.HasTag(GAME_TAG.IS_FABLED_BUNDLE_CARD) == true)
                    {
                        continue;
                    }

                    var card = collection.GetCard(slot.CardID, TAG_PREMIUM.NORMAL);
                    if (card?.IsCraftable != true)
                    {
                        hasUncraftableCards = true;
                        continue;
                    }

                    if (!prices.TryGetValue(slot.CardID, out int cost))
                    {
                        cost = CraftingManager.GetCardValue(slot.CardID, TAG_PREMIUM.NORMAL)?.GetBuyValue() ?? 0;
                        if (cost > 0)
                        {
                            prices.Add(slot.CardID, cost);
                        }
                    }
                    if (cost <= 0)
                    {
                        hasUncraftableCards = true;
                        continue;
                    }
                    dust += cost * slot.Count;
                }
            }
        }
    }
}
