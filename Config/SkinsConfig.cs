using StandRiseServer.Models;

namespace StandRiseServer.Config;

public static class SkinsConfig
{
    public static List<InventoryItemDefinition> GetAllItems()
    {
        var items = new List<InventoryItemDefinition>();
        items.AddRange(GetCasesAndBoxes());
        items.AddRange(GetMedals());
        items.AddRange(GetStickers());
        items.AddRange(GetGiftsAndPasses());
        items.AddRange(GetOriginSkins());
        items.AddRange(GetFuriousSkins());
        items.AddRange(GetRivalSkins());
        items.AddRange(GetFableSkins());
        items.AddRange(GetAssistanceSkins());
        items.AddRange(GetEvent2YearsSkins());
        items.AddRange(GetNewYear2020Skins());
        items.AddRange(GetOtherSkins());
        return items;
    }

    private static List<InventoryItemDefinition> GetCasesAndBoxes()
    {
        return new List<InventoryItemDefinition>
        {
            CreateCase(301, "Origin Case", "Origin", 100),
            CreateCase(302, "Furious Case", "Furious", 100),
            CreateCase(303, "Rival Case", "Rival", 100),
            CreateCase(304, "Fable Case", "Fable", 100),
            CreateBox(401, "Origin Box", "Origin", 100),
            CreateBox(402, "Furious Box", "Furious", 100),
            CreateBox(403, "Rival Box", "Rival", 100),
            CreateBox(404, "Fable Box", "Fable", 100),
        };
    }

    private static List<InventoryItemDefinition> GetMedals()
    {
        return new List<InventoryItemDefinition>
        {
            // Assistance медали
            CreateMedal(100, "Medal \"Assistance Bronze\"", "Uncommon", "Assistance"),
            CreateMedal(101, "Medal \"Assistance Silver\"", "Rare", "Assistance"),
            CreateMedal(102, "Medal \"Assistance Gold\"", "Epic", "Assistance"),
            CreateMedal(103, "Medal \"Assistance Platinum\"", "Legendary", "Assistance"),
            CreateMedal(104, "Medal \"Assistance Brilliant\"", "Arcane", "Assistance"),
            // Veteran 2018 медали
            CreateMedal(105, "Medal \"Veteran 2018 Bronze\"", "Uncommon", "Veteran2018"),
            CreateMedal(106, "Medal \"Veteran 2018 Silver\"", "Rare", "Veteran2018"),
            CreateMedal(107, "Medal \"Veteran 2018 Gold\"", "Epic", "Veteran2018"),
            CreateMedal(108, "Medal \"Veteran 2018 Platinum\"", "Legendary", "Veteran2018"),
            // Veteran 2019 медали
            CreateMedal(109, "Medal \"Veteran 2019 Bronze\"", "Uncommon", "Veteran2019"),
            CreateMedal(110, "Medal \"Veteran 2019 Silver\"", "Rare", "Veteran2019"),
            CreateMedal(111, "Medal \"Veteran 2019 Gold\"", "Epic", "Veteran2019"),
            CreateMedal(112, "Medal \"Veteran 2019 Platinum\"", "Legendary", "Veteran2019"),
            // 2 Years медали
            CreateMedal(113, "Medal \"2 Years Silver\"", "Rare", "Event2Years"),
            CreateMedal(114, "Medal \"2 Years Gold\"", "Epic", "Event2Years"),
            // Competitive медали
            CreateMedal(115, "Medal \"Competitive Bronze\"", "Uncommon", ""),
            CreateMedal(116, "Medal \"Competitive Silver\"", "Rare", ""),
            CreateMedal(117, "Medal \"Competitive Gold\"", "Epic", ""),
            CreateMedal(118, "Medal \"Competitive Platinum\"", "Legendary", ""),
            CreateMedal(119, "Medal \"Competitive Brilliant\"", "Arcane", ""),
            // New Year 2020 медали
            CreateMedal(120, "Medal \"New Year 2020 Bronze\"", "Uncommon", ""),
            CreateMedal(121, "Medal \"New Year 2020 Silver\"", "Rare", ""),
            CreateMedal(122, "Medal \"New Year 2020 Gold\"", "Epic", ""),
            CreateMedal(123, "Medal \"New Year 2020 Platinum\"", "Legendary", ""),
            CreateMedal(124, "Medal \"New Year 2020 Brilliant\"", "Arcane", ""),
        };
    }

