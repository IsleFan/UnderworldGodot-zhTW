using System;
using System.IO;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// LOOK.C's description line on UW1's own strings and COMOBJ.DAT: the article comes from the first word before the
/// name (none after a count, none for quality type 13), and with no such word from the name's own ("a_", "an_",
/// "the_", or none at all). The prefix 'You see ' is left out (IncludeYouSee false), so only the composition is tested.
/// </summary>
[Collection("UWClassState")]
public class LookDescriptionTests : IDisposable
{
    private readonly string _origBasePath = UWClass.BasePath;
    private readonly byte _origRes = UWClass._RES;

    public LookDescriptionTests()
    {
        UWClass.BasePath = Path.Combine(TestData.UW2GogRoot, "UW1");
        UWClass._RES = UWClass.GAME_UW1;
        GameStrings.LoadStringsPak(Path.Combine(UWClass.BasePath, "DATA", "STRINGS.PAK"));
        Loader.ReadStreamFile(Path.Combine(UWClass.BasePath, "DATA", "COMOBJ.DAT"), out var comobj);
        typeof(commonObjDat).GetField("buffer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .SetValue(null, comobj);
    }

    public void Dispose()
    {
        UWClass.BasePath = _origBasePath;
        UWClass._RES = _origRes;
    }

    private static uwObject Item(int item_id, int quality, int link = 0, bool quantity = false)
    {
        var obj = new uwObject { IsStatic = true, index = 1, PTR = 0, DataBuffer = new byte[8] };
        obj.item_id = item_id;
        obj.quality = (short)quality;
        obj.is_quant = (short)(quantity ? 1 : 0);
        obj.link = (short)link;
        obj.owner = 0;
        return obj;
    }

    private static string Describe(uwObject obj) =>
        look.GetDescriptionString(obj, new[] { null, obj }, lorecheckresult: 1, IncludeYouSee: false);

    private static string Article(string word) => "aeiou".IndexOf(word[0]) >= 0 ? "an " : "a ";

    [Theory]
    [InlineData("a_sword&swords", true, false, "a sword")]
    [InlineData("a_sword&swords", false, false, "sword")]
    [InlineData("a_sword&swords", true, true, "swords")]
    [InlineData("a_shield", true, true, "a shields")]   // no '&': an "s" goes on the end, the article stays
    [InlineData("the_Key of Truth", true, false, "the Key of Truth")]
    [InlineData("acid", true, false, "acid")]
    public void FixNameString_FollowsTheOriginal(string name, bool article, bool plural, string expected)
    {
        Assert.Equal(expected, GameStrings.FixNameString(name, article, plural));
    }

    [Fact]
    public void QualityType13_TakesNoArticle()
    {
        var boots = Item(0x2A, quality: 40);   // chain boots, quality type 13
        Assert.Equal(13, commonObjDat.qualitytype(0x2A));
        var quality = GameStrings.GetString(5, 13 * 6 + (40 >> 4) + 1);
        Assert.NotEqual("", quality);

        Assert.Equal($"{quality} chain boots", Describe(boots));
    }

    [Fact]
    public void WithNoWordBefore_TheNamesOwnArticleIsUsed()
    {
        // quality type 15 has no quality words, and these are indestructible (quality class 3, the fifth word)
        Assert.Equal("", GameStrings.GetString(5, 15 * 6 + 5));

        Assert.Equal("the Key of Truth", Describe(Item(0xE1, quality: 40)));
        Assert.Equal("acid", Describe(Item(0x16, quality: 40)));
    }

    [Fact]
    public void ACount_ReplacesTheArticle_AndThePluralIsUsed()
    {
        Assert.Equal("5 sling stones", Describe(Item(0x10, quality: 40, link: 5, quantity: true)));
    }

    [Fact]
    public void AQualityWord_GivesTheArticleByItsFirstLetter()
    {
        var qt = commonObjDat.qualitytype(0x2);   // an_axe
        var quality = GameStrings.GetString(5, qt * 6 + (40 >> 4) + 1);
        Assert.NotEqual("", quality);

        Assert.Equal($"{Article(quality)}{quality} axe", Describe(Item(0x2, quality: 40)));
    }

    /// <summary>
    /// GetObjDesc, for the enchanting and mending messages: never an article, the plural for a stack.
    /// </summary>
    [Fact]
    public void GetObjDesc_HasNoArticle_AndUsesThePluralForAStack()
    {
        var qt = commonObjDat.qualitytype(0x2);
        var quality = GameStrings.GetString(5, qt * 6 + (40 >> 4) + 1);

        Assert.Equal($"{quality} axe", look.GetObjDesc(Item(0x2, quality: 40)));
        Assert.Equal("sling stones", look.GetObjDesc(Item(0x10, quality: 40, link: 5, quantity: true)));
    }
}
