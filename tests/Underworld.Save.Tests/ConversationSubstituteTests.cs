using System;
using Underworld;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// Conversation text substitution follows convert_string (BABL.C): @ then the kind (G global,
/// S stack local, P by-reference parameter, C constant), the type (I integer), the number and an
/// optional index variable that picks element index - 1 of an array; "@@" is a literal @.
/// Only integer substitutions are checked here, as string ones need the game's strings.
/// </summary>
[Collection("UWClassState")]
public class ConversationSubstituteTests : IDisposable
{
    private readonly short[] _origValues = ConversationVM.StackValues;
    private readonly int _origStack = ConversationVM.stack;
    private readonly int _origBp = ConversationVM.basep;

    public ConversationSubstituteTests()
    {
        ConversationVM.StackValues = new short[4096];
        ConversationVM.stack = 1000;
        ConversationVM.basep = 20;
    }

    public void Dispose()
    {
        ConversationVM.StackValues = _origValues;
        ConversationVM.stack = _origStack;
        ConversationVM.basep = _origBp;
    }

    private static void Global(int address, short value) => ConversationVM.StackValues[address] = value;
    private static void Local(int n, short value) => ConversationVM.StackValues[1000 + 20 + n] = value;

    [Fact]
    public void Global_integer_with_constant_index_reads_that_array_element()
    {//@GI10C3 is element 3 of the array at global 10, so mem[12]
        Global(10, 7); Global(11, 8); Global(12, 9);
        Assert.Equal("You have 9 coins.", ConversationVM.TextSubstitute("You have @GI10C3 coins."));
    }

    [Fact]
    public void Index_can_itself_be_a_stack_variable()
    {//@GI10SI2: the index is local 2, here 2, so mem[11]
        Global(10, 7); Global(11, 8);
        Local(2, 2);
        Assert.Equal("8", ConversationVM.TextSubstitute("@GI10SI2"));
    }

    [Fact]
    public void Stack_integer_reads_a_local_and_negative_numbers_are_parameters()
    {
        Local(3, 42);
        Local(-1, 5);
        Assert.Equal("42 and 5", ConversationVM.TextSubstitute("@SI3 and @SI-1"));
    }

    [Fact]
    public void Pointer_integer_reads_through_the_address_held_in_the_parameter()
    {//a parameter passed by reference: local -2 holds the address 300
        Local(-2, 300);
        Global(300, 11); Global(301, 12);
        Assert.Equal("11 12", ConversationVM.TextSubstitute("@PI-2 @PI-2C2"));
    }

    [Fact]
    public void Constant_is_the_number_itself_and_double_at_is_a_literal_at()
    {
        Assert.Equal("15 @ home", ConversationVM.TextSubstitute("@C15 @@ home"));
    }

    [Fact]
    public void A_string_typed_index_does_not_count_and_its_number_stays_in_the_text()
    {//AtIndex returns 0 for a non-integer index without reading its number: mem[10 - 1]
        Global(9, 4);
        Assert.Equal("44", ConversationVM.TextSubstitute("@GI10SS4"));
    }
}