    private static List<InventoryItemDefinition> GetStickers()
    {
        return new List<InventoryItemDefinition>
        {
            // Halloween стикеры
            CreateSticker(1101, "Gold Skull", "Halloween"),
            CreateSticker(1102, "Punisher", "Halloween"),
            CreateSticker(1103, "Mad Bat", "Halloween"),
            CreateSticker(1104, "Infernal Skull", "Halloween"),
            CreateSticker(1105, "Ghoul", "Halloween"),
            CreateSticker(1106, "Batrider", "Halloween"),
            CreateSticker(1107, "Gangsta Pumpkin", "Halloween"),
            CreateSticker(1108, "Snot", "Halloween"),
            CreateSticker(1109, "Devilish", "Halloween"),
            CreateSticker(1110, "Hurry Ghost", "Halloween"),
            CreateSticker(1111, "Feed", "Halloween"),
            CreateSticker(1112, "Anticamper", "Halloween"),
            CreateSticker(1113, "S1001", "Halloween"),
            CreateSticker(1114, "Bloody Clown", "Halloween"),
            CreateSticker(1115, "Ghosty", "Halloween"),
            CreateSticker(1116, "Mummy", "Halloween"),
            CreateSticker(1117, "Rush", "Halloween"),
            CreateSticker(1118, "Evil Pumpkin", "Halloween"),
            CreateSticker(1119, "Zombie", "Halloween"),
            CreateSticker(1120, "Dracula", "Halloween"),
            // NewYear2020 стикеры
            CreateSticker(1121, "Snowflake", "NewYear2020"),
            CreateSticker(1122, "Frost", "NewYear2020"),
            CreateSticker(1123, "Winter Star", "NewYear2020"),
            CreateSticker(1124, "Ice Crystal", "NewYear2020"),
            CreateSticker(1125, "Snowman", "NewYear2020"),
            CreateSticker(1126, "New Year Tree", "NewYear2020"),
            CreateSticker(1127, "Gift Box", "NewYear2020"),
            CreateSticker(1128, "Candy Cane", "NewYear2020"),
            CreateSticker(1129, "Bell", "NewYear2020"),
            CreateSticker(1130, "Firework", "NewYear2020"),
            CreateSticker(1131, "2020", "NewYear2020"),
            CreateSticker(1132, "Champagne", "NewYear2020"),
        };
    }

    private static List<InventoryItemDefinition> GetGiftsAndPasses()
    {
        return new List<InventoryItemDefinition>
        {
            CreateGift(501, "Gift New Year 2019"),
            CreatePass(601, "Two Years Event Gold Pass"),
            CreatePass(602, "New Year Madness 2020 Gold Pass"),
            CreatePack(701, "Halloween 2019 Stickers Pack"),
        };
    }


    /// <summary>
    /// Origin Collection - 17 скинов
    /// </summary>
    private static List<InventoryItemDefinition> GetOriginSkins()
    {
        return new List<InventoryItemDefinition>
        {
            // RARE
            Skin(15001, "Desert Eagle \"Predator\"", "pistol", 5, "Rare", "Origin"),
            Skin(45002, "AKR12 \"Pixel Camouflage\"", "rifle", 45, "Rare", "Origin"),
            Skin(51002, "AWM \"Phoenix\"", "sniper", 11, "Rare", "Origin"),
            Skin(62001, "SM1014 \"Pathfinder\"", "shotgun", 14, "Rare", "Origin"),
            Skin(13001, "P350 \"Neon\"", "pistol", 3, "Rare", "Origin"),
            // EPIC
            Skin(32001, "UMP45 \"Cyberpunk\"", "smg", 6, "Epic", "Origin"),
            Skin(35002, "P90 \"Ghoul\"", "smg", 35, "Epic", "Origin"),
            // LEGENDARY
            Skin(45001, "AKR12 \"Railgun\"", "rifle", 45, "Legendary", "Origin"),
            Skin(46002, "M4 \"Necromancer\"", "rifle", 9, "Legendary", "Origin"),
            Skin(47002, "M16 \"Winged\"", "rifle", 10, "Legendary", "Origin"),
            // ARCANE
            Skin(11002, "G22 \"Nest\"", "pistol", 1, "Arcane", "Origin"),
            Skin(44002, "AKR \"Treasure Hunter\"", "rifle", 8, "Arcane", "Origin"),
            Skin(71002, "M9 Bayonet \"Ancient\"", "knife", 71, "Arcane", "Origin"),
            Skin(71003, "M9 Bayonet \"Scratch\"", "knife", 71, "Arcane", "Origin"),
            Skin(71004, "M9 Bayonet \"Universe\"", "knife", 71, "Arcane", "Origin"),
            Skin(71005, "M9 Bayonet \"Dragon Glass\"", "knife", 71, "Arcane", "Origin"),
        };
    }

