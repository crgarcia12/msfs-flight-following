namespace MSFSFlightFollowing.SimConnect;

using System;

/// <summary>
/// Decoder for the FlyByWire A32NX FMA (Flight Mode Annunciator) integer enums.
/// Values mirror <c>fbw-a32nx/src/systems/shared/src/autopilot.ts</c> in the FBW repo
/// so any change there should be reflected here as well.
/// </summary>
internal static class A32nxFmaModes
{
    // ---- VerticalMode enum (LVar A32NX_FMA_VERTICAL_MODE) ----
    public const int VM_NONE        = 0;
    public const int VM_ALT         = 10;
    public const int VM_ALT_CPT     = 11;
    public const int VM_OP_CLB      = 12;
    public const int VM_OP_DES      = 13;
    public const int VM_VS          = 14;
    public const int VM_FPA         = 15;
    public const int VM_ALT_CST     = 20;
    public const int VM_ALT_CST_CPT = 21;
    public const int VM_CLB         = 22;
    public const int VM_DES         = 23;
    public const int VM_FINAL       = 24;
    public const int VM_GS_CPT      = 30;
    public const int VM_GS_TRACK    = 31;
    public const int VM_LAND        = 32;
    public const int VM_FLARE       = 33;
    public const int VM_ROLL_OUT    = 34;
    public const int VM_SRS         = 40;
    public const int VM_SRS_GA      = 41;
    public const int VM_TCAS        = 50;

    public static string VerticalLabel(int mode) => mode switch
    {
        VM_ALT         => "ALT",
        VM_ALT_CPT     => "ALT*",
        VM_OP_CLB      => "OP CLB",
        VM_OP_DES      => "OP DES",
        VM_VS          => "V/S",
        VM_FPA         => "FPA",
        VM_ALT_CST     => "ALT CST",
        VM_ALT_CST_CPT => "ALT CST*",
        VM_CLB         => "CLB",
        VM_DES         => "DES",
        VM_FINAL       => "FINAL",
        VM_GS_CPT      => "G/S*",
        VM_GS_TRACK    => "G/S",
        VM_LAND        => "LAND",
        VM_FLARE       => "FLARE",
        VM_ROLL_OUT    => "ROLL OUT",
        VM_SRS         => "SRS",
        VM_SRS_GA      => "SRS GA",
        VM_TCAS        => "TCAS",
        _              => "",
    };

    /// <summary>
    /// Vertical-mode label that also recovers <c>ALT CRZ</c> (cruise-altitude hold),
    /// which the FBW / Headwind WASM does NOT expose through the legacy
    /// <c>A32NX_FMA_VERTICAL_MODE</c> integer LVar. Instead it sets bit 26 of
    /// <c>A32NX_FMGC_1_DISCRETE_WORD_1</c> ("dashMode"), which the in-sim PFD reads
    /// directly. See
    /// <c>flybywiresim/aircraft:fbw-a32nx/src/systems/instruments/src/PFD/FMA.tsx</c>
    /// <c>B1Cell.getText()</c>.
    /// </summary>
    public static string VerticalLabel(int mode, double fmgcDiscreteWord1)
    {
        // Bit 26 of FMGC discrete_word_1 = "dashMode" — ALT CRZ.
        // Bit numbering follows ARINC429 / FBW convention (1-based).
        if (Arinc429Bit(fmgcDiscreteWord1, 26)) return "ALT CRZ";
        return VerticalLabel(mode);
    }

    // ---- LateralMode enum (LVar A32NX_FMA_LATERAL_MODE) ----
    public const int LM_NONE      = 0;
    public const int LM_HDG       = 10;
    public const int LM_TRACK     = 11;
    public const int LM_NAV       = 20;
    public const int LM_LOC_CPT   = 30;
    public const int LM_LOC_TRACK = 31;
    public const int LM_LAND      = 32;
    public const int LM_FLARE     = 33;
    public const int LM_ROLL_OUT  = 34;
    public const int LM_RWY       = 40;
    public const int LM_RWY_TRACK = 41;
    public const int LM_GA_TRACK  = 50;

    public static string LateralLabel(int mode) => mode switch
    {
        LM_HDG       => "HDG",
        LM_TRACK     => "TRK",
        LM_NAV       => "NAV",
        LM_LOC_CPT   => "LOC*",
        LM_LOC_TRACK => "LOC",
        LM_LAND      => "LAND",
        LM_FLARE     => "FLARE",
        LM_ROLL_OUT  => "ROLL OUT",
        LM_RWY       => "RWY",
        LM_RWY_TRACK => "RWY TRK",
        LM_GA_TRACK  => "GA TRK",
        _            => "",
    };

