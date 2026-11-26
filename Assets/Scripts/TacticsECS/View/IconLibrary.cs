using UnityEngine;
namespace TacticsECS
{
    /// <summary>All built-in HUD and tech icons come from the same CC0 pixel catalog.</summary>
    public static class IconLibrary
    {
        /// <summary>"hud."로 시작하면 HUD 아이콘(HudArt), 아니면 도트 아이콘 표(SpriteCatalog의 Icon.*).</summary>
        public static Sprite Get(string iconName) => iconName.StartsWith(HudArt.IconPrefix)
            ? HudArt.Icon(iconName.Substring(HudArt.IconPrefix.Length)) : PixelSpriteCatalog.Get("Icon."+iconName);
    }
}
