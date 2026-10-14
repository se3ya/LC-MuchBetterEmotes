using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MuchBetterEmotes;

internal sealed class DragArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IInitializePotentialDragHandler
{
    private RectTransform? _rect;

    public Action<Vector2, bool>? Changed;

    public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

    public void OnPointerDown(PointerEventData eventData) => Report(eventData, true);

    public void OnDrag(PointerEventData eventData) => Report(eventData, false);

    private void Report(PointerEventData eventData, bool press)
    {
        if (Changed == null || eventData.button != PointerEventData.InputButton.Left) return;

        _rect ??= (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            return;
        }

        Changed(Rect.PointToNormalized(_rect.rect, local), press);
    }
}