    /// <summary>
    /// Furious Collection - 15 скинов
    /// </summary>
    private static List<InventoryItemDefinition> GetFuriousSkins()
    {
        return new List<InventoryItemDefinition>
        {
            // RARE
            Skin(15002, "Desert Eagle \"Red\"", "pistol", 5, "Rare", "Furious"),
            Skin(48001, "FAMAS \"Beagle\"", "rifle", 48, "Epic", "Furious"),
            // EPIC
            Skin(32003, "UMP45 \"Shark\"", "smg", 6, "Rare", "Furious"),
            Skin(44004, "AKR \"Sport\"", "rifle", 8, "Epic", "Furious"),
            Skin(52001, "M40 \"Quake\"", "sniper", 12, "Epic", "Furious"),
            Skin(51003, "AWM \"Gear\"", "sniper", 11, "Legendary", "Furious"),
            Skin(13003, "P350 \"Forest Spirit\"", "pistol", 3, "Arcane", "Furious"),
            Skin(48002, "FAMAS \"Fury\"", "rifle", 48, "Arcane", "Furious"),
            // LEGENDARY
            Skin(46006, "M4 \"Pro\"", "rifle", 9, "Rare", "Furious"),
            Skin(51004, "AWM \"Scratch\"", "sniper", 11, "Rare", "Furious"),
            Skin(32004, "UMP45 \"Winged\"", "smg", 6, "Legendary", "Furious"),
            Skin(72002, "Karambit \"Claw\"", "knife", 72, "Arcane", "Furious"),
            Skin(72006, "Karambit \"Scratch\"", "knife", 72, "Arcane", "Furious"),
            // ARCANE
            Skin(72004, "Karambit \"Dragon\"", "knife", 72, "Arcane", "Furious"),
            Skin(72007, "Karambit \"Universe\"", "knife", 72, "Arcane", "Furious"),
        };
    }

    /// <summary>
    /// Rival Collection - 16 скинов
    /// </summary>
    private static List<InventoryItemDefinition> GetRivalSkins()
    {
        return new List<InventoryItemDefinition>
        {
            // RARE
            Skin(13004, "P350 \"Rally\"", "pistol", 3, "Rare", "Rival"),
            Skin(44006, "AKR \"Carbon\"", "rifle", 8, "Rare", "Rival"),
            Skin(48003, "FAMAS \"Hull\"", "rifle", 48, "Rare", "Rival"),
            Skin(52003, "M40 \"Beagle\"", "sniper", 12, "Rare", "Rival"),
            // EPIC
            Skin(15006, "Desert Eagle \"Dragon Glass\"", "pistol", 5, "Epic", "Rival"),
            Skin(34001, "MP7 \"Offroad\"", "smg", 7, "Epic", "Rival"),
            Skin(46007, "M4 \"Grand Prix\"", "rifle", 9, "Epic", "Rival"),
            Skin(11008, "G22 \"Frost Wyrm\"", "pistol", 1, "Legendary", "Rival"),
            Skin(34002, "MP7 \"Arcade\"", "smg", 7, "Legendary", "Rival"),
            Skin(44005, "AKR \"Necromancer\"", "rifle", 8, "Legendary", "Rival"),
            // LEGENDARY
            Skin(32005, "UMP45 \"Beast\"", "smg", 6, "Arcane", "Rival"),
            Skin(51007, "AWM \"Genesis\"", "sniper", 11, "Arcane", "Rival"),
            // ARCANE
            Skin(73002, "jKommando \"Ancient\"", "knife", 73, "Arcane", "Rival"),
            Skin(73003, "jKommando \"Reaper\"", "knife", 73, "Arcane", "Rival"),
            Skin(73004, "jKommando \"Floral\"", "knife", 73, "Arcane", "Rival"),
            Skin(73006, "jKommando \"Luxury\"", "knife", 73, "Arcane", "Rival"),
        };
    }


