namespace Starkive;

/// <summary>
/// IAU-approved star names used to give each SSZ file a unique, memorable identity.
/// The name is derived deterministically from the file's token GUID so it is
/// stable and reproducible — the same file always maps to the same star.
/// </summary>
internal static class StarNames
{
    // 200 IAU-approved proper star names (https://www.iau.org/public/themes/naming_stars/)
    private static readonly string[] Names =
    [
        "Acamar", "Achernar", "Achird", "Acrab", "Acrux", "Acubens", "Adara", "Adhara",
        "Adhil", "Ain", "Ainalrami", "Aladfar", "Alathfar", "Albali", "Albireo", "Alchiba",
        "Alcor", "Alcyone", "Aldebaran", "Alderamin", "Aldhanab", "Aldhibah", "Alfirk",
        "Algedi", "Algenib", "Algieba", "Algol", "Algorab", "Alhena", "Alioth", "Alkaid",
        "Alkalurops", "Alkes", "Almaaz", "Almach", "Alnair", "Alnasl", "Alnilam", "Alnitak",
        "Alniyat", "Alphard", "Alphecca", "Alpheratz", "Alrai", "Alrescha", "Alsafi",
        "Alsciaukat", "Alsephina", "Alshain", "Alshat", "Altair", "Altais", "Alterf",
        "Aludra", "Alula", "Alya", "Alzirr", "Ancha", "Angetenar", "Ankaa", "Antares",
        "Arcalis", "Arcturus", "Arkab", "Arneb", "Ascella", "Asellus", "Aspidiske",
        "Asterope", "Athebyne", "Atik", "Atlas", "Atria", "Avior", "Azelfafage", "Azha",
        "Baham", "Baten", "Beid", "Bellatrix", "Betelgeuse", "Botein", "Brachium",
        "Bunda", "Canopus", "Capella", "Caph", "Castor", "Castula", "Cebalrai", "Celeano",
        "Cervantes", "Chalawan", "Chamukuy", "Chara", "Chertan", "Citadelle", "Cor Caroli",
        "Cursa", "Dabih", "Dalim", "Deneb", "Denebola", "Diadem", "Dirah", "Dschubba",
        "Dubhe", "Duhr", "Dziban", "Electra", "Elnath", "Eltanin", "Enif", "Errai",
        "Fafnir", "Fomalhaut", "Fulu", "Furud", "Fuyue", "Gacrux", "Garnet", "Gemma",
        "Giausar", "Gienah", "Ginan", "Gomeisa", "Graffias", "Hadar", "Haedus", "Hamal",
        "Hassaleh", "Helvetios", "Homam", "Iklil", "Imai", "Intercrus", "Izar", "Jabbah",
        "Jishui", "Judah", "Kaffaljidhma", "Kang", "Kappa", "Kaus", "Keid", "Khad",
        "Kitalpha", "Kochab", "Koeia", "Kornephoros", "Kurhah", "Larawag", "Lesath",
        "Libertas", "Liesma", "Lilii", "Lionrock", "Lucilinburhuc", "Lusitania", "Maasym",
        "Maia", "Marfik", "Markab", "Mazkek", "Megrez", "Meissa", "Mekbuda", "Meleph",
        "Menkar", "Menkent", "Menkib", "Merak", "Merga", "Meridiana", "Merope", "Mesarthim",
        "Miaplacidus", "Mimosa", "Minchir", "Minelauva", "Mintaka", "Mira", "Mirach",
        "Miram", "Mirfak", "Mirzam", "Misam", "Mizar", "Mothallah", "Muliphein", "Muphrid",
        "Musica", "Nahn", "Naos", "Nash", "Nashira", "Nekkar", "Nihal", "Nunki", "Nusakan",
        "Okul", "Peacock", "Phact", "Phecda", "Pherkad", "Pleione", "Polaris", "Pollux",
        "Porrima", "Praecipua", "Procyon", "Propus", "Rasalas", "Rasalgethi", "Rasalhague",
        "Rastaban", "Regor", "Regulus", "Revati", "Rigel", "Rigil", "Rotanev", "Ruchbah",
        "Rukbat", "Sabik", "Saclateni", "Sadachbia", "Sadr", "Saiph", "Sargas", "Sarin",
        "Scheat", "Sceptrum", "Segin", "Seginus", "Shaula", "Sheliak", "Sheratan", "Sirius",
        "Situla", "Skat", "Spica", "Sualocin", "Subra", "Suhail", "Sulafat", "Syrma",
        "Tabit", "Taiyangshou", "Talitha", "Tania", "Tarazed", "Taygeta", "Tegmine",
        "Thabit", "Theemin", "Thuban", "Tiaki", "Titawin", "Toliman", "Torcular", "Tureis",
        "Unukalhai", "Vega", "Veritate", "Vindemiatrix", "Wasat", "Wazn", "Wurren",
        "Xamidimura", "Xuange", "Yed", "Yildun", "Zaniah", "Zaurak", "Zavijava",
        "Zhang", "Zibal", "Zosma", "Zubenelgenubi", "Zubenelhakrabi", "Zubeneschamali",
    ];

    /// <summary>
    /// Returns a star name deterministically derived from a file token GUID.
    /// The same token always produces the same name.
    /// </summary>
    internal static string GetForToken(Guid token)
    {
        // Use the first 4 bytes of the token as an index seed
        var bytes = token.ToByteArray();
        uint seed = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
        return Names[seed % (uint)Names.Length];
    }

    /// <summary>Total number of available star names.</summary>
    internal static int Count => Names.Length;
}
