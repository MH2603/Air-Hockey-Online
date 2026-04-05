namespace MH.UI
{
    /// <summary>
    /// Use this base (or override <see cref="UIView.Layer"/>) for stacked windows (Escape pops top).
    /// </summary>
    public class UIWindow : UIView
    {
        public override UILayer Layer => UILayer.Window;
    }
}
