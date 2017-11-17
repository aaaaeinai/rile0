﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour {

	public int width;
	public int height;

	public string seed;
	public bool useRandomSeed;

	[Range(0, 100)]
	public int randomFillPercent;

	int [,] map;

	void Start()
	{
		GenerateMap();
	}


	void Update()
	{
		if (Input.GetMouseButtonDown(0))
			GenerateMap();
	}

    private void GenerateMap()
    {
        map = new int[width, height];
		RandomFillMap();

		for (int i = 0; i < 5; i++)
			SmoothMap();

		ProcessMap();

		int borderSize = 10;
		int[,] borderedMap = new int[width + borderSize * 2, height + borderSize * 2];

		for (int i = 0; i < borderedMap.GetLength(0); i ++) {
			for (int j = 0; j < borderedMap.GetLength(1); j ++) {
				if (i >= borderSize && i < width + borderSize && j >= borderSize && j < height + borderSize)
					borderedMap[i, j] = map[i -borderSize, j -borderSize];
				else
					borderedMap[i, j] = 1;
			}
		}

		MeshGenerator meshGen  = GetComponent<MeshGenerator>();
		meshGen.GenerateMesh(borderedMap, 1);
    }

    void RandomFillMap() {
		if (useRandomSeed)
			seed = Time.time.ToString();

		System.Random pseudoRandom = new System.Random(seed.GetHashCode());

		for (int i = 0; i < width; i ++) {
			for (int j = 0; j < height; j ++) {
				if (i == 0 || i == width -1 || j == 0 || j == height -1)
					map[i,j] = 1;
				else 
					map[i,j] = (pseudoRandom.Next(0,100) < randomFillPercent) ? 1 : 0;
				
			}
		}
	}

	void SmoothMap() {
		for (int i = 0; i < width; i ++) {
			for (int j = 0; j < height; j ++) {
				int sideCountTiles = CountSides(i, j);

				if (sideCountTiles > 4)
					map[i, j] = 1;
				else if (sideCountTiles < 4)
					map[i, j] = 0;
			}
		}
	}

	int CountSides(int gridX, int gridY) {
		int sideCount = 0;

		// Traverse 3x3 grid
		for (int sideX = gridX - 1; sideX <= gridX + 1; sideX ++) {
			for (int sideY = gridY - 1; sideY <= gridY + 1; sideY ++) {
				if (IsInMapRange(sideX, sideY)) {
					if (sideX != gridX || sideY != gridY) // if not center
						sideCount += map[sideX, sideY];
				} else
					sideCount ++;
			}
		}
		return sideCount;
	}

	// Remove tile less than wall threshold

	struct Coord {
		public int tileX;
		public int tileY;

		public Coord(int x, int y) {
			tileX = x;
			tileY = y;
		}
	}

	void ProcessMap() {
		List<List<Coord>> wallRegions = GetRegions(1);
		int wallThresholdSize = 50;

		foreach (List<Coord> wallRegion in wallRegions) {
			if (wallRegion.Count < wallThresholdSize) {
				foreach (Coord tile in wallRegion) {
					map[tile.tileX, tile.tileY] = 0;
				}
			}
		}

        List<List<Coord>> roomRegions = GetRegions (0);
        int roomThresholdSize = 50;
		List<Room> survivingRooms = new List<Room>();
        
        foreach (List<Coord> roomRegion in roomRegions) {
            if (roomRegion.Count < roomThresholdSize) {
                foreach (Coord tile in roomRegion) {
                    map[tile.tileX,tile.tileY] = 1;
                }
            } else {
				survivingRooms.Add(new Room(roomRegion, map));
			}
        }

		survivingRooms.Sort();
		survivingRooms[0].isMainRoom = true;
		survivingRooms[0].isAccessibleFromMainRoom = true;
		ConnectClosestRooms(survivingRooms);
	}


	List<List<Coord>> GetRegions(int tileType) {
		List<List<Coord>> regions = new List<List<Coord>>();
		int[,] mapFlags = new int[width, height];


		for (int i = 0; i < width; i ++) {
			for (int j = 0; j < height; j ++) {
				if (mapFlags[i,j] == 0 && map[i,j] == tileType) {
					List<Coord> newRegion = GetRegionTiles(i, j);
					regions.Add(newRegion);

					foreach (Coord tile in newRegion)
						mapFlags[tile.tileX, tile.tileY] = 1;
				}
			}
		}

		return regions;
	}

	List<Coord> GetRegionTiles(int startX, int startY) {
		List<Coord> tiles = new List<Coord>();
		int[,] mapFlags = new int[width, height];
		int tileType = map[startX, startY];

		Queue<Coord> queue = new Queue<Coord>();
		queue.Enqueue(new Coord(startX, startY));
		mapFlags[startX, startY] = 1;

		while (queue.Count > 0) {
			Coord tile = queue.Dequeue();
			tiles.Add(tile);

			for (int x = tile.tileX -1; x <= tile.tileX +1; x ++) {
				for (int y = tile.tileY -1; y <= tile.tileY +1; y++) {
					if (IsInMapRange(x, y) && (y == tile.tileY || x == tile.tileX)) {
						if (mapFlags[x, y] == 0 && map[x, y] == tileType) {
							mapFlags[x, y] = 1;
							queue.Enqueue(new Coord(x, y));
						}
					}
				}
			}
		}

		return tiles;
	}

    bool IsInMapRange(int x, int y) {
		return x >= 0 && x < width && y >= 0 && y < height;
	}

	// ConnectClosestRooms

	void ConnectClosestRooms(List<Room> allRooms, bool forceAccessFromMainRoom = false) {

		List<Room> roomListA = new List<Room>();
		List<Room> roomListB = new List<Room>();

		if (forceAccessFromMainRoom) {
			foreach (Room room in allRooms) {
				if (room.isAccessibleFromMainRoom) {
					roomListB.Add(room);
				} else {
					roomListA.Add(room);
				}
			}
		} else {
			roomListA = allRooms;
			roomListB = allRooms;
		}

		int bestDist = 0;
		Coord bestTileA = new Coord();
		Coord bestTileB = new Coord();
		Room bestRoomA = new Room();
		Room bestRoomB = new Room();
		bool possibleConnectionFound = false;

		foreach (Room roomA in roomListA) {

			if (!forceAccessFromMainRoom) {
				possibleConnectionFound = false;
				if (roomA.connectedRooms.Count > 0)
					continue;
			}

			foreach (Room roomB in roomListB) {
				if (roomA == roomB || roomA.IsConnected(roomB)) continue;

				for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA ++) {
					for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB ++) {
						Coord tileA = roomA.edgeTiles[tileIndexA];
						Coord tileB = roomB.edgeTiles[tileIndexB];
						int distBetweenRooms = (int) (Mathf.Pow(tileA.tileX - tileB.tileX, 2) + Mathf.Pow(tileA.tileY - tileB.tileY, 2));

						if (distBetweenRooms < bestDist || !possibleConnectionFound) {
							bestDist = distBetweenRooms;
							possibleConnectionFound = true;
							bestTileA = tileA;
							bestTileB = tileB;
							bestRoomA = roomA;
							bestRoomB = roomB;
						}
					}
				}
			}

			if (possibleConnectionFound && !forceAccessFromMainRoom) {
				CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
			}
		}

		if (possibleConnectionFound && forceAccessFromMainRoom) {
			CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
			ConnectClosestRooms(allRooms, true);
		}

		if (!forceAccessFromMainRoom) {
			ConnectClosestRooms(allRooms, true);
		}
	}

	void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB) {
		Room.ConnectRooms(roomA, roomB);
		// Debug.DrawLine(CoordToWorldPoint(tileA), CoordToWorldPoint(tileB), Color.green, 10);

		List<Coord> line = GetLine(tileA, tileB);
		foreach (Coord c in line) {
			DrawCircle(c, 5);
		}
	}

	void DrawCircle(Coord c, int r) {
		for (int x = -r; x <= r;x ++) {
			for (int y = -r; y <= r;y ++) {
				if (x*x + y*y <= r*r) {
					int drawX = c.tileX + x;
					int drawY = c.tileY + y;
					if (IsInMapRange(drawX, drawY)) {
						map[drawX, drawY] = 0;
					}
				}
			}
		}
	}

	List<Coord> GetLine(Coord from, Coord to) {
		List<Coord> line = new List<Coord>();

		int x = from.tileX;
		int y = from.tileY;

		int dx = to.tileX - from.tileX;
		int dy = to.tileY - from.tileY;
		
		bool inverted = false;
		int step = Math.Sign(dx);
		int gradientStep = Math.Sign(dy);

		int longest = Mathf.Abs(dx);
		int shortest = Mathf.Abs(dy);

		if (longest < shortest) {
			inverted = true;
			longest = Mathf.Abs(dy);
			shortest = Mathf.Abs(dx);

			step = Math.Sign(dy);
			gradientStep = Math.Sign(dx);
		}

		int gradientAccum = longest/2;
		for (int i = 0; i < longest; i ++) {
			line.Add(new Coord(x, y));

			if (inverted) {
				y += step;
			} else {
				x += step;
			}

			gradientAccum += shortest;
			if (gradientAccum >= longest) {
				if (inverted) {
					x += gradientStep;
				} else {
					y += gradientStep;
				}
				gradientAccum -= longest;
			}
		}

		return line;
	}

	Vector3 CoordToWorldPoint(Coord tile) {
		return new Vector3(-width / 2 + .5f + tile.tileX, 2, -height / 2 + .5f + tile.tileY);
	}

	class Room : IComparable<Room> {
		public List<Coord> tiles;
		public List<Coord> edgeTiles;
		public List<Room> connectedRooms;
		public int roomSize;
		public bool isAccessibleFromMainRoom;
		public bool isMainRoom;

		public Room() {}

		public Room(List<Coord> roomTiles, int[,] map) {
			tiles = roomTiles;
			roomSize = tiles.Count;

			connectedRooms = new List<Room>();
			edgeTiles = new List<Coord>();

			foreach (Coord tile in tiles) {
				for (int x = tile.tileX -1; x <= tile.tileX +1; x ++) {
					for (int y = tile.tileY -1; y <= tile.tileY +1; y ++) {
						if (x == tile.tileX || y == tile.tileY) {
							if (map[x, y] == 1) {
								edgeTiles.Add(tile);
							}
						}
					}
				}
			}
		}

		public void SetAccessibleFromMainRoom() {
			if (!isAccessibleFromMainRoom) {
				isAccessibleFromMainRoom = true;
				foreach (Room connectedRoom in connectedRooms) {
					connectedRoom.SetAccessibleFromMainRoom();
				}
			}
		}

		public static void ConnectRooms(Room roomA, Room roomB) {
			if (roomA.isAccessibleFromMainRoom) {
				roomB.SetAccessibleFromMainRoom();
			} else if (roomB.isAccessibleFromMainRoom) {
				roomA.SetAccessibleFromMainRoom();
			}
			roomA.connectedRooms.Add(roomB);
			roomB.connectedRooms.Add(roomA);
		}

		public bool IsConnected(Room otherRoom) {
			return connectedRooms.Contains(otherRoom);
		}

		public int CompareTo(Room otherRoom) {
			return otherRoom.roomSize.CompareTo(roomSize);
		}
	}
}
