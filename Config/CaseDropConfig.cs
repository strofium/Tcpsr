using StandRiseServer.Models;

namespace StandRiseServer.Config;

public static class CaseDropConfig
{
    public static List<CaseDefinition> GetAllCaseDefinitions()
    {
        return new List<CaseDefinition>
        {
            GetOriginCaseDefinition(),
            GetOriginBoxDefinition(),
            GetFuriousCaseDefinition(),
            GetFuriousBoxDefinition(),
            GetRivalCaseDefinition(),
            GetRivalBoxDefinition(),
            GetFableCaseDefinition(),
            GetFableBoxDefinition(),
        };
    }

    /// <summary>
    /// Origin Case - 17 скинов
    /// </summary>
    private static CaseDefinition GetOriginCaseDefinition()
    {
        return new CaseDefinition
        {
            CaseId = 301,
            DisplayName = "Origin Case",
            Collection = "Origin",
            SkinIds = new List<int>
            {
                // Rare
                15001, 45002, 51002, 62001, 13001,
                // Epic
                32001, 35002,
                // Legendary
                45001, 46002, 47002,
                // Arcane
                11002, 44002, 71001, 71002, 71003, 71004, 71005
            },
            SkinWeights = new List<float>
            {
                12f, 12f, 12f, 12f, 12f,
                8f, 8f,
                4f, 4f, 4f,
                1f, 1f, 1f, 1f, 1f, 1f, 1f
            },
            StatTrackSkinIds = new List<int> { 1015001, 1045002, 1051002, 1013001, 1032001, 1035002, 1045001, 1046002, 1047002, 1011002, 1044002 },
            StatTrackChance = 0.10f
        };
    }

    private static CaseDefinition GetOriginBoxDefinition()
    {
        return new CaseDefinition
        {
            CaseId = 401,
            DisplayName = "Origin Box",
            Collection = "Origin",
            SkinIds = new List<int> { 11002, 44002 },
            SkinWeights = new List<float> { 50f, 50f },
            StatTrackSkinIds = new List<int> { 1011002, 1044002 },
            StatTrackChance = 0.10f
        };
    }

    /// <summary>
    /// Furious Case - 15 скинов
    /// </summary>
    private static CaseDefinition GetFuriousCaseDefinition()
    {
        return new CaseDefinition
        {
            CaseId = 302,
            DisplayName = "Furious Case",
            Collection = "Furious",
            SkinIds = new List<int>
            {
                // Rare
                15002, 48001,
                // Epic
                32003, 44004, 52001, 51003, 13003, 48002,
                // Legendary
                46006, 51004, 32004, 72002, 72006,
                // Arcane
                72004, 72007
            },
            SkinWeights = new List<float>
            {
                20f, 20f,
                6f, 6f, 6f, 6f, 6f, 6f,
                3f, 3f, 3f, 2f, 2f,
                1f, 1f
            },
            StatTrackSkinIds = new List<int> { },
            StatTrackChance = 0.10f
        };
    }

    private static CaseDefinition GetFuriousBoxDefinition()
    {
        return new CaseDefinition
        {
            CaseId = 402,
            DisplayName = "Furious Box",
            Collection = "Furious",
            SkinIds = new List<int> { 15002, 48001 },
            SkinWeights = new List<float> { 50f, 50f },
            StatTrackSkinIds = new List<int> { },
            StatTrackChance = 0.10f
        };
    }

    /// <summary>
    /// Rival Case - 16 скинов
    /// </summary>
    private static CaseDefinition GetRivalCaseDefinition()
    {
        return new CaseDefinition
        {
            CaseId = 303,
            DisplayName = "Rival Case",
            Collection = "Rival",
            SkinIds = new List<int>
            {
                // Rare
                13004, 44006, 48003, 52003,
                // Epic
                15006, 34001, 46007, 11008, 34002, 44005,
                // Legendary
                32005, 51007,
                // Arcane
                73002, 73003, 73004, 73006
            },
            SkinWeights = new List<float>
            {
                14f, 14f, 14f, 14f,
                5f, 5f, 5f, 5f, 5f, 5f,
                3f, 3f,
                1f, 1f, 1f, 1f
            },
            StatTrackSkinIds = new List<int> { },
            StatTrackChance = 0.10f
        };
    }

    private static CaseDefinition GetRivalBoxDefinition()
    {
        return new CaseDefinition
        {
            CaseId = 403,
            DisplayName = "Rival Box",
            Collection = "Rival",
            SkinIds = new List<int> { 73002, 73003 },
            SkinWeights = new List<float> { 50f, 50f },
            StatTrackSkinIds = new List<int> { },
            StatTrackChance = 0.10f
        };
    }

    /// <summary>
    /// Fable Case - 16 скинов
    /// </summary>
    private static CaseDefinition GetFableCaseDefinition()
    {
        return new CaseDefinition
        {
            CaseId = 304,
            DisplayName = "Fable Case",
            Collection = "Fable",
            SkinIds = new List<int>
            {
                // Rare
                41701, 44601,
                // Epic
                41102, 41502, 41703, 45301, 41212, 43202, 44903, 41605, 43402,
                // Legendary
                44603, 47504,
                // Arcane
                47502, 47503, 47505
            },
            SkinWeights = new List<float>
            {
                20f, 20f,
                5f, 5f, 5f, 5f, 5f, 5f, 5f, 5f, 5f,
                3f, 3f,
                1f, 1f, 1f
            },
            StatTrackSkinIds = new List<int> { },
            StatTrackChance = 0.10f
        };
    }

    private static CaseDefinition GetFableBoxDefinition()
    {
        return new CaseDefinition
        {
            CaseId = 404,
            DisplayName = "Fable Box",
            Collection = "Fable",
            SkinIds = new List<int> { 47502, 47503 },
            SkinWeights = new List<float> { 50f, 50f },
            StatTrackSkinIds = new List<int> { },
            StatTrackChance = 0.10f
        };
    }
}
