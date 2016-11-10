﻿using UnityEngine;

[RequireComponent(typeof(PathFinderAgent))]
[RequireComponent(typeof(Mover))]
public abstract class Character : MonoBehaviour
{
	private PathFinderAgent _agent;
	public PathFinderAgent Agent
	{
		get
		{
			if (_agent == null)
			{
				_agent = GetComponent<PathFinderAgent>();
			}
			return _agent;
		}
	}

	public void Setup(PathFinder pathFinder)
	{
		var node = pathFinder.GetRandomWalkableNode();
		transform.position = node.WorldCoordinates + Vector3.up;
		Agent.Setup(pathFinder, node);
	}
}