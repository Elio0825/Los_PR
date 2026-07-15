using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace LosPr.BLM.UI.Assets;

internal sealed class BlmFamiliarTexture : IDisposable
{
    private const string ResourceName = "LosPr.BLM.UI.Assets.BlackCatFamiliar.png";

    public IDalamudTextureWrap? GetOrQueue()
        => Svc.Texture
            .GetFromManifestResource(typeof(BlmFamiliarTexture).Assembly, ResourceName)
            .GetWrapOrDefault();

    public void Dispose()
    {
        // Shared manifest textures are owned and released by Dalamud.
    }
}
