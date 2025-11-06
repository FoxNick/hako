namespace HakoJS.Host;

[Flags]
public enum StripFlags
{
    None = 0,
    Source = 1 << 0,
    Debug = 1 << 1,
    All = Source | Debug
}

public class StripOptions
{
    private StripFlags Flags { get; set; } = StripFlags.None;

    public bool StripSource
    {
        get => Flags.HasFlag(StripFlags.Source);
        set
        {
            if (value)
            {
                Flags |= StripFlags.Source;
            }
            else
            {
                // If disabling source, must also disable debug (since debug implies source)
                Flags &= ~(StripFlags.Source | StripFlags.Debug);
            }
        }
    }

    public bool StripDebug
    {
        get => Flags.HasFlag(StripFlags.Debug);
        set
        {
            if (value)
            {
                // Debug stripping implies source stripping
                Flags |= StripFlags.Debug | StripFlags.Source;
            }
            else
            {
                Flags &= ~StripFlags.Debug;
            }
        }
    }

    internal int ToNativeFlags()
    {
        // If Debug is set, return 2 (which implies source on native side)
        // Don't set both bits since JS_STRIP_DEBUG already includes source
        if (Flags.HasFlag(StripFlags.Debug))
            return 2;
        if (Flags.HasFlag(StripFlags.Source))
            return 1;
        return 0;
    }

    internal static StripOptions FromNativeFlags(int flags)
    {
        var options = new StripOptions { Flags = StripFlags.None };

        // If debug flag is set, it implies source stripping too
        if ((flags & 2) != 0)
            options.Flags = StripFlags.Debug | StripFlags.Source;
        else if ((flags & 1) != 0)
            options.Flags = StripFlags.Source;

        return options;
    }
}