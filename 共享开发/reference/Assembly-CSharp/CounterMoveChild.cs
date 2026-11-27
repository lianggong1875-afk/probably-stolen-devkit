using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CounterMoveChild : MonoBehaviour
{
	private Vector3 lastParentPosition;

	[SerializeField]
	private float movementMultiplier = 1f;

	private void Start()
	{
		if (transform.parent != null)
		{
			lastParentPosition = transform.parent.position;
		}
	}

	private void LateUpdate()
	{
		if (transform.parent != null)
		{
			if (UIDragHandler.isDraggingAnyUI && UIDragHandler.draggingID == "magnifyingGlass")
			{
				Vector3 vector = transform.parent.position - lastParentPosition;
				transform.position -= vector * movementMultiplier;
			}
			lastParentPosition = transform.parent.position;
		}
	}

	private bool IsAnyGraphicRaycastTargetMagnifyingGlass()
	{
		PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
		pointerEventData.position = Input.mousePosition;
		List<RaycastResult> list = new List<RaycastResult>();
		EventSystem.current.RaycastAll(pointerEventData, list);
		foreach (RaycastResult item in list)
		{
			if (item.gameObject.GetComponent<MagnifyingGlassMono>() != null)
			{
				return true;
			}
		}
		return false;
	}
}
