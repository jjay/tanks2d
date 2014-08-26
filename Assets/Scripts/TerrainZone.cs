﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class ZoneBounds {
    public TerrainZone zone;
    public Bounds bounds;
    public byte boundsPosition;

    public ZoneBounds(TerrainZone zone, Bounds bounds, byte boundsPosition){
        this.zone = zone;
        this.bounds = bounds;
        this.boundsPosition = boundsPosition;
    }

    public void OnEnter(){
        GameController game = GameController.instance;
        if (zone != game.activeZone){
            game.ChangeActiveZone(zone);
            return;
        }
        if (boundsPosition == TerrainZone.CENTER){
            zone.RemoveAdjacents(TerrainZone.TOP|TerrainZone.BOTTOM|TerrainZone.LEFT|TerrainZone.RIGHT);
            return;
        }

        byte removeFlags = 0;
        if ((boundsPosition & TerrainZone.LEFT) > 0){
            removeFlags |= TerrainZone.RIGHT;
            zone.AddAdjacent(TerrainZone.LEFT);
        }
        if ((boundsPosition & TerrainZone.LEFT) == 0){
            removeFlags |= TerrainZone.LEFT;
        }
        if ((boundsPosition & TerrainZone.RIGHT) > 0){
            removeFlags |= TerrainZone.LEFT;
            zone.AddAdjacent(TerrainZone.RIGHT);
        }
        if ((boundsPosition & TerrainZone.RIGHT) == 0){
            removeFlags |= TerrainZone.RIGHT;
        }
        if ((boundsPosition & TerrainZone.TOP) > 0){
            removeFlags |= TerrainZone.BOTTOM;
            zone.AddAdjacent(TerrainZone.TOP);
        }
        if ((boundsPosition & TerrainZone.TOP) == 0){
            removeFlags |= TerrainZone.TOP;
        }
        if ((boundsPosition & TerrainZone.BOTTOM) > 0){
            removeFlags |= TerrainZone.TOP;
            zone.AddAdjacent(TerrainZone.BOTTOM);
        }
        if ((boundsPosition & TerrainZone.BOTTOM) == 0){
            removeFlags |= TerrainZone.BOTTOM;
        }
        if ((boundsPosition & (TerrainZone.TOP|TerrainZone.LEFT)) == (TerrainZone.TOP|TerrainZone.LEFT)){
            zone.AddAdjacent(TerrainZone.TOP|TerrainZone.LEFT);
        }

        if ((boundsPosition & (TerrainZone.TOP|TerrainZone.RIGHT)) == (TerrainZone.TOP|TerrainZone.RIGHT)){
            zone.AddAdjacent(TerrainZone.TOP|TerrainZone.RIGHT);
        }

        if ((boundsPosition & (TerrainZone.BOTTOM|TerrainZone.LEFT)) == (TerrainZone.BOTTOM|TerrainZone.LEFT)){
            zone.AddAdjacent(TerrainZone.BOTTOM|TerrainZone.LEFT);
        }
        if ((boundsPosition & (TerrainZone.BOTTOM|TerrainZone.RIGHT)) == (TerrainZone.BOTTOM|TerrainZone.RIGHT)){
            zone.AddAdjacent(TerrainZone.BOTTOM|TerrainZone.RIGHT);
        }
        zone.RemoveAdjacents(removeFlags);
    }
}

public class TerrainZone : MonoBehaviour {
    public const byte CENTER = 0;
    public const byte TOP = 1;
    public const byte BOTTOM = 2;
    public const byte LEFT = 4;
    public const byte RIGHT = 8;

    public List<ZoneBounds> bounds;
    public Dictionary<byte, TerrainZone> adjacentZones;

    public static TerrainZone FromQNode(QNode node, Vector3 position = default(Vector3)){
        GameObject go = new GameObject("Zone");
        go.tag = "TerrainZone";
        go.transform.position = position;
        TerrainZone zone = go.AddComponent<TerrainZone>();
        zone.CreateChildren(node);
        return zone;
    }

    void Awake(){
        bounds = new List<ZoneBounds>();
        adjacentZones = new Dictionary<byte, TerrainZone>();
    }

    void Start(){

    }

    public void OnDrawGizmos(){
        foreach (ZoneBounds b in bounds){
            Gizmos.DrawWireCube(b.bounds.center, b.bounds.size);
        }
    }

