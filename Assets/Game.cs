﻿using UnityEngine;

public class Game : MonoBehaviour
{
	[SerializeField]
	private MapGenerator MapGenerator = null;
	[SerializeField]
	private MeshGenerator MeshGenerator = null;
	[SerializeField]
	private PlayerController Controller = null;
	[SerializeField]
	private PathFinder PathFinder = null;

	private void Start()
	{
		var map = MapGenerator.GenerateMap();
		PathFinder.RegisterMap(map);
		MeshGenerator.GenerateMeshes(map);
		Controller.SetPosition(MapGenerator.GetPlayerPosition());
	}
}