﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
	[SerializeField]
	private bool DrawGizmos = false;
	[SerializeField]
	private string Seed = "Seed";
	[SerializeField]
	private bool UseRandomSeed = true;
	[SerializeField]
	private int Width = 64;
	[SerializeField]
	private int Height = 64;
	[SerializeField]
	private int TileSize = 1;
	[SerializeField]
	[Range(45, 55)]
	private int RoomFillPercentage = 50;
	[SerializeField]
	private int WallThresholdSize = 50;
	[SerializeField]
	private int RoomThresholdSize = 50;
	[SerializeField]
	private int CorridorThickness = 2;
	[SerializeField]
	private int SmoothingLoops = 5;
	[SerializeField]
	private bool RandomizeVertexPositionsX = false;
	[SerializeField]
	[Range(0f, 0.2f)]
	private float VertexOffsetX = 0f;
	[SerializeField]
	private bool RandomizeVertexPositionsY = false;
	[SerializeField]
	[Range(0f, 0.2f)]
	private float VertexOffsetY = 0f;
	[SerializeField]
	private bool RandomizeVertexPositionsZ = false;
	[SerializeField]
	[Range(0f, 0.2f)]
	private float VertexOffsetZ = 0f;

	private Tile[,] _map;
	private List<Room> _survivingRooms;
	private List<Tile> _walkableTiles;

	private void Awake()
	{
		MessageHub.Instance.Subscribe<CreateGameEvent>(OnCreateGameEvent);
		MessageHub.Instance.Subscribe<MeshDestroyedEvent>(OnMeshDestroyedEvent);
	}
	private void OnDrawGizmos()
	{
		if (!DrawGizmos)
		{
			return;
		}

		if (_survivingRooms == null)
		{
			return;
		}

		//for (var x = 0; x < _map.GetLength(0); x++)
		//{
		//	for (var y = 0; y < _map.GetLength(1); y++)
		//	{
		//		var tile = _map[x, y];
		//		Gizmos.color = tile.Type == TileType.Floor ? Color.green : tile.Type == TileType.Wall ? Color.red : tile.Type == TileType.Roof ? Color.blue : Color.cyan;
		//		Gizmos.DrawCube(tile.WorldCoordinates + (Vector3.one * TileSize * 0.5f) + (Vector3.up * 5.0f), Vector3.one * 0.5f);
		//	}
		//}
	}

	private void OnCreateGameEvent(CreateGameEvent createGameEvent)
	{
		StartCoroutine(CreateMap(() =>
		{
			MessageHub.Instance.Publish(new MapCreatedEvent(null)
			{
				Map = _map
			});
		}));
	}
	private void OnMeshDestroyedEvent(MeshDestroyedEvent meshDestroyedEvent)
	{
		_map = null;
		_survivingRooms.Clear();
		_walkableTiles.Clear();

		MessageHub.Instance.Publish(new MapDestroyedEvent(null));
	}
	private IEnumerator CreateMap(Action completed)
	{
		_map = new Tile[Width, Height];
		_walkableTiles = new List<Tile>(_map.Length);

		RandomFillMap();
		GenerateClusters();
		FilterWalls();
		FilterRooms();
		ConnectClosestRooms();
		//CreateWater();
		ConfigureTiles();
		RegisterWalkableTiles();

		completed();

		yield break;
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
					_map[x, y] = new Tile(x, y, TileType.Roof, TileSize);
				}
				else
				{
					var tileType = rng.Next(0, 100) < RoomFillPercentage ? TileType.Floor : TileType.Roof;
					_map[x, y] = new Tile(x, y, tileType, TileSize);
				}
			}
		}
	}
	private void GenerateClusters()
	{
		for (var i = 0; i < SmoothingLoops; i++)
		{
			for (var x = 1; x < Width - 1; x++)
			{
				for (var y = 1; y < Height - 1; y++)
				{
					if (GetNumberOfNeighbouringTiles(x, y) > 4)
					{
						_map[x, y].Type = TileType.Floor;
					}
					else if (GetNumberOfNeighbouringTiles(x, y) < 4)
					{
						_map[x, y].Type = TileType.Roof;
					}
				}
			}
		}
	}
	private void FilterWalls()
	{
		var wallRegions = GetRegions(TileType.Roof);
		for (var i = 0; i < wallRegions.Count; i++)
		{
			var wallRegion = wallRegions[i];
			if (wallRegion.Count < WallThresholdSize)
			{
				for (var j = 0; j < wallRegion.Count; j++)
				{
					_map[wallRegion[j].GridCoordinates.X, wallRegion[j].GridCoordinates.Y].Type = TileType.Floor;
				}
			}
		}
	}
	private void FilterRooms()
	{
		_survivingRooms = new List<Room>(128);

		var roomRegions = GetRegions(TileType.Floor);
		for (var i = 0; i < roomRegions.Count; i++)
		{
			var roomRegion = roomRegions[i];
			if (roomRegion.Count < RoomThresholdSize)
			{
				for (var j = 0; j < roomRegion.Count; j++)
				{
					_map[roomRegion[j].GridCoordinates.X, roomRegion[j].GridCoordinates.Y].Type = TileType.Roof;
				}
			}
			else
			{
				_survivingRooms.Add(new Room(roomRegion));
			}
		}

		_survivingRooms.Sort();
	}
	private void ConnectClosestRooms(bool forceAccessibilityFromMainRoom = false)
	{
		_survivingRooms[0].IsMainRoom = true;
		_survivingRooms[0].IsAccessibleFromMainRoom = true;

		var roomListA = new List<Room>(64);
		var roomListB = new List<Room>(64);

		if (forceAccessibilityFromMainRoom)
		{
			for (var i = 0; i < _survivingRooms.Count; i++)
			{
				if (_survivingRooms[i].IsAccessibleFromMainRoom)
				{
					roomListB.Add(_survivingRooms[i]);
				}
				else
				{
					roomListA.Add(_survivingRooms[i]);
				}
			}
		}
		else
		{
			roomListA = _survivingRooms;
			roomListB = _survivingRooms;
		}

		var bestDistance = 0f;
		var possibleConnectionFound = false;
		Tile bestTileA = null;
		Tile bestTileB = null;
		Room bestRoomA = null;
		Room bestRoomB = null;

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

				for (var tileIndexA = 0; tileIndexA < roomListA[i].Tiles.Count; tileIndexA++)
				{
					for (var tileIndexB = 0; tileIndexB < roomListB[j].Tiles.Count; tileIndexB++)
					{
						var tileA = roomListA[i].Tiles[tileIndexA];
						var tileB = roomListB[j].Tiles[tileIndexB];
						var distanceX = (tileA.WorldCoordinates.x - tileB.WorldCoordinates.x) * (tileA.WorldCoordinates.x - tileB.WorldCoordinates.x);
						var distanceY = (tileA.WorldCoordinates.z - tileB.WorldCoordinates.z) * (tileA.WorldCoordinates.z - tileB.WorldCoordinates.z);
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
			ConnectClosestRooms(true);
		}

		if (!forceAccessibilityFromMainRoom)
		{
			ConnectClosestRooms(true);
		}
	}
	private void CreatePassage(Room roomA, Room roomB, Tile tileA, Tile tileB)
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

		var line = GetLine(tileA.GridCoordinates, tileB.GridCoordinates);
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
					var drawX = c.X + x;
					var drawY = c.Y + y;
					if (IsInMapRange(drawX, drawY))
					{
						_map[drawX, drawY].Type = TileType.Floor;
					}
				}
			}
		}
	}
	private void CreateWater()
	{
		for (var i = 0; i < _survivingRooms.Count; i++)
		{
			var room = _survivingRooms[i];
			var maximumWaterTiles = 50;
			var waterTiles = 0;
			for (var j = 0; j < room.Tiles.Count; j++)
			{
				var tile = room.Tiles[j];
				if (tile.Type == TileType.Floor)
				{
					_map[tile.GridCoordinates.X, tile.GridCoordinates.Y].Type = TileType.Water;
					waterTiles++;
				}

				if (waterTiles >= maximumWaterTiles || waterTiles >= room.Tiles.Count * 0.5f)
				{
					break;
				}
			}
		}
	}
	private void ConfigureTiles()
	{
		var controlNodes = new ControlNode[Width, Height];
		for (var x = 0; x < Width; x++)
		{
			for (var y = 0; y < Height; y++)
			{
				var tile = _map[x, y];
				var position = tile.WorldCoordinates.WithY(0);

				if (RandomizeVertexPositionsX)
				{
					position += Vector3.right * UnityEngine.Random.Range(-(TileSize * VertexOffsetX), TileSize * VertexOffsetX);
				}
				if (RandomizeVertexPositionsY)
				{
					position += Vector3.up * UnityEngine.Random.Range(-(TileSize * VertexOffsetY), TileSize * VertexOffsetY);
				}
				if (RandomizeVertexPositionsZ)
				{
					position += Vector3.forward * UnityEngine.Random.Range(-(TileSize * VertexOffsetZ), TileSize * VertexOffsetZ);
				}

				controlNodes[x, y] = new ControlNode(position, tile.Type == TileType.Floor, TileSize);
			}
		}

		for (var x = 0; x < Width - 1; x++)
		{
			for (var y = 0; y < Height - 1; y++)
			{
				var tile = _map[x, y];
				tile.SetConfiguration(controlNodes[x, y + 1], controlNodes[x + 1, y + 1], controlNodes[x + 1, y], controlNodes[x, y]);
			}
		}
	}
	private void RegisterWalkableTiles()
	{
		for (var x = 0; x < Width; x++)
		{
			for (var y = 0; y < Height; y++)
			{
				var tile = _map[x, y];
				if (tile.Type == TileType.Floor && tile.ConfigurationSquare != null && tile.ConfigurationSquare.Configuration == 15)
				{
					_walkableTiles.Add(tile);
				}
			}
		}
	}
	private bool IsInMapRange(int x, int y)
	{
		return x >= 0 && x < Width && y >= 0 && y < Height;
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
						neighbouringTiles += _map[x, y].Type == TileType.Floor ? 1 : 0;
					}
				}
			}
		}

		return neighbouringTiles;
	}
	private List<List<Tile>> GetRegions(TileType tileType)
	{
		var regions = new List<List<Tile>>(64);
		var mapFlags = new bool[Width, Height];

		for (var x = 0; x < Width; x++)
		{
			for (var y = 0; y < Height; y++)
			{
				if (!mapFlags[x, y] && _map[x, y].Type == tileType)
				{
					var regionTiles = GetRegionTiles(x, y);
					regions.Add(regionTiles);

					for (var i = 0; i < regionTiles.Count; i++)
					{
						var xRegion = regionTiles[i].GridCoordinates.X;
						var yRegion = regionTiles[i].GridCoordinates.Y;
						mapFlags[xRegion, yRegion] = true;
					}
				}
			}
		}

		return regions;
	}
	private List<Tile> GetRegionTiles(int startX, int startY)
	{
		var tiles = new List<Tile>(1024);
		var mapFlags = new bool[Width, Height];
		var tileType = _map[startX, startY].Type;
		var queue = new Queue<Tile>();

		queue.Enqueue(new Tile(startX, startY, tileType, TileSize));
		mapFlags[startX, startY] = true;

		while (queue.Count > 0)
		{
			var tile = queue.Dequeue();
			tiles.Add(tile);

			for (var x = (int)tile.GridCoordinates.X - 1; x <= tile.GridCoordinates.X + 1; x++)
			{
				for (var y = (int)tile.GridCoordinates.Y - 1; y <= tile.GridCoordinates.Y + 1; y++)
				{
					if (IsInMapRange(x, y) && (y == tile.GridCoordinates.Y || x == tile.GridCoordinates.X))
					{
						if (!mapFlags[x, y] && _map[x, y].Type == tileType)
						{
							mapFlags[x, y] = true;
							queue.Enqueue(_map[x, y]);
						}
					}
				}
			}
		}

		return tiles;
	}
	private List<Coordinates> GetLine(Coordinates from, Coordinates to)
	{
		var line = new List<Coordinates>(64);

		var x = from.X;
		var y = from.Y;

		var deltaX = to.X - from.X;
		var deltaY = to.Y - from.Y;

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

	public Tile GetRandomWalkableTile()
	{
		return _walkableTiles.GetRandomElement();
	}
	public int GetTileSize()
	{
		return TileSize;
	}
}