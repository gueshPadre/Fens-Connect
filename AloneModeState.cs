namespace FENS_Connect;

public static class AloneModeState
{
    private const string PreferenceKey = "is_alone";

    public const string WidgetToggleAction = "com.fenssafety.fensconnect.ALONE_WIDGET_TOGGLE";

    public static event EventHandler<bool>? Changed;

    public static bool IsAlone => Preferences.Default.Get(PreferenceKey, false);

    public static bool Toggle()
    {
        var isAlone = !IsAlone;
        Set(isAlone);
        return isAlone;
    }

    public static void Set(bool isAlone)
    {
        Preferences.Default.Set(PreferenceKey, isAlone);
        Changed?.Invoke(null, isAlone);
    }
}
