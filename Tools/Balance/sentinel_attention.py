"""
Sentinel dikkati bir oyun boyunca nasıl birikir?

Oyundaki kurallarla (docs/TASARIM.md) bir oyuncu politikası için operasyon operasyon dikkat hesaplar.
Operasyon başına taban dikkatler, koddaki ölçülmüş sonuçlardan alındı (testlerle doğrulanan sayılar):

  AKIŞ  kurtarma, yük atarak (doğal)          3-4   -> 4
  AKIŞ  kurtarma, kimseyi feda etmeden         11
  AKIŞ  zarar, doğal görünen yol               6
  AKIŞ  zarar, elle kesme (saldırı)            17
  AĞ    gizli, kopyasız                        2
  AĞ    gizli, kopyayla (kopya ele geçer)      2 + 3 = 5
  AĞ    fark edilip kaçmak                     2 + 4 + 5 = 11

Varsayımlar (değişebilir, en altta duyarlılık tablosu var):
  - Oyunda 40 operasyon var (6-7 saat, operasyon başına ~10 dakika).
  - Olayların saat dilimi oyuncunun seçimi değil; iki senaryo: hepsi akşam / üç dilime eşit dağılmış.
  - Sentinel imzası: kullanılan her taktik için ek bedel = 2 x seviye; kullanılınca +1, kullanılmayınca -1,
    kopya ele geçerse +2. Şu anki kurallarda seviye en fazla 3 ve her olayda dikkat önce 6 azalır.

Çalıştırma:  python3 Tools/Balance/sentinel_attention.py
"""

OPERATIONS = 40
ATTENTION_PER_LEVEL = 2
DECOY_CAPTURE_LEVELS = 2

# (ad, taban dikkat, taktik, kopya ele geçti mi)
SAVE_SHED = ("kurtarma, yük atarak", 4, "yuk_atarak_kurtarma", False)
SAVE_NO_SACRIFICE = ("kurtarma, kimseyi feda etmeden", 11, "aktarma", False)
HARM_NATURAL = ("zarar, doğal yol", 6, "kendiliginden_attirma", False)
NET_UNSEEN = ("AĞ gizli", 2, None, False)
NET_DECOY = ("AĞ kopyayla", 5, "kopya", True)

POLICIES = {
    # Her zaman aynı doğal hileyi kullanan oyuncu.
    "Tek taktik (hep yük atarak kurtarır)": [SAVE_SHED],
    "Tek taktik (hep kopya)": [NET_DECOY],
    # Kimseyi feda etmeyen ama iz bırakan oyuncu.
    "Kimseyi feda etmeyen": [SAVE_NO_SACRIFICE],
    # Taktiklerini sırayla değiştiren dikkatli oyuncu.
    "Çeşitli ve dikkatli": [SAVE_SHED, NET_UNSEEN, HARM_NATURAL, NET_DECOY],
}

SLOT_SCENARIOS = {
    "hepsi akşam": lambda i: "aksam",
    "dilimlere eşit": lambda i: ("aksam", "gece", "gunduz")[i % 3],
}


def simulate(policy, slot_of, time_slot_tactic=True, slot_cap=None, decay_per_op=0, level_cap=None):
    levels = {}
    attention = 0
    found_at = None

    def capped(tactic, level):
        if level_cap is not None:
            level = min(level, level_cap)
        if slot_cap is not None and tactic.startswith("saat_"):
            level = min(level, slot_cap)
        return level

    for i in range(OPERATIONS):
        name, base, tactic, decoy_captured = policy[i % len(policy)]
        used = set()
        if tactic:
            used.add(tactic)
        if time_slot_tactic:
            used.add("saat_" + slot_of(i))

        signature = sum(ATTENTION_PER_LEVEL * levels.get(t, 0) for t in used)
        attention = max(0, attention - decay_per_op)
        attention += base + signature

        for t in set(levels) | used:
            level = levels.get(t, 0)
            level = level + 1 if t in used else max(0, level - 1)
            levels[t] = capped(t, level)
        if decoy_captured:
            levels["kopya"] = capped("kopya", levels.get("kopya", 0) + DECOY_CAPTURE_LEVELS)

        if found_at is None and attention >= 100:
            found_at = i + 1
    return attention, found_at


VARIANTS = [
    ("Eski kurallar (v0.1)", dict()),
    ("Saat dilimi yok, imza en fazla 3", dict(time_slot_tactic=False, level_cap=3)),
    # Oyundaki kurallar: SentinelRules.MaxLevel = 3, WorldState.AttentionDecayPerEvent = 6.
    ("Şu anki kurallar (+ olay başına -6)", dict(time_slot_tactic=False, level_cap=3, decay_per_op=6)),
    ("Karşılaştırma: olay başına -8", dict(time_slot_tactic=False, level_cap=3, decay_per_op=8)),
]


def fmt(result):
    attention, found = result
    return f"{attention:5d} / {'op ' + str(found) if found else '-':>6}"


def main():
    print(f"40 operasyon sonunda toplam dikkat / Sentinel'in seni bulduğu operasyon (100'e ulaşma)\n")
    for slot_name, slot_of in SLOT_SCENARIOS.items():
        print(f"Olay saatleri: {slot_name}")
        header = f"  {'Politika':<38}" + "".join(f"{v[0]:>46}" for v in VARIANTS)
        print(header)
        for policy_name, policy in POLICIES.items():
            row = f"  {policy_name:<38}"
            for _, kwargs in VARIANTS:
                row += f"{fmt(simulate(policy, slot_of, **kwargs)):>46}"
            print(row)
        print()

    print("Duyarlılık (şu anki kurallar): operasyon sayısı değişirse")
    global OPERATIONS
    current = VARIANTS[2][1]
    for ops in (30, 40, 50):
        OPERATIONS = ops
        row = f"  {ops} operasyon:"
        for policy_name, policy in POLICIES.items():
            row += f"  {policy_name.split('(')[-1].rstrip(')')[:22]}: {fmt(simulate(policy, SLOT_SCENARIOS['hepsi akşam'], **current))}"
        print(row)


if __name__ == "__main__":
    main()
