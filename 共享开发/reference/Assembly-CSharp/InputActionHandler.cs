using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class InputActionHandler : MonoBehaviour
{
	[SerializeField]
	public int actionPriority;

	[NonSerialized]
	public bool lastEnabled;

	private bool initialized;

	public abstract List<KeyCode> keyListeners { get; }

	public int priority
	{
		get
		{
			return actionPriority;
		}
		set
		{
			actionPriority = value;
		}
	}

	private void Start()
	{
		TryInit();
		lastEnabled = enabled;
		InputActionManager.current.Add(this);
	}

	public void UpdatePress(ISet<KeyCode> keysPressed)
	{
		if (enabled && initialized && keysPressed.Count != 0)
		{
			OnEventPress(keysPressed);
		}
	}

	public void UpdateRelease(ISet<KeyCode> keysReleased)
	{
		if (enabled && initialized && keysReleased.Count != 0)
		{
			OnEventRelease(keysReleased);
		}
	}

	public void UpdateMouseMove(Vector3 mousePosition, Vector3 mouseDelta)
	{
		if (enabled && initialized && mouseDelta.sqrMagnitude != 0f)
		{
			OnEventMouseMove(mousePosition, mouseDelta);
		}
	}

	public void UpdateMouseScroll(Vector2 scrollDelta)
	{
		if (enabled && initialized && scrollDelta.sqrMagnitude != 0f)
		{
			OnEventMouseScroll(scrollDelta);
		}
	}

	public bool UpdateUpdate()
	{
		if (!enabled || !initialized)
		{
			return false;
		}
		return OnEventUpdate();
	}

	public void UpdateReset()
	{
		if (initialized)
		{
			OnEventReset();
		}
	}

	protected abstract bool OnEventInit();

	protected abstract void OnEventPress(ISet<KeyCode> keysPressed);

	protected abstract void OnEventRelease(ISet<KeyCode> keysReleased);

	protected abstract void OnEventMouseMove(Vector3 mousePosition, Vector3 mouseDelta);

	protected abstract void OnEventMouseScroll(Vector2 scrollDelta);

	protected abstract bool OnEventUpdate();

	protected abstract void OnEventReset();

	public bool Enable()
	{
		lastEnabled = true;
		if (enabled)
		{
			return false;
		}
		UpdateReset();
		enabled = true;
		return true;
	}

	public bool Disable()
	{
		lastEnabled = false;
		if (!enabled)
		{
			return false;
		}
		enabled = false;
		UpdateReset();
		return true;
	}

	private void TryInit()
	{
		if (OnEventInit())
		{
			initialized = true;
		}
	}

	public void Check()
	{
		if (!initialized)
		{
			TryInit();
		}
		else if (!enabled && lastEnabled)
		{
			lastEnabled = false;
			OnEventReset();
		}
		else if (enabled && !lastEnabled)
		{
			lastEnabled = true;
			OnEventReset();
		}
	}
}