    /// <summary>
    /// Fable Collection - 16 скинов
    /// </summary>
    private static List<InventoryItemDefinition> GetFableSkins()
    {
        return new List<InventoryItemDefinition>
        {
            // RARE
            Skin(41102, "G22 \"Starfall\"", "pistol", 1, "Rare", "Fable"),
            Skin(41502, "Desert Eagle \"Ace\"", "pistol", 5, "Rare", "Fable"),
            Skin(41703, "Five Seven \"Tactical\"", "pistol", 17, "Rare", "Fable"),
            Skin(45301, "M110 \"Cyber\"", "sniper", 53, "Rare", "Fable"),
            // EPIC
            Skin(41212, "USP \"Pisces\"", "pistol", 2, "Epic", "Fable"),
            Skin(43202, "UMP45 \"Cerberus\"", "smg", 6, "Epic", "Fable"),
            Skin(44903, "FnFAL \"Tactical\"", "rifle", 49, "Epic", "Fable"),
            Skin(41605, "Tec9 \"Fable\"", "pistol", 16, "Legendary", "Fable"),
            Skin(43402, "MP7 \"Lich\"", "smg", 7, "Legendary", "Fable"),
            Skin(44601, "M4 \"Lizard\"", "rifle", 9, "Legendary", "Fable"),
            // LEGENDARY
            Skin(41701, "Five Seven \"Venom\"", "pistol", 17, "Arcane", "Fable"),
            Skin(44603, "M4 \"Samurai\"", "rifle", 9, "Arcane", "Fable"),
            Skin(47504, "Butterfly \"Black Widow \"", "knife", 75, "Arcane", "Fable"),
            // ARCANE
            Skin(47502, "Butterfly \"Legacy\"", "knife", 75, "Arcane", "Fable"),
            Skin(47503, "Butterfly \"Dragon Glass\"", "knife", 75, "Arcane", "Fable"),
            Skin(47505, "Butterfly \"Starfall\"", "knife", 75, "Arcane", "Fable"),
        };
    }

    /// <summary>
    /// Assistance Collection - скины для медалей Assistance
    /// </summary>
    private static List<InventoryItemDefinition> GetAssistanceSkins()
    {
        return new List<InventoryItemDefinition>
        {
            Skin(15003, "Desert Eagle \"Morgan\"", "pistol", 5, "Rare", "Assistance"),
            Skin(46001, "M4 \"Predator\"", "rifle", 9, "Epic", "Assistance"),
            Skin(51001, "AWM \"Sport\"", "sniper", 11, "Legendary", "Assistance"),
            Skin(71001, "M9 Bayonet \"Blue Blood\"", "knife", 71, "Arcane", "Assistance"),
        };
    }

    /// <summary>
    /// Event2Years Collection - скины 2 Years (без кейса)
    /// </summary>
    private static List<InventoryItemDefinition> GetEvent2YearsSkins()
    {
        return new List<InventoryItemDefinition>
        {
            Skin(12002, "USP \"2Years\"", "pistol", 2, "Epic", "Event2Years"),
            Skin(12003, "USP \"2Years Red\"", "pistol", 2, "Epic", "Event2Years"),
            Skin(34003, "MP7 \"2Years\"", "smg", 7, "Epic", "Event2Years"),
            Skin(34004, "MP7 \"2Years Red\"", "smg", 7, "Epic", "Event2Years"),
            Skin(44007, "AKR \"2Years\"", "rifle", 8, "Epic", "Event2Years"),
            Skin(51008, "AWM \"2Years Red\"", "sniper", 11, "Epic", "Event2Years"),
        };
    }

