namespace Xwing.Web.Theme;
public sealed class ThemePreferenceState
{
    public ThemePreference Preference { get; private set; } = ThemePreference.System;
    private Func<ThemePreference, Task>? setPreferenceAsync;
    public void Configure(ThemePreference initial, Func<ThemePreference, Task> onSetPreferenceAsync)
    {
        Preference = initial;
        setPreferenceAsync = onSetPreferenceAsync;
    }

    public Task SetPreferenceAsync(ThemePreference preference)
    {
        if (setPreferenceAsync is null)
        {
            return Task.CompletedTask;
        }
        return setPreferenceAsync(preference);
    }

    internal void SetPreferenceWithoutPersist(ThemePreference preference)
    {
        Preference = preference;
    }
}
