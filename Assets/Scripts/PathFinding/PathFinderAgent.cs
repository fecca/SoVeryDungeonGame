﻿using System;
using System.Collections.Generic;
using UnityEngine;

public class PathFinderAgent : MonoBehaviour
{
	private LinkedList<PathfindingNode> _path = new LinkedList<PathfindingNode>();
	private PathFinder _pathFinder;
	private Vector3 _pendingTo;
	private Action _completedPath;
	private bool _pendingNewPath;
	private float _movementSpeed;
	private PathfindingNode _currentNode;
	private PathfindingNode _nextNode;

	private void FixedUpdate()
	{
		if (_path.Count > 0)
		{
			MoveAlongPath();
		}
	}
	private void OnDrawGizmos()
	{
		if (_path != null)
		{
			for (var iteration = _path.First; iteration != null; iteration = iteration.Next)
			{
				Gizmos.color = Color.blue;
				Gizmos.DrawSphere(new Vector3(iteration.Value.WorldCoordinates.x, 1, iteration.Value.WorldCoordinates.z), 0.25f);
			}
		}
	}

	public void Setup(PathFinder pathFinder, PathfindingNode startingNode)
	{
		_pathFinder = pathFinder;
		_currentNode = startingNode;
		_currentNode.Occupied = true;
	}
	public void StartPathTo(Vector3 targetPosition, float movementSpeed, Action completed = null)
	{
		_movementSpeed = movementSpeed;
		_completedPath = completed;
		if (_path.Count > 0)
		{
			_pendingNewPath = true;
			_pendingTo = targetPosition;
		}
		else
		{
			_pendingNewPath = false;
			_pathFinder.GetPath(transform.position, targetPosition, (path) =>
			{
				_path = path;
			});
		}
	}
	public void StartPathTo(Tile targetTile, float movementSpeed, Action completed = null)
	{
		StartPathTo(new Vector3(targetTile.WorldCoordinates.x, transform.position.y, targetTile.WorldCoordinates.z), movementSpeed, completed);
	}
	public void SmoothStop()
	{
		var firstNode = _path.First;
		_path.Clear();
		if (firstNode != null)
		{
			_path.AddFirst(firstNode);
		}
	}

	private void MoveAlongPath()
	{
		_nextNode = _path.First.Value;
		_nextNode.Occupied = true;
		var targetPosition = new Vector3(_nextNode.WorldCoordinates.x, transform.position.y, _nextNode.WorldCoordinates.z);
		transform.position = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * _movementSpeed);

		if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
		{
			ArrivedAtNode();
		}
	}
	private void ArrivedAtNode()
	{
		_currentNode.Occupied = false;
		_currentNode = _nextNode;

		if (_pendingNewPath)
		{
			_pendingNewPath = false;
			_pathFinder.GetPath(transform.position, _pendingTo, (path) =>
			{
				_path = path;
			});
		}
		else
		{
			_path.RemoveFirst();
			if (_path.Count == 0)
			{
				if (_completedPath != null)
				{
					_completedPath();
				}
			}
		}
	}
}