    public void CreateChildren(QNode node){
        GameController game = GameController.instance;
        foreach (KeyValuePair<Vector3, TerrainType> pair in node.terrain){
            GameObject go;
            switch (pair.Value){
                case TerrainType.Grass: 
                    go = GameObject.Instantiate(game.grassPrefab) as GameObject; 
                    break;
                case TerrainType.Stone:
                    go = GameObject.Instantiate(game.stonePrefab) as GameObject;
                    break;
                case TerrainType.Tree:
                    go = GameObject.Instantiate(game.treePrefab) as GameObject;
                    break;
                case TerrainType.Water:
                    go = GameObject.Instantiate(game.waterPrefab) as GameObject;
                    break;
                default:
                    go = new GameObject("Empty");
                    break;
            }
            go.transform.parent = transform;
            go.transform.localPosition = pair.Key;
        }

        float xPart = (float)QNode.BLOCK_SIZE / 3f;
        float yPart = (float)QNode.BLOCK_SIZE / 3f;
        float xCenter = transform.position.x + (float)QNode.BLOCK_SIZE / 2f - 0.5f;
        float yCenter = transform.position.y + (float)QNode.BLOCK_SIZE / 2f - 0.5f;
        Vector3 size = new Vector3(xPart, yPart, 2);
        for (byte pos=0; pos<16; pos++){
            if ( (pos & (TOP|BOTTOM)) == (TOP|BOTTOM)) continue;
            if ( (pos & (LEFT|RIGHT)) == (LEFT|RIGHT)) continue;
            float x = (float)( xCenter - xPart * (pos&LEFT)/LEFT + xPart * (pos&RIGHT)/RIGHT);
            float y = (float)( yCenter - yPart * (pos&BOTTOM)/BOTTOM + yPart * (pos&TOP)/TOP);
            bounds.Add(new ZoneBounds(
                this,
                new Bounds(new Vector3(x, y, 0), size),
                pos
            ));
        }
    }

    public byte DeterminateRelativePosition(TerrainZone adjacent){
        byte pos = 0;
        Vector3 delta = transform.position - adjacent.transform.position;
        if ( delta.x > 0.1f){
            pos |= LEFT;
        } else if (delta.x < -0.1f){
            pos |= RIGHT;
        }
        if ( delta.y > 0.1){
            pos |= BOTTOM;
        } else if (delta.y < -0.1f){
            pos |= TOP;
        }

        return pos;
    }


    public void AddAdjacent(byte pos){
        if (adjacentZones.ContainsKey(pos)) return;
        GameController game = GameController.instance;
        QNode node = game.activeNode;
        Vector3 zonePosition = transform.position;

        if ( (pos&TOP) > 0 ){
            node = game.tree.GetAdjacentTerrain(node, RelativePosition.Top);
            zonePosition += Vector3.up * QNode.BLOCK_SIZE;
        } else if ( (pos&BOTTOM) > 0){
            node = game.tree.GetAdjacentTerrain(node, RelativePosition.Bottom);
            zonePosition += Vector3.down * QNode.BLOCK_SIZE;
        }

        if ( (pos&LEFT) > 0){
            node = game.tree.GetAdjacentTerrain(node, RelativePosition.Left);
            zonePosition += Vector3.left * QNode.BLOCK_SIZE;
        } else if ( (pos&RIGHT) > 0){
            node = game.tree.GetAdjacentTerrain(node, RelativePosition.Right);
            zonePosition += Vector3.right * QNode.BLOCK_SIZE;
        }

        TerrainZone newZone = TerrainZone.FromQNode(node, zonePosition);
        GameObject[] zones = GameObject.FindGameObjectsWithTag("TerrainZone");

        foreach (GameObject go in zones){
            TerrainZone zone = go.GetComponent<TerrainZone>();
            // link newZone with eachOther
            newZone.adjacentZones[newZone.DeterminateRelativePosition(zone)] = zone;
            // link each other zone with newZone
            zone.adjacentZones[zone.DeterminateRelativePosition(newZone)] = newZone;
        }
    }

    public void RemoveAdjacents(byte mask){
        List<byte> removeKeys = new List<byte>();
        foreach (KeyValuePair<byte, TerrainZone> pair in adjacentZones){
            if ( (pair.Key & mask) > 0) {
                removeKeys.Add(pair.Key);
            }
        }
        foreach (byte key in removeKeys){
            TerrainZone removedZone = adjacentZones[key];
            GameObject[] zones = GameObject.FindGameObjectsWithTag("TerrainZone");
            foreach (GameObject go in zones){
                TerrainZone zone = go.GetComponent<TerrainZone>();
                //unlink removedZone from each other zones
                removedZone.adjacentZones.Remove(removedZone.DeterminateRelativePosition(zone));
                //unlink each other zone from removedZone
                zone.adjacentZones.Remove(zone.DeterminateRelativePosition(removedZone));
            }
            GameObject.Destroy(removedZone.gameObject);
        }
    }

}