using UnityEngine;

public class ZoomFollower : MonoBehaviour
{
	public Transform target;

	public float targetNormalScale = 1f;

	[SerializeField]
	private Vector3 baseLocalOffset;

	[SerializeField]
	private Vector3 baseSelfScale = Vector3.one;

	[SerializeField]
	private bool hasBaseline;

	[ContextMenu("Capture Baseline (do this at normal zoom)")]
	private void CaptureBaseline()
	{
		if (!(target == null))
		{
			baseLocalOffset = target.InverseTransformPoint(transform.position);
			baseSelfScale = transform.localScale;
			hasBaseline = true;
		}
	}

	public void SetTarget(Transform master, float masterNormalScale, float masterCurrentScale)
	{
		target = master;
		targetNormalScale = masterNormalScale;
		if (!(master == null))
		{
			float num = masterCurrentScale / masterNormalScale;
			baseLocalOffset = target.InverseTransformPoint(transform.position);
			baseSelfScale = new Vector3(transform.localScale.x / num, transform.localScale.y / num, transform.localScale.z);
			hasBaseline = true;
		}
	}

	private void LateUpdate()
	{
		if (!(target == null) && hasBaseline)
		{
			if (transform.parent != target.parent)
			{
				transform.SetParent(target.parent, worldPositionStays: true);
				Vector3 localPosition = transform.localPosition;
				localPosition.z = 0f;
				transform.localPosition = localPosition;
			}
			float num = target.localScale.x / targetNormalScale;
			transform.position = target.TransformPoint(baseLocalOffset);
			transform.localScale = new Vector3(baseSelfScale.x * num, baseSelfScale.y * num, baseSelfScale.z);
		}
	}
}
