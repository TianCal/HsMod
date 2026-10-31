#!/usr/bin/env python3
"""Build or update the list of cards seen in popular constructed decks.

Downloads Firestone's public deck stats (current season, all ranks) for
Standard and Wild, decodes every deck code, and merges the cards into a JSON
file. The file only grows: cards seen in any earlier run stay in it, so
running this once per season accumulates several seasons.

Reprints share a name (e.g. CORE_ copies), so every collectible printing with
the same English name as a seen card is marked as used too.

Usage: python3 tools/used_cards.py [--out PATH]

How many games make a card "used" is decided in HsMod by the
"Disenchant: Min Games" setting, not here.
"""
import argparse
import base64
import datetime
import gzip
import json
import os
import sys
import urllib.request

FIRESTONE = "https://static.zerotoheroes.com/api/constructed/stats/decks/{fmt}/all/current-season/overview-from-hourly.gz.json"
HSJSON = "https://api.hearthstonejson.com/v1/latest/enUS/cards.json"
FORMATS = ("standard", "wild")
DEFAULT_OUT = "/Applications/Hearthstone/BepInEx/config/HsUsedCards.json"
TOP_ARCHETYPES = 5


def fetch_json(url):
    req = urllib.request.Request(url, headers={"User-Agent": "HsMod-used-cards"})
    with urllib.request.urlopen(req, timeout=120) as resp:
        raw = resp.read()
    if raw[:2] == b"\x1f\x8b":
        raw = gzip.decompress(raw)
    return json.loads(raw)


def read_varint(data, pos):
    result = shift = 0
    while True:
        b = data[pos]
        pos += 1
        result |= (b & 0x7F) << shift
        if not b & 0x80:
            return result, pos
        shift += 7


def decode_deck(deckstring):
    """Return the dbfIds of all cards in a deck code, sideboards included."""
    data = base64.b64decode(deckstring)
    pos = 0
    _, pos = read_varint(data, pos)  # reserved
    _, pos = read_varint(data, pos)  # version
    _, pos = read_varint(data, pos)  # format
    n, pos = read_varint(data, pos)
    for _ in range(n):  # heroes
        _, pos = read_varint(data, pos)
    cards = set()
    for copies in (1, 2, None):
        n, pos = read_varint(data, pos)
        for _ in range(n):
            dbf, pos = read_varint(data, pos)
            if copies is None:
                _, pos = read_varint(data, pos)
            cards.add(dbf)
    if pos < len(data):
        has_sideboards, pos = read_varint(data, pos)
        if has_sideboards:
            for copies in (1, 2, None):
                n, pos = read_varint(data, pos)
                for _ in range(n):
                    dbf, pos = read_varint(data, pos)
                    if copies is None:
                        _, pos = read_varint(data, pos)
                    _, pos = read_varint(data, pos)  # owner card
                    cards.add(dbf)
    return cards


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", default=DEFAULT_OUT)
    args = parser.parse_args()

    cards = fetch_json(HSJSON)
    by_dbf = {c["dbfId"]: c for c in cards}
    printings = {}
    for c in cards:
        # Hero skins share names with playable hero cards (e.g. Lord Jaraxxus); keep
        # disenchantable HERO cards, drop only skins.
        if c.get("collectible") and c.get("set") != "HERO_SKINS":
            printings.setdefault(c["name"], set()).add(c["dbfId"])

    if os.path.exists(args.out):
        with open(args.out, encoding="utf-8") as f:
            db = json.load(f)
    else:
        db = {"runs": [], "cards": {}}

    run = {"date": datetime.date.today().isoformat(), "formats": {}}
    for fmt in FORMATS:
        stats = fetch_json(FIRESTONE.format(fmt=fmt))
        # Games are summed per card name, so a card played under several printings
        # (e.g. CS1_112 and CORE_CS1_112) gets the games of all of them, and a deck
        # holding two printings of one card counts once.
        seen = {}
        archetypes = {}   # card name -> {deck archetype: games}
        printing_of = {}
        decks = skipped = 0
        for deck in stats.get("deckStats", []):
            games = deck.get("totalGames", 0)
            if not deck.get("decklist"):
                continue
            try:
                ids = decode_deck(deck["decklist"])
            except (IndexError, ValueError):
                skipped += 1
                continue
            decks += 1
            archetype = deck.get("archetypeName") or f"archetype-{deck.get('archetypeId', '?')}"
            for name in {by_dbf.get(dbf, {}).get("name") or f"#{dbf}" for dbf in ids}:
                seen[name] = seen.get(name, 0) + games
                per = archetypes.setdefault(name, {})
                per[archetype] = per.get(archetype, 0) + games
            for dbf in ids:
                printing_of.setdefault(by_dbf.get(dbf, {}).get("name") or f"#{dbf}", set()).add(dbf)
        run["formats"][fmt] = {"lastUpdated": stats.get("lastUpdated"),
                               "decks": decks, "cards": len(seen)}
        # Keyed by format and season month, so rerunning within a season overwrites
        # instead of double counting.
        season = (stats.get("lastUpdated") or run["date"])[:7]
        for name, games in seen.items():
            # The deck archetypes that played the card most, for showing why a card
            # counts (or almost counts) as used.
            top = dict(sorted(archetypes[name].items(), key=lambda x: -x[1])[:TOP_ARCHETYPES])
            for same in printings.get(name, set()) | printing_of[name]:
                entry = db["cards"].setdefault(str(same), {
                    "name": by_dbf.get(same, {}).get("name", name),
                    "games": {}, "firstSeen": run["date"]})
                key = f"{fmt}:{season}"
                if games >= entry["games"].get(key, 0):
                    entry.setdefault("decks", {})[key] = top
                entry["games"][key] = max(entry["games"].get(key, 0), games)
                entry["lastSeen"] = run["date"]
        print(f"{fmt}: {decks} decks, {len(seen)} distinct cards, {skipped} undecodable decks skipped", file=sys.stderr)

    db["runs"].append(run)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    # Write to a temp file and swap it in, so HsMod never reads a half-written file.
    tmp = args.out + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(db, f, ensure_ascii=False, indent=1, sort_keys=True)
    os.replace(tmp, args.out)
    print(f"{len(db['cards'])} used card printings -> {args.out}", file=sys.stderr)


if __name__ == "__main__":
    main()
