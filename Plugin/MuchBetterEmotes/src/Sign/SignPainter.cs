using UnityEngine;

namespace MuchBetterEmotes;

internal sealed class SignPainter : MonoBehaviour
{
    public static SignPainter? Current { get; private set; }

    public static void Create(StartOfRound round)
    {
        if (Current != null) return;

        GameObject host = new("SignPainter");
        host.transform.SetParent(round.transform, false);
        Current = host.AddComponent<SignPainter>();
    }

    private void LateUpdate()
    {
        SignCanvases.Paint();
    }

    private void OnDestroy()
    {
        if (Current != this) return;

        Current = null;
        SignCanvases.DestroyAll();
    }
}