    /// <summary>
    /// New Year 2020 Collection - скины из InventoryId
    /// </summary>
    private static List<InventoryItemDefinition> GetNewYear2020Skins()
    {
        return new List<InventoryItemDefinition>
        {
            // Используем ID из InventoryId enum
            // Rare
            Skin(65201, "M40 \"Arctic\"", "sniper", 12, "Rare", "NewYear2020"),
            Skin(66201, "SM1014 \"Arctic\"", "shotgun", 14, "Rare", "NewYear2020"),
            Skin(63401, "MP7 \"Winter Sport\"", "smg", 7, "Rare", "NewYear2020"),
            // Epic
            Skin(65202, "M40 \"Winter Track\"", "sniper", 12, "Epic", "NewYear2020"),
            Skin(61201, "USP \"Stone Cold\"", "pistol", 2, "Epic", "NewYear2020"),
            // Legendary
            Skin(61601, "TEC9 \"Necromancer\"", "pistol", 16, "Legendary", "NewYear2020"),
            Skin(65101, "AWM \"Winter Sport\"", "sniper", 11, "Legendary", "NewYear2020"),
            // Arcane
            Skin(61101, "G22 \"Frozen\"", "pistol", 1, "Arcane", "NewYear2020"),
            Skin(67701, "Flip Knife \"Dragon Glass\"", "knife", 77, "Arcane", "NewYear2020"),
            Skin(67702, "Flip Knife \"Arctic\"", "knife", 77, "Arcane", "NewYear2020"),
            Skin(67703, "Flip Knife \"Stone Cold\"", "knife", 77, "Arcane", "NewYear2020"),
            Skin(67704, "Flip Knife \"Vortex\"", "knife", 77, "Arcane", "NewYear2020"),
            Skin(67705, "Flip Knife \"Frozen\"", "knife", 77, "Arcane", "NewYear2020"),
        };
    }

