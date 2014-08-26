using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class GameController : MonoBehaviour {
    
    public GameObject treePrefab;
    public GameObject grassPrefab;
    public GameObject waterPrefab;
    public GameObject stonePrefab;
    public GameObject tankPrefab;

    [HideInInspector] public static GameController instance;
    [HideInInspector] public TerrainZone activeZone;
    [HideInInspector] public GameObject tank;
    [HideInInspector] public QNode activeNode;
    [HideInInspector] public QTree tree;

    private ZoneBounds activeBounds;

    void Awake(){
        instance = this;
        tree = new QTree(Path.Combine(Application.persistentDataPath, "qtree"));
        Debug.Log(Application.persistentDataPath);
    }

	// Use this for initialization
	void Start () {
        Load();
        activeZone = TerrainZone.FromQNode(activeNode);
        tank = GameObject.Instantiate(tankPrefab) as GameObject;
        tank.transform.parent = activeZone.transform;
        tank.GetComponent<TankController>().Load();
	}

    void Restart(){
        tree.Clear();
        activeZone = null;
        activeNode = null;
        tank = null;
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("TerrainZone")){
            Destroy(go);
        }
        PlayerPrefs.DeleteAll();
        Start();
    }

    public void Load(){
        activeNode = tree.GetTerrain(new QuadPath(PlayerPrefs.GetString("path", "3")));
    }

    public void Save(){
        PlayerPrefs.SetString("path", activeNode.path.ToString());
    }

    public void Update(){
        if (activeZone == null) return;
        if (activeZone.bounds == null) return;
        List<ZoneBounds> boundsWithAdjacent = new List<ZoneBounds>(activeZone.bounds);
        foreach(TerrainZone adjacentZone in activeZone.adjacentZones.Values){
            boundsWithAdjacent.AddRange(adjacentZone.bounds);
        }
        foreach(ZoneBounds bounds in boundsWithAdjacent){
            if (bounds.bounds.Contains(tank.transform.position)){
                if (bounds != activeBounds) {
                    bounds.OnEnter();
                    activeBounds = bounds;
                }
                break;
            }
        }
    }

    public void ChangeActiveZone(TerrainZone newZone){
        byte relativePosition = 0;
        foreach (KeyValuePair<byte, TerrainZone> pair in activeZone.adjacentZones){
            if (pair.Value == newZone) {
                relativePosition = pair.Key;
                break;
            }
        }
        if ((relativePosition & TerrainZone.TOP) > 0){
            activeNode = tree.GetAdjacentTerrain(activeNode, RelativePosition.Top);
        } else if ((relativePosition & TerrainZone.BOTTOM) > 0){
            activeNode = tree.GetAdjacentTerrain(activeNode, RelativePosition.Bottom);
        }
        if ((relativePosition & TerrainZone.LEFT) > 0){
            activeNode = tree.GetAdjacentTerrain(activeNode, RelativePosition.Left);
        } else if ((relativePosition & TerrainZone.RIGHT) > 0){
            activeNode = tree.GetAdjacentTerrain(activeNode, RelativePosition.Right);
        }

        tank.transform.parent = newZone.transform;
        activeZone = newZone;
        Save();
        tank.GetComponent<TankController>().Save();
    }

    void OnGUI(){
        //GUI.Label(new Rect(10, 10, 100, 50), activeNode.path.ToString());
        if (GUI.Button(new Rect(10, 10, 100, 50), "New Game")){
            Restart();
        }
    }

    void OnDestroy(){
        Save();
    }

}
