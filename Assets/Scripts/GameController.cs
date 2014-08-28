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

    private const string API_VERSION = "1.2";

    void Awake(){
        instance = this;
        zones = new Dictionary<string, TerrainZone>();
        QTree.instance.rootLocation = Path.Combine(Application.persistentDataPath, "qtree");
        if (PlayerPrefs.GetString("API", "0") != API_VERSION){
            PlayerPrefs.DeleteAll();
            QTree.instance.Clear();
        }
        InvokeRepeating("GenerateGrass", 5, 5);
    }

	// Use this for initialization
	void Start () {
        Load();
        activeZone = CreateTerrainZone(activeNode, Vector3.zero);
        tank = GameObject.Instantiate(tankPrefab) as GameObject;
        tank.transform.parent = activeZone.transform;
        tank.GetComponent<TankController>().Load();
        Save();
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
        PlayerPrefs.SetString("API", API_VERSION);
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
        tank.transform.parent = newZone.transform;
        activeNode = QTree.instance.LoadOrCreate(newZone.imutablePath);
        activeZone = newZone;
        Save();
        tank.GetComponent<TankController>().Save();
    }

    public TerrainZone CreateTerrainZone(QNode node, Vector3 zonePosition){
        TerrainZone newZone = TerrainZone.FromQNode(node, zonePosition);
        zones[node.path.imutable] = newZone;

        foreach (TerrainZone zone in zones.Values){
            QuadRelation relation = new QuadRelation(zone.transform.position - newZone.transform.position);
            // link newZone with eachOther
            newZone.adjacentZones[relation] = zone;
            // link each other zone with newZone
            zone.adjacentZones[relation.Flip()] = newZone;
        }
        return newZone;
    }

    public void RemoveTerrainZone(TerrainZone removedZone){
        foreach (TerrainZone zone in zones.Values){
            QuadRelation relation = new QuadRelation(zone.transform.position - removedZone.transform.position);
            //unlink removedZone from each other zones
            //removedZone.adjacentZones.Remove(removedZone.DeterminateRelativePosition(zone));
            removedZone.adjacentZones.Remove(relation);
            //unlink each other zone from removedZone
            //zone.adjacentZones.Remove(zone.DeterminateRelativePosition(removedZone));
            zone.adjacentZones.Remove(relation.Flip());
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
