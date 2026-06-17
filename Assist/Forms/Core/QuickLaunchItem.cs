namespace Assist.Forms.Core;

internal sealed record QuickLaunchItem(
    string Title,
    string Category,
    string Keywords,
    Action Execute)
{
    public override string ToString() => $"{Title}  -  {Category}";
}
