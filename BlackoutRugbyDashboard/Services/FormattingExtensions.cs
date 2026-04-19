namespace BlackoutRugbyDashboard.Services;

public static class FormattingExtensions
{
    public static string ToSignedString(this int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }
}
