using UnityEngine;
namespace TacticsECS
{
    /// <summary>All built-in HUD and tech icons come from the same CC0 pixel catalog.</summary>
    public static class IconLibrary
    {
        public static Sprite Get(string iconName) => PixelSpriteCatalog.Get("Icon."+iconName);
    }
}