    // ---- Armed bitmasks ----
    // ArmedVerticalMode bit positions
    public const int AV_ALT     = 0;
    public const int AV_ALT_CST = 1;
    public const int AV_CLB     = 2;
    public const int AV_DES     = 3;
    public const int AV_GS      = 4;
    public const int AV_FINAL   = 5;
    public const int AV_TCAS    = 6;

    public static string VerticalArmedLabel(int bitmask)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (Bit(bitmask, AV_ALT))     parts.Add("ALT");
        if (Bit(bitmask, AV_ALT_CST)) parts.Add("ALT CST");
        if (Bit(bitmask, AV_CLB))     parts.Add("CLB");
        if (Bit(bitmask, AV_DES))     parts.Add("DES");
        if (Bit(bitmask, AV_GS))      parts.Add("G/S");
        if (Bit(bitmask, AV_FINAL))   parts.Add("FINAL");
        if (Bit(bitmask, AV_TCAS))    parts.Add("TCAS");
        return string.Join(" ", parts);
    }

    // ArmedLateralMode bit positions
    public const int AL_NAV = 0;
    public const int AL_LOC = 1;

    public static string LateralArmedLabel(int bitmask)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (Bit(bitmask, AL_NAV)) parts.Add("NAV");
        if (Bit(bitmask, AL_LOC)) parts.Add("LOC");
        return string.Join(" ", parts);
    }

    /// <summary>True if the FBW A32NX module is producing a real FMA (i.e. powered up).</summary>
    public static bool IsActive(int verticalMode, int lateralMode) =>
        verticalMode != VM_NONE || lateralMode != LM_NONE;

    /// <summary>SRS pitch mode is normal during takeoff and go-around. Outside those phases
    /// (FMGC TAKEOFF = 1, GO_AROUND = 6) it almost always means TOGA was pressed in error.</summary>
    public static bool IsSrs(int verticalMode) =>
        verticalMode == VM_SRS || verticalMode == VM_SRS_GA;

    // ---- AutoThrustMode enum (LVar A32NX_AUTOTHRUST_MODE) ----
    public const int AT_NONE        = 0;
    public const int AT_MAN_TOGA    = 1;
    public const int AT_MAN_GA_SOFT = 2;
    public const int AT_MAN_FLEX    = 3;
    public const int AT_MAN_DTO     = 4;
    public const int AT_MAN_MCT     = 5;
    public const int AT_MAN_THR     = 6;
    public const int AT_SPEED       = 7;
    public const int AT_MACH        = 8;
    public const int AT_THR_MCT     = 9;
    public const int AT_THR_CLB     = 10;
    public const int AT_THR_LVR     = 11;
    public const int AT_THR_IDLE    = 12;
    public const int AT_A_FLOOR     = 13;
    public const int AT_TOGA_LK     = 14;

    /// <summary>Render the FBW A/THR mode as the label that appears in the leftmost
    /// FMA column on the PFD. Returns "" when nothing is engaged.</summary>
    public static string AutothrustLabel(int mode) => mode switch
    {
        AT_MAN_TOGA    => "MAN TOGA",
        AT_MAN_GA_SOFT => "MAN GA",
        AT_MAN_FLEX    => "MAN FLEX",
        AT_MAN_DTO     => "MAN DTO",
        AT_MAN_MCT     => "MAN MCT",
        AT_MAN_THR     => "MAN THR",
        AT_SPEED       => "SPEED",
        AT_MACH        => "MACH",
        AT_THR_MCT     => "THR MCT",
        AT_THR_CLB     => "THR CLB",
        AT_THR_LVR     => "THR LVR",
        AT_THR_IDLE    => "THR IDLE",
        AT_A_FLOOR     => "A.FLOOR",
        AT_TOGA_LK     => "TOGA LK",
        _              => "",
    };

    /// <summary>
    /// A/THR label that decodes the actual mode from the
    /// <c>A32NX_FCU_ATS_FMA_DISCRETE_WORD</c> ARINC429 bits. The legacy integer LVar
    /// <c>A32NX_AUTOTHRUST_MODE</c> in the current FBW / Headwind WASM only maps
    /// A.FLOOR (=13) — every other mode (MACH, SPEED, THR CLB, etc.) returns 0
    /// from the shim. The PFD reads the discrete word directly. Bit numbering
    /// follows ARINC429 / FBW convention (1-based from LSB) and matches
    /// <c>fbw-a32nx/src/systems/instruments/src/PFD/FMA.tsx</c>
    /// <c>getA1A2CellText()</c> (priority order = first matching branch wins).
    /// </summary>
    public static string AutothrustLabel(int mode, double atsFmaDiscreteWord)
    {
        // Match the priority order of FMA.tsx getA1A2CellText() (lines 479-588).
        // We don't have the FCU_ATS_DISCRETE_WORD (which distinguishes MAN MCT vs
        // THR MCT for bit 12, and MAN THR vs THR LVR for bit 15) so we use the
        // A32nxAutothrustStatus integer to infer atActive via the lookup overload.
        if (Arinc429Bit(atsFmaDiscreteWord, 11)) return "MAN TOGA";
        if (Arinc429Bit(atsFmaDiscreteWord, 13)) return "MAN FLX";
        if (Arinc429Bit(atsFmaDiscreteWord, 29)) return "MAN DTO";
        if (Arinc429Bit(atsFmaDiscreteWord, 17)) return "A.FLOOR";
        if (Arinc429Bit(atsFmaDiscreteWord, 18)) return "TOGA LK";
        if (Arinc429Bit(atsFmaDiscreteWord, 19)) return "SPEED";
        if (Arinc429Bit(atsFmaDiscreteWord, 20)) return "MACH";
        if (Arinc429Bit(atsFmaDiscreteWord, 12)) return "THR MCT";
        if (Arinc429Bit(atsFmaDiscreteWord, 14)) return "THR CLB";
        if (Arinc429Bit(atsFmaDiscreteWord, 15)) return "THR LVR";
        if (Arinc429Bit(atsFmaDiscreteWord, 16)) return "THR IDLE";
        // Fall back to the legacy integer mapping for anything else (will be ""
        // unless A.FLOOR was reported there too).
        return AutothrustLabel(mode);
    }

    /// <summary>
    /// Extract bit <paramref name="bit1Based"/> (1-based, FBW convention) from a
    /// 32-bit packed discrete word stored in an FBW ARINC429 FLOAT64 LVar.
    ///
    /// The encoding (from <c>fbw-common/src/systems/shared/src/arinc429.ts</c>
    /// <c>getRawWord()</c> and the matching C++ <c>Arinc429Utils::fromSimVar</c>)
    /// is:
    /// <code>
    ///   rawWord_uint64 = (ssm &lt;&lt; 32) | float32_bit_pattern_of_data_value
    /// </code>
    /// The lower 32 bits are NOT a plain integer — they're the IEEE-754 bit pattern
    /// of a <c>float32</c>. We must reinterpret those bits as a float, convert the
    /// float to an int32 (matching JS <c>ToInt32</c> truncation), and then test the
    /// bit using <c>(asInt32 >> (bit-1)) &amp; 1</c> — exactly what
    /// <c>Arinc429Word.bitValue()</c> does on the JS side.
    /// </summary>
    private static bool Arinc429Bit(double word, int bit1Based)
    {
        if (bit1Based < 1 || bit1Based > 32) return false;
        if (double.IsNaN(word) || double.IsInfinity(word) || word < 0) return false;

        ulong asUlong = (ulong)word;
        uint lower32 = (uint)(asUlong & 0xFFFFFFFFu);

        float asFloat = BitConverter.Int32BitsToSingle(unchecked((int)lower32));
        if (float.IsNaN(asFloat) || float.IsInfinity(asFloat)) return false;

        // JS ToInt32 semantics: truncate toward zero, then mod 2^32. For the
        // small power-of-two values FBW writes here this is exact via (long).
        long truncated = (long)asFloat;
        uint asInt32 = unchecked((uint)truncated);

        return ((asInt32 >> (bit1Based - 1)) & 1u) != 0u;
    }

    /// <summary>True when A/THR is armed (status = 1) but not yet active.</summary>
    public static bool IsAthrArmed(int status) => status == 1;

    /// <summary>True when A/THR is actively driving the thrust levers (status = 2).</summary>
    public static bool IsAthrActive(int status) => status == 2;

    private static bool Bit(int value, int pos) => ((value >> pos) & 1) == 1;
}
