using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ZoneBounds {
    public TerrainZone zone;
    public Bounds bounds;
    public QuadRelation rel;

    public ZoneBounds(TerrainZone zone, Bounds bounds, QuadRelation rel){
        this.zone = zone;
        this.bounds = bounds;
        this.rel = rel;
    }

    public void OnEnter(){
        GameController game = GameController.instance;
        if (zone != game.activeZone){
            game.ChangeActiveZone(zone);
            return;
        }
        if (rel == QuadRelation.Center){
            zone.RemoveAdjacents(QuadRelation.Everything);
            return;
        }

        QuadRelation removeFlags = QuadRelation.Center;

        if (rel & QuadRelation.Left){
            removeFlags |= QuadRelation.Right;
            zone.AddAdjacent(QuadRelation.Left);
        } else {
            removeFlags |= QuadRelation.Left;
        }

        if (rel & QuadRelation.Right){
            removeFlags |= QuadRelation.Left;
            zone.AddAdjacent(QuadRelation.Right);
        } else {
            removeFlags |= QuadRelation.Right;
        }


        if (rel & QuadRelation.Top){
            removeFlags |= QuadRelation.Bottom;
            zone.AddAdjacent(QuadRelation.Top);
        } else {
            removeFlags |= QuadRelation.Top;
        }
       
        if (rel & QuadRelation.Bottom){
            removeFlags |= QuadRelation.Top;
            zone.AddAdjacent(QuadRelation.Bottom);
        } else {
            removeFlags |= QuadRelation.Bottom;
        }

        if (rel == QuadRelation.TopLEft) zone.AddAdjacent(QuadRelation.TopLEft);
        if (rel == QuadRelation.TopRight) zone.AddAdjacent(QuadRelation.TopRight);
        if (rel == QuadRelation.BottomLeft) zone.AddAdjacent(QuadRelation.BottomLeft);
        if (rel == QuadRelation.BottomRight) zone.AddAdjacent(QuadRelation.BottomRight);

        zone.RemoveAdjacents(removeFlags);
    }
}

public class TerrainZone : MonoBehaviour {

    public string imutablePath;
    public List<ZoneBounds> bounds;
    public Dictionary<QuadRelation, TerrainZone> adjacentZones;

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
        adjacentZones = new Dictionary<QuadRelation, TerrainZone>();
    }

    void Start(){

    }


    #if UNITY_EDITOR
    public void OnDrawGizmos(){
        if (imutablePath == null) return;
        QNode node = QTree.instance.LoadOrCreate(imutablePath);
        if (node.terrain == null) return;

        if (Camera.current == null) return;
        float visibleZone = (Camera.current.ViewportToWorldPoint(new Vector3(0, 0, 1)) - 
            Camera.current.ViewportToWorldPoint(new Vector3(1, 1, 1))).magnitude;
        if (visibleZone > 30) return;

        for (int x=0; x<QNode.BLOCK_SIZE; x++){
            for (int y=0; y<QNode.BLOCK_SIZE; y++){
                Vector3 pos = transform.position + new Vector3(x, y, 0);
                //Handles.Label(pos, (100 * (float)activeNode.terrain[x,y].weight / (float)activeNode.totalWeights).ToString("F"));
                Handles.Label(pos, node.terrain[x,y].weight.ToString("F"));
            }
        }
    }
    #endif

    public void AddTerrainElement(Vector3 position, TerrainType terrainType){
        GameController game = GameController.instance;
        GameObject go;
        switch (terrainType){
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
        go.transform.localPosition = position;
    }

    public void CreateChildren(QNode node){
        imutablePath = node.path.imutable;
        foreach (KeyValuePair<Vector3, TerrainType> pair in node.VisibleTerrain()){
            AddTerrainElement(pair.Key, pair.Value);
        }


        Vector3 size = new Vector3(QNode.BLOCK_SIZE/3f, QNode.BLOCK_SIZE/3f, 2);
        float x = transform.position.x + (float)QNode.BLOCK_SIZE / 2f - 0.5f;
        float y = transform.position.y + (float)QNode.BLOCK_SIZE / 2f - 0.5f;
        foreach (QuadRelation rel in QuadRelation.All){
            Vector3 boundsPosition = new Vector3(
                x + rel.x * QNode.BLOCK_SIZE / 3f,
                y + rel.y * QNode.BLOCK_SIZE / 3f,
                0
            );
            bounds.Add(new ZoneBounds(this, new Bounds(boundsPosition, size), rel));
        }
    }

    public void AddAdjacent(QuadRelation relation){
        if (adjacentZones.ContainsKey(relation)) return;
        GameController game = GameController.instance;
        Vector3 zonePosition = transform.position + 
            new Vector3(QNode.BLOCK_SIZE * relation.x, QNode.BLOCK_SIZE * relation.y, 0);
        QNode node = QTree.instance.GetAdjacentTerrain(game.activeNode, relation);

        game.CreateTerrainZone(node, zonePosition);
    }

    public void RemoveAdjacents(QuadRelation mask){
        List<QuadRelation> removeKeys = new List<QuadRelation>();
        foreach (KeyValuePair<QuadRelation, TerrainZone> pair in adjacentZones){
            if ( pair.Key & mask ) {
                removeKeys.Add(pair.Key);
            }
        }
        foreach (QuadRelation relation in removeKeys){
            GameController.instance.RemoveTerrainZone(adjacentZones[relation]);
        }
    }

}
