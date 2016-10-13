﻿using System.Collections.Generic;
using UnityEngine;

public class PathFinderAgent : MonoBehaviour
{
	[SerializeField]
	private float MovementSpeed = 5.0f;

	private LinkedList<PathNode> _path;
	private PathFinder _pathFinder;
	private Color _randomColor;

	private void Update()
	{
		if (_path.Count > 0)
		{
			MoveAlongPath();
		}
	}

	public void Setup(PathFinder pathFinder)
	{
		_path = new LinkedList<PathNode>();
		_pathFinder = pathFinder;
		_randomColor = new Color(Random.value, Random.value, Random.value);
	}

	public void StartPath(Vector2 from, Vector2 to)
	{
		PathNode unfinishedNode = null;
		if (_path.Count > 0)
		{
			unfinishedNode = _path.First.Value;
		}
		_path = _pathFinder.GetPath(from, to);
		if (unfinishedNode != null)
		{
			_path.AddFirst(unfinishedNode);
		}
	}

	private void MoveAlongPath()
	{
		var targetNode = _path.First;
		var targetPosition = new Vector3(targetNode.Value.Tile.Coordinates.X, transform.position.y, targetNode.Value.Tile.Coordinates.Y);
		transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * MovementSpeed);

		if (Vector3.Distance(transform.position, targetPosition) < 0.02f)
		{
			_path.Remove(targetNode);
		}
	}

	private void OnDrawGizmos()
	{
		if (_path != null)
		{
			for (var iteration = _path.First; iteration != null; iteration = iteration.Next)
			{
				Gizmos.color = _randomColor;
				Gizmos.DrawCube(new Vector3(iteration.Value.Tile.Coordinates.X, 1, iteration.Value.Tile.Coordinates.Y), Vector3.one * 0.25f);
			}
		}
	}
}