    /// <summary>
    /// Остальные скины - Competitive, Nameless и StatTrack версии
    /// </summary>
    private static List<InventoryItemDefinition> GetOtherSkins()
    {
        return new List<InventoryItemDefinition>
        {
            // Competitive collection скины (все Arcane)
            Skin(57501, "Butterfly \"Fire Storm\"", "knife", 75, "Arcane", "Competitive"),
            Skin(54601, "M4 \"Pixel Storm\"", "rifle", 9, "Arcane", "Competitive"),
            Skin(54401, "AKR \"Fabric Storm\"", "rifle", 8, "Arcane", "Competitive"),
            Skin(51601, "TEC-9 \"Splinter Storm\"", "pistol", 16, "Arcane", "Competitive"),
            Skin(51701, "Five Seven \"Camo Storm\"", "pistol", 17, "Arcane", "Competitive"),
            // Nameless collection скины (все Arcane)
            Skin(41101, "G22 \"Relic\"", "pistol", 1, "Arcane", "Nameless"),
            Skin(44901, "FN FAL \"Leather\"", "rifle", 49, "Arcane", "Nameless"),
            Skin(12001, "USP \"Genesis\"", "pistol", 2, "Arcane", "Nameless"),
            Skin(51006, "AWM \"Treasure Hunter\"", "sniper", 11, "Arcane", "Nameless"),
            Skin(72003, "Karambit \"Gold\"", "knife", 72, "Arcane", "Nameless"),
            // StatTrack версии для Origin
            SkinST(1015003, "Desert Eagle \"Predator\"", "pistol", 5, "Rare", "Origin"),
            SkinST(1045002, "AKR12 \"Pixel\"", "rifle", 45, "Rare", "Origin"),
            SkinST(1062002, "SM1014 \"Pathfinder\"", "shotgun", 14, "Rare", "Origin"),
            SkinST(1051002, "AWM \"Phoenix\"", "sniper", 11, "Rare", "Origin"),
            SkinST(1013001, "P350 \"Neon\"", "pistol", 3, "Rare", "Origin"),
            SkinST(1032001, "UMP45 \"Cyberpunk\"", "smg", 6, "Epic", "Origin"),
            SkinST(1035002, "P90 \"Ghoul\"", "smg", 35, "Epic", "Origin"),
            SkinST(1045001, "AKR12 \"Railgun\"", "rifle", 45, "Legendary", "Origin"),
            SkinST(1046002, "M4 \"Necromancer\"", "rifle", 9, "Legendary", "Origin"),
            SkinST(1047002, "M16 \"Winged\"", "rifle", 10, "Legendary", "Origin"),
            SkinST(1011002, "G22 \"Nest\"", "pistol", 1, "Arcane", "Origin"),
            SkinST(1044002, "AKR \"Treasure Hunter\"", "rifle", 8, "Arcane", "Origin"),
            // StatTrack версии для Furious
            SkinST(1013003, "P350 \"Forest Spirit\"", "pistol", 3, "Arcane", "Furious"),
            SkinST(1032003, "UMP45 \"Shark\"", "smg", 6, "Rare", "Furious"),
            SkinST(1032004, "UMP45 \"Winged\"", "smg", 6, "Legendary", "Furious"),
            SkinST(1044004, "AKR \"Sport\"", "rifle", 8, "Epic", "Furious"),
            SkinST(1046006, "M4 \"Pro\"", "rifle", 9, "Rare", "Furious"),
            SkinST(1048001, "FAMAS \"Beagle\"", "rifle", 48, "Epic", "Furious"),
            SkinST(1048002, "FAMAS \"Fury\"", "rifle", 48, "Arcane", "Furious"),
            SkinST(1051003, "AWM \"Gear\"", "sniper", 11, "Legendary", "Furious"),
            SkinST(1051004, "AWM \"Scratch\"", "sniper", 11, "Rare", "Furious"),
            SkinST(1052001, "M40 \"Quake\"", "sniper", 12, "Epic", "Furious"),
            // StatTrack версии для Rival
            SkinST(1011008, "G22 \"Frost Wyrm\"", "pistol", 1, "Legendary", "Rival"),
            SkinST(1013004, "P350 \"Rally\"", "pistol", 3, "Rare", "Rival"),
            SkinST(1032005, "UMP45 \"Beast\"", "smg", 6, "Arcane", "Rival"),
            SkinST(1034001, "MP7 \"Offroad\"", "smg", 7, "Epic", "Rival"),
            SkinST(1034002, "MP7 \"Arcade\"", "smg", 7, "Legendary", "Rival"),
            SkinST(1015006, "Desert Eagle \"Dragon Glass\"", "pistol", 5, "Epic", "Rival"),
            SkinST(1044006, "AKR \"Carbon\"", "rifle", 8, "Rare", "Rival"),
            SkinST(1044005, "AKR \"Necromancer\"", "rifle", 8, "Legendary", "Rival"),
            SkinST(1046007, "M4 \"Grand Prix\"", "rifle", 9, "Epic", "Rival"),
            SkinST(1048003, "FAMAS \"Hull\"", "rifle", 48, "Rare", "Rival"),
            SkinST(1051007, "AWM \"Genesis\"", "sniper", 11, "Arcane", "Rival"),
            SkinST(1052003, "M40 \"Beagle\"", "sniper", 12, "Rare", "Rival"),
            // StatTrack версии для Fable
            SkinST(1041102, "G22 \"Starfall\"", "pistol", 1, "Rare", "Fable"),
            SkinST(1041502, "Desert Eagle \"Ace\"", "pistol", 5, "Rare", "Fable"),
            SkinST(1041701, "Five Seven \"Venom\"", "pistol", 17, "Arcane", "Fable"),
            SkinST(1041703, "Five Seven \"Tactical\"", "pistol", 17, "Rare", "Fable"),
            SkinST(1044903, "FnFAL \"Tactical\"", "rifle", 49, "Epic", "Fable"),
            SkinST(1044601, "M4 \"Lizard\"", "rifle", 9, "Legendary", "Fable"),
            SkinST(1044603, "M4 \"Samurai\"", "rifle", 9, "Arcane", "Fable"),
            SkinST(1045301, "M110 \"Cyber\"", "sniper", 53, "Rare", "Fable"),
            SkinST(1043402, "MP7 \"Lich\"", "smg", 7, "Legendary", "Fable"),
            SkinST(1041605, "Tec9 \"Fable\"", "pistol", 16, "Legendary", "Fable"),
            SkinST(1043202, "UMP45 \"Cerberus\"", "smg", 6, "Epic", "Fable"),
            SkinST(1041212, "USP \"Pisces\"", "pistol", 2, "Epic", "Fable"),
            // StatTrack версии для NewYear2020
            SkinST(1065101, "AWM \"Winter Sport\"", "sniper", 11, "Legendary", "NewYear2020"),
            SkinST(1061101, "G22 \"Frozen\"", "pistol", 1, "Arcane", "NewYear2020"),
            SkinST(1065201, "M40 \"Arctic\"", "sniper", 12, "Rare", "NewYear2020"),
            SkinST(1065202, "M40 \"Winter Track\"", "sniper", 12, "Epic", "NewYear2020"),
            SkinST(1063401, "MP7 \"Winter Sport\"", "smg", 7, "Rare", "NewYear2020"),
            SkinST(1066201, "SM1014 \"Arctic\"", "shotgun", 14, "Rare", "NewYear2020"),
            SkinST(1061601, "TEC9 \"Necromancer\"", "pistol", 16, "Legendary", "NewYear2020"),
            SkinST(1061201, "USP \"Stone Cold\"", "pistol", 2, "Epic", "NewYear2020"),
        };
    }


