﻿using System;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
	[SerializeField]
	private string Seed = "Seed";
	[SerializeField]
	private bool UseRandomSeed = true;
	[SerializeField]
	private int Width = 64;
	[SerializeField]
	private int Height = 64;
	[SerializeField]
	[Range(45, 55)]
	private int RandomFillPercent = 50;
	[SerializeField]
	private int WallThresholdSize = 50;
	[SerializeField]
	private int RoomThresholdSize = 50;
	[SerializeField]
	private int CorridorThickness = 2;
	[SerializeField]
	private int SmoothingLoops = 5;

	private int[,] map;

	private void Start()
	{
		GenerateMap();
	}

	private void Update()
	{
		if (Input.GetMouseButtonDown(0))
		{
			GenerateMap();
		}
	}

	private void GenerateMap()
	{
		map = new int[Width, Height];

		RandomFillMap();
		SmoothMap();
		ProcessWalls();
		ProcessRooms();

		var meshGenerator = GetComponent<MeshGenerator>();
		meshGenerator.GenerateMesh(map, 1);
	}

	private void RandomFillMap()
	{
		if (UseRandomSeed)
		{
			Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue).ToString();
		}

		System.Random rng = new System.Random(Seed.GetHashCode());
		for (var x = 0; x < Width; x++)
		{
			for (var y = 0; y < Height; y++)
			{
				if (x == 0 || x == Width - 1 || y == 0 || y == Height - 1)
				{
					map[x, y] = 0;
				}
				else
				{
					map[x, y] = rng.Next(0, 100) < RandomFillPercent ? 1 : 0;
				}
			}
		}
	}

	private void SmoothMap()
	{
		for (var i = 0; i < SmoothingLoops; i++)
		{
			for (var x = 1; x < Width - 1; x++)
			{
				for (var y = 1; y < Height - 1; y++)
				{
					if (GetNumberOfNeighbouringTiles(x, y) > 4)
					{
						map[x, y] = 1;
					}
					else if (GetNumberOfNeighbouringTiles(x, y) < 4)
					{
						map[x, y] = 0;
					}
				}
			}
		}
	}

	private int GetNumberOfNeighbouringTiles(int xPosition, int yPosition)
	{
		var neighbouringTiles = 0;
		for (var x = xPosition - 1; x <= xPosition + 1; x++)
		{
			for (var y = yPosition - 1; y <= yPosition + 1; y++)
			{
				if (IsInMapRange(x, y))
				{
					if (x != xPosition || y != yPosition)
					{
						neighbouringTiles += map[x, y];
					}
				}
			}
		}

		return neighbouringTiles;
	}

	private void ProcessWalls()
	{
		var wallRegions = GetRegions(0);
		for (var i = 0; i < wallRegions.Count; i++)
		{
			var wallRegion = wallRegions[i];
			if (wallRegion.Count < WallThresholdSize)
			{
				for (var j = 0; j < wallRegion.Count; j++)
				{
					map[wallRegion[j].TileX, wallRegion[j].TileY] = 1;
				}
			}
		}
	}

	private void ProcessRooms()
	{
		var survivingRooms = new List<Room>(64);

		var roomRegions = GetRegions(1);
		for (var i = 0; i < roomRegions.Count; i++)
		{
			var roomRegion = roomRegions[i];
			if (roomRegion.Count < RoomThresholdSize)
			{
				for (var j = 0; j < roomRegion.Count; j++)
				{
					map[roomRegion[j].TileX, roomRegion[j].TileY] = 0;
				}
			}
			else
			{
				survivingRooms.Add(new Room(roomRegion, map));
			}
		}

		survivingRooms.Sort();
		survivingRooms[0].IsMainRoom = true;
		survivingRooms[0].IsAccessibleFromMainRoom = true;

		ConnectClosestRooms(survivingRooms);
	}

	private List<List<Coordinates>> GetRegions(int tileType)
	{
		var regions = new List<List<Coordinates>>(64);
		var mapFlags = new int[Width, Height];

		for (var x = 0; x < Width; x++)
		{
			for (var y = 0; y < Height; y++)
			{
				if (mapFlags[x, y] == 0 && map[x, y] == tileType)
				{
					var newRegion = GetRegionTiles(x, y);
					regions.Add(newRegion);

					for (var i = 0; i < newRegion.Count; i++)
					{
						mapFlags[newRegion[i].TileX, newRegion[i].TileY] = 1;
					}
				}
			}
		}

		return regions;
	}

	private List<Coordinates> GetRegionTiles(int startX, int startY)
	{
		var tiles = new List<Coordinates>(1024);
		var mapFlags = new int[Width, Height];
		var tileType = map[startX, startY];
		var queue = new Queue<Coordinates>();

		queue.Enqueue(new Coordinates(startX, startY));
		mapFlags[startX, startY] = 1;

		while (queue.Count > 0)
		{
			var tile = queue.Dequeue();
			tiles.Add(tile);

			for (var x = tile.TileX - 1; x <= tile.TileX + 1; x++)
			{
				for (var y = tile.TileY - 1; y <= tile.TileY + 1; y++)
				{
					if (IsInMapRange(x, y) && (y == tile.TileY || x == tile.TileX))
					{
						if (mapFlags[x, y] == 0 && map[x, y] == tileType)
						{
							mapFlags[x, y] = 1;
							queue.Enqueue(new Coordinates(x, y));
						}
					}
				}
			}
		}

		return tiles;
	}

	private void ConnectClosestRooms(List<Room> allRooms, bool forceAccessibilityFromMainRoom = false)
	{
		var roomListA = new List<Room>(64);
		var roomListB = new List<Room>(64);

		if (forceAccessibilityFromMainRoom)
		{
			for (var i = 0; i < allRooms.Count; i++)
			{
				if (allRooms[i].IsAccessibleFromMainRoom)
				{
					roomListB.Add(allRooms[i]);
				}
				else
				{
					roomListA.Add(allRooms[i]);
				}
			}
		}
		else
		{
			roomListA = allRooms;
			roomListB = allRooms;
		}

		var bestDistance = 0;
		var bestTileA = new Coordinates();
		var bestTileB = new Coordinates();
		var bestRoomA = new Room();
		var bestRoomB = new Room();
		var possibleConnectionFound = false;

		for (var i = 0; i < roomListA.Count; i++)
		{
			if (!forceAccessibilityFromMainRoom)
			{
				possibleConnectionFound = false;
				if (roomListA[i].ConnectedRooms.Count > 0)
				{
					continue;
				}
			}

			for (var j = 0; j < roomListB.Count; j++)
			{
				if (roomListA[i] == roomListB[j] || roomListA[i].IsConnected(roomListB[j]))
				{
					continue;
				}

				for (var tileIndexA = 0; tileIndexA < roomListA[i].EdgeTiles.Count; tileIndexA++)
				{
					for (var tileIndexB = 0; tileIndexB < roomListB[j].EdgeTiles.Count; tileIndexB++)
					{
						var tileA = roomListA[i].EdgeTiles[tileIndexA];
						var tileB = roomListB[j].EdgeTiles[tileIndexB];
						var distanceX = (tileA.TileX - tileB.TileX) * (tileA.TileX - tileB.TileX);
						var distanceY = (tileA.TileY - tileB.TileY) * (tileA.TileY - tileB.TileY);
						var distanceBetweenRooms = distanceX + distanceY;

						if (distanceBetweenRooms < bestDistance || !possibleConnectionFound)
						{
							bestDistance = distanceBetweenRooms;
							possibleConnectionFound = true;
							bestTileA = tileA;
							bestTileB = tileB;
							bestRoomA = roomListA[i];
							bestRoomB = roomListB[j];
						}
					}
				}
			}

			if (possibleConnectionFound && !forceAccessibilityFromMainRoom)
			{
				CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
			}
		}

		if (possibleConnectionFound && forceAccessibilityFromMainRoom)
		{
			CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
			ConnectClosestRooms(allRooms, true);
		}

		if (!forceAccessibilityFromMainRoom)
		{
			ConnectClosestRooms(allRooms, true);
		}
	}

	private void CreatePassage(Room roomA, Room roomB, Coordinates tileA, Coordinates tileB)
	{
		if (roomA.IsAccessibleFromMainRoom)
		{
			roomB.SetAccessibleFromMainRoom();
		}
		else if (roomB.IsAccessibleFromMainRoom)
		{
			roomA.SetAccessibleFromMainRoom();
		}
		roomA.ConnectedRooms.Add(roomB);
		roomB.ConnectedRooms.Add(roomA);

		var line = GetLine(tileA, tileB);
		for (var i = 0; i < line.Count; i++)
		{
			CreateCorridor(line[i], CorridorThickness);
		}
	}

	private void CreateCorridor(Coordinates c, int r)
	{
		for (var x = -r; x <= r; x++)
		{
			for (var y = -r; y <= r; y++)
			{
				if (x * x + y * y <= r * r)
				{
					var drawX = c.TileX + x;
					var drawY = c.TileY + y;
					if (IsInMapRange(drawX, drawY))
					{
						map[drawX, drawY] = 1;
					}
				}
			}
		}
	}

	private List<Coordinates> GetLine(Coordinates from, Coordinates to)
	{
		var line = new List<Coordinates>(64);

		var x = from.TileX;
		var y = from.TileY;

		var deltaX = to.TileX - from.TileX;
		var deltaY = to.TileY - from.TileY;

		var step = Math.Sign(deltaX);
		var gradientStep = Math.Sign(deltaY);

		var longest = Mathf.Abs(deltaX);
		var shortest = Mathf.Abs(deltaY);

		var inverted = false;
		if (longest < shortest)
		{
			inverted = true;
			longest = Mathf.Abs(deltaY);
			shortest = Mathf.Abs(deltaX);
			step = Math.Sign(deltaY);
			gradientStep = Math.Sign(deltaX);
		}

		var gradientAccumulation = longest / 2;
		for (var i = 0; i < longest; i++)
		{
			line.Add(new Coordinates(x, y));
			if (inverted)
			{
				y += step;
			}
			else
			{
				x += step;
			}

			gradientAccumulation += shortest;
			if (gradientAccumulation >= longest)
			{
				if (inverted)
				{
					x += gradientStep;
				}
				else
				{
					y += gradientStep;
				}
				gradientAccumulation -= longest;
			}
		}

		return line;
	}

	private Vector3 CoordinatesToWorldPoint(Coordinates tile)
	{
		return new Vector3(-Width / 2f + 0.5f + tile.TileX, 2, -Height / 2f + 0.5f + tile.TileY);
	}

	private bool IsInMapRange(int x, int y)
	{
		return x >= 0 && x < Width && y >= 0 && y < Height;
	}
}