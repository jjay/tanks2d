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

    private ZoneBounds activeBounds;
    private Dictionary<string, TerrainZone> zones;

    void Awake(){
        instance = this;
        //PlayerPrefs.DeleteAll();
        zones = new Dictionary<string, TerrainZone>();
        QTree.instance.rootLocation = Path.Combine(Application.persistentDataPath, "qtree");
        InvokeRepeating("GenerateGrass", 5, 5);
    }
	// Use this for initialization
	void Start () {
        Load();
        activeZone = CreateTerrainZone(activeNode, Vector3.zero);
        tank = GameObject.Instantiate(tankPrefab) as GameObject;
        tank.transform.parent = activeZone.transform;
        tank.GetComponent<TankController>().Load();
	}

    void Restart(){
        QTree.instance.Clear();
        activeZone = null;
        activeNode = null;
        tank = null;
        zones.Clear();
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("TerrainZone")){
            Destroy(go);
        }
        PlayerPrefs.DeleteAll();
        Start();
    }

    public void Load(){
        activeNode = QTree.instance.GetTerrain(new QuadPath(PlayerPrefs.GetString("path", "3")));
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
            activeNode = QTree.instance.GetAdjacentTerrain(activeNode, RelativePosition.Top);
        } else if ((relativePosition & TerrainZone.BOTTOM) > 0){
            activeNode = QTree.instance.GetAdjacentTerrain(activeNode, RelativePosition.Bottom);
        }
        if ((relativePosition & TerrainZone.LEFT) > 0){
            activeNode = QTree.instance.GetAdjacentTerrain(activeNode, RelativePosition.Left);
        } else if ((relativePosition & TerrainZone.RIGHT) > 0){
            activeNode = QTree.instance.GetAdjacentTerrain(activeNode, RelativePosition.Right);
        }

        tank.transform.parent = newZone.transform;
        activeZone = newZone;
        Save();
        tank.GetComponent<TankController>().Save();
    }

    public TerrainZone CreateTerrainZone(QNode node, Vector3 zonePosition){
        TerrainZone newZone = TerrainZone.FromQNode(node, zonePosition);
        zones[node.path.imutable] = newZone;

        foreach (TerrainZone zone in zones.Values){
            // link newZone with eachOther
            newZone.adjacentZones[newZone.DeterminateRelativePosition(zone)] = zone;
            // link each other zone with newZone
            zone.adjacentZones[zone.DeterminateRelativePosition(newZone)] = newZone;
        }
        return newZone;
    }

    public void RemoveTerrainZone(TerrainZone removedZone){
        foreach (TerrainZone zone in zones.Values){
            //unlink removedZone from each other zones
            removedZone.adjacentZones.Remove(removedZone.DeterminateRelativePosition(zone));
            //unlink each other zone from removedZone
            zone.adjacentZones.Remove(zone.DeterminateRelativePosition(removedZone));
        }
        zones.Remove(removedZone.imutablePath);
        GameObject.Destroy(removedZone.gameObject);
    }

    public void GenerateGrass(){
        GeneratedVertexInfo info = QTree.instance.GenerateVertex();
        if (info == null){
            Debug.LogWarning("No avaible space for generating new grass");
            return;
        }
        if (zones.ContainsKey(info.node.path.imutable)){
            zones[info.node.path.imutable].AddTerrainElement(new Vector3(info.x, info.y, 0), TerrainType.Grass);
        }
    }

    void OnGUI(){
        if (GUI.Button(new Rect(10, 10, 100, 50), "New Game")){
            Restart();
        }
    }

    void OnDestroy(){
        Save();
    }
}