    // ========== HELPERS ==========
    private static InventoryItemDefinition CreateCase(int id, string name, string col, int gold) => new()
    {
        ItemId = id, DisplayName = name, Category = "case", Rarity = "Rare", Collection = col,
        BuyPrice = new() { new() { CurrencyId = 102, Value = gold } },
        SellPrice = new() { new() { CurrencyId = 101, Value = gold * 50 } },
        Properties = new() { { "type", "case" }, { "collection", col } }
    };

    private static InventoryItemDefinition CreateBox(int id, string name, string col, int silver) => new()
    {
        ItemId = id, DisplayName = name, Category = "box", Rarity = "Rare", Collection = col,
        BuyPrice = new() { new() { CurrencyId = 101, Value = silver } },
        SellPrice = new() { new() { CurrencyId = 101, Value = silver / 2 } },
        Properties = new() { { "type", "box" }, { "collection", col } }
    };

    private static InventoryItemDefinition CreateMedal(int id, string name, string rarity, string col) => new()
    {
        ItemId = id, DisplayName = name, Category = "medal", Rarity = rarity, Collection = col,
        BuyPrice = new(), SellPrice = new(),
        Properties = string.IsNullOrEmpty(col) ? new() { { "type", "medal" } } : new() { { "type", "medal" }, { "collection", col } }
    };

    private static InventoryItemDefinition CreateSticker(int id, string name, string collection = "") => new()
    {
        ItemId = id, DisplayName = name, Category = "sticker", Rarity = "Arcane", Collection = collection,
        BuyPrice = new(), SellPrice = new(), 
        Properties = string.IsNullOrEmpty(collection) 
            ? new() { { "type", "sticker" } } 
            : new() { { "type", "sticker" }, { "collection", collection } }
    };

    private static InventoryItemDefinition CreateGift(int id, string name) => new()
    {
        ItemId = id, DisplayName = name, Category = "gift", Rarity = "Rare",
        BuyPrice = new(), SellPrice = new(), Properties = new() { { "type", "gift" } }
    };

    private static InventoryItemDefinition CreatePass(int id, string name) => new()
    {
        ItemId = id, DisplayName = name, Category = "pass", Rarity = "Legendary",
        BuyPrice = new(), SellPrice = new(), Properties = new() { { "type", "pass" } }
    };

    private static InventoryItemDefinition CreatePack(int id, string name) => new()
    {
        ItemId = id, DisplayName = name, Category = "pack", Rarity = "Rare",
        BuyPrice = new() { new() { CurrencyId = 102, Value = 50 } },
        SellPrice = new() { new() { CurrencyId = 101, Value = 25 } },
        Properties = new() { { "type", "pack" } }
    };

    private static InventoryItemDefinition Skin(int id, string name, string sub, int wid, string rarity, string col) => new()
    {
        ItemId = id, DisplayName = name, Category = "weapon", Subcategory = sub,
        Rarity = rarity, WeaponId = wid, Collection = col,
        BuyPrice = new(), SellPrice = new(),
        Properties = new() { { "type", "weapon" }, { "value", GetVal(rarity).ToString() }, { "collection", col } }
    };

    private static InventoryItemDefinition SkinST(int id, string name, string sub, int wid, string rarity, string col) => new()
    {
        ItemId = id, DisplayName = name, Category = "weapon", Subcategory = sub,
        Rarity = rarity, WeaponId = wid, Collection = col,
        BuyPrice = new(), SellPrice = new(),
        Properties = new() { { "type", "weapon" }, { "value", GetVal(rarity).ToString() }, { "collection", col }, { "stattrack", "true" } }
    };

    private static int GetVal(string r) => r switch { "Common" => 1, "Uncommon" => 2, "Rare" => 3, "Epic" => 4, "Legendary" => 5, "Arcane" => 6, _ => 1 };
}
