using GameNetcodeStuff;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MuchBetterEmotes;

internal static class Props
{
    public const string Back = "Back";

    private const int SignIndex = 6;
    private const float LegsHeight = 0.197f;
    private const float LegsScale = 13.99568f;

    public static GameObject? AddSign(PlayerControllerB player, out MeshRenderer? board, out MeshRenderer? back)
    {
        board = null;
        back = null;
        GameObject? prefab = AssetsLoader.SignPrefab;
        if (prefab == null) return null;

        GameObject sign = Object.Instantiate(prefab, player.playerBodyAnimator.transform);
        sign.name = "Sign";
        sign.transform.SetSiblingIndex(SignIndex);
        sign.transform.SetLocalPositionAndRotation(new Vector3(0.029f, -0.45f, 1.3217f), Quaternion.Euler(65.556f, 180f, 180f));

        board = sign.GetComponent<MeshRenderer>();
        board.enabled = false;
        back = AddBack(sign, board);
        return sign;
    }

    public static Renderer? AddLegs(PlayerControllerB player)
    {
        GameObject? prefab = AssetsLoader.LegsPrefab;
        if (prefab == null) return null;

        Transform body = player.playerBodyAnimator.transform;
        Transform model = body.parent;
        GameObject root = Object.Instantiate(prefab, model);

        Transform mesh = root.transform.Find("Mesh");
        Transform armature = root.transform.Find("Armature");
        SkinnedMeshRenderer legs = mesh.GetComponent<SkinnedMeshRenderer>();
        mesh.name = "LEGS";
        mesh.SetParent(model);

        armature.name = "FistPersonLegs";
        armature.SetParent(body);
        armature.localPosition = new Vector3(0f, LegsHeight, 0f);
        armature.localScale = new Vector3(LegsScale, LegsScale, LegsScale);

        legs.forceRenderingOff = true;
        Object.Destroy(root);
        return legs;
    }

    private static MeshRenderer AddBack(GameObject sign, MeshRenderer board)
    {
        GameObject back = new(Back);
        back.layer = sign.layer;
        back.transform.SetParent(sign.transform, false);
        back.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, 0.005f), Quaternion.Euler(0f, 180f, 0f));
        back.transform.localScale = Vector3.one;

        back.AddComponent<MeshFilter>().sharedMesh = sign.GetComponent<MeshFilter>().sharedMesh;
        MeshRenderer backRenderer = back.AddComponent<MeshRenderer>();
        backRenderer.sharedMaterial = board.sharedMaterial;
        backRenderer.renderingLayerMask = board.renderingLayerMask;
        backRenderer.enabled = false;
        return backRenderer;
    }
}
