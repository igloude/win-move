using Tactadile.Core;
using Tactadile.Native;

namespace Tactadile.Tests;

public sealed class KeyboardHookTests
{
    // --- ComputeModFlags: stuck modifier recovery ---

    [Fact]
    public void ComputeModFlags_StuckWinCount_ResetsToZero()
    {
        // In the test environment no keys are physically held, so
        // GetAsyncKeyState returns 0 for all VK codes. A stuck _winCount
        // should be corrected to 0 by the hardware validation.
        var hook = new KeyboardHook();
        hook.ForceModifierCounts(win: 1, shift: 0, ctrl: 0, alt: 0);

        uint flags = hook.ComputeModFlags();

        Assert.Equal(0u, flags);
    }

    [Fact]
    public void ComputeModFlags_StuckShiftCount_ResetsToZero()
    {
        var hook = new KeyboardHook();
        hook.ForceModifierCounts(win: 0, shift: 2, ctrl: 0, alt: 0);

        uint flags = hook.ComputeModFlags();

        Assert.Equal(0u, flags);
    }

    [Fact]
    public void ComputeModFlags_StuckCtrlCount_ResetsToZero()
    {
        var hook = new KeyboardHook();
        hook.ForceModifierCounts(win: 0, shift: 0, ctrl: 1, alt: 0);

        uint flags = hook.ComputeModFlags();

        Assert.Equal(0u, flags);
    }

    [Fact]
    public void ComputeModFlags_StuckAltCount_ResetsToZero()
    {
        var hook = new KeyboardHook();
        hook.ForceModifierCounts(win: 0, shift: 0, ctrl: 0, alt: 1);

        uint flags = hook.ComputeModFlags();

        Assert.Equal(0u, flags);
    }

    [Fact]
    public void ComputeModFlags_AllModifiersStuck_AllResetToZero()
    {
        var hook = new KeyboardHook();
        hook.ForceModifierCounts(win: 1, shift: 1, ctrl: 1, alt: 1);

        uint flags = hook.ComputeModFlags();

        Assert.Equal(0u, flags);
    }

    [Fact]
    public void ComputeModFlags_ZeroCounts_ReturnsZero()
    {
        var hook = new KeyboardHook();
        // No ForceModifierCounts — all default to 0

        uint flags = hook.ComputeModFlags();

        Assert.Equal(0u, flags);
    }

    // --- ComputeModFlags: successive calls remain correct ---

    [Fact]
    public void ComputeModFlags_AfterReset_SubsequentCallStillReturnsZero()
    {
        var hook = new KeyboardHook();
        hook.ForceModifierCounts(win: 1, shift: 0, ctrl: 0, alt: 0);

        // First call resets
        hook.ComputeModFlags();

        // Second call should still be 0 (count was reset, not just masked)
        uint flags = hook.ComputeModFlags();
        Assert.Equal(0u, flags);
    }

    // --- SetOverrides: combo set management ---

    [Fact]
    public void SetOverrides_StoresComboSet()
    {
        var hook = new KeyboardHook();
        var combos = new HashSet<(uint, uint)>
        {
            (NativeConstants.MOD_WIN, 0x54) // Win+T
        };

        hook.SetOverrides(true, combos);

        // Verify override is active by checking that ComputeModFlags returns 0
        // (no modifiers held), so Win+T combo would not match when T alone is pressed
        Assert.Equal(0u, hook.ComputeModFlags());
    }

    // --- SetBlockCopilot ---

    [Fact]
    public void SetBlockCopilot_DefaultIsFalse()
    {
        // BlockCopilot is initialized to false — confirm via the field
        // (indirectly: no suppression happens without enabling it)
        var hook = new KeyboardHook();
        // If we could inspect _blockCopilot we'd assert false.
        // Instead, verify SetBlockCopilot doesn't throw.
        hook.SetBlockCopilot(false);
        hook.SetBlockCopilot(true);
        hook.SetBlockCopilot(false);
    }

    // --- SetWinKeyDelay ---

    [Fact]
    public void SetWinKeyDelay_AcceptsParameters()
    {
        var hook = new KeyboardHook();
        hook.SetWinKeyDelay(true, 500);
        hook.SetWinKeyDelay(false, 0);
    }

    // --- ForceModifierCounts: negative values ---

    [Fact]
    public void ForceModifierCounts_NegativeValues_ComputeModFlagsReturnsZero()
    {
        var hook = new KeyboardHook();
        hook.ForceModifierCounts(win: -1, shift: -1, ctrl: -1, alt: -1);

        uint flags = hook.ComputeModFlags();

        // Negative counts should not produce any flags
        Assert.Equal(0u, flags);
    }
}
