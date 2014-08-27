using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public enum TerrainType {
    None,
    Grass,
    Tree,
    Stone,
    Water
}

public enum RelativePosition {
    Top,
    Bottom,
    Left,
    Right
}


public class LRUCache<TKey, TValue> {
    Dictionary<TKey,TValue> dict;
    List<TKey> list;
    private uint capacity;

    public LRUCache(uint capacity = 8){
        dict = new Dictionary<TKey, TValue>();
        list = new List<TKey>();
        this.capacity = capacity;
    }

    public void Clear(){
        dict.Clear();
        list.Clear();
    }

    public bool ContainsKey(TKey key){
        return dict.ContainsKey(key);
    }

    public TValue this[TKey key] {
        get {
            if (dict.ContainsKey(key)){
                list.Remove(key);
                list.Add(key);
            }
            return dict[key];
        }
        set {
            if (dict.ContainsKey(key)){
                list.Remove(key);
            }
            list.Add(key);
            dict[key] = value;
            if (list.Count > capacity){
                dict.Remove(list[0]);
                list.RemoveAt(0);
            }
        }
    }
}

public struct Position {
    public byte key;
    public Position(byte _key){ key = _key; }
    public Position(string _key){ key = Convert.ToByte(_key); }
    public Position(char _key){ key = Convert.ToByte(_key); }

    public bool HasAdjacent(RelativePosition rel){
        switch (rel){
            case RelativePosition.Top: return (key|1) == 3;
            case RelativePosition.Bottom: return (key|1) == 1;
            case RelativePosition.Left: return (key|2) == 3; 
            case RelativePosition.Right: return (key|2) == 2;
            default: return false;
        }
    }

    public Position Flip(RelativePosition pos){
        if (pos == RelativePosition.Top || pos == RelativePosition.Bottom){
            return new Position( (byte)((2&(~(key&2))) | (1&key)) );
        } else {
            return new Position( (byte)((2&key) | (1&(~(key&1)))) );
        }
    }

    public override string ToString(){
        return Convert.ToString(key);
    }
}

public class QuadPath {

    public static QuadPath empty = new QuadPath(new List<Position>());

    private List<Position> path;

    public QuadPath parent {
        get {
            if (path.Count <= 1) return QuadPath.empty;
            return new QuadPath(path.Take(path.Count - 1).ToList());
        }
    }


    public bool isRoot {
        get { return path.Count == 0; }
    }

    public Position position {
        get { return path.Last(); }
    }

    public Position root {
        get {
            if (path.Count > 0) return path.First();
            throw new UnityException("QuadPath without root");
        }
    }



    private string _imutable = "";
    public string imutable {
        get {
            if (_imutable != "") return _imutable;
            QTree tree = QTree.instance;
            int offset = 0;
            while (offset < tree.history.path.Count && offset < path.Count){
                if (tree.history.path[offset].key != path[offset].key) break;
                offset++;
            }
            _imutable = String.Join("/", path.Skip(offset).Select(p => p.ToString()).ToArray());
            return _imutable;
        }
    }

    public QuadPath Normalized(){
        List<Position> history = QTree.instance.history.path;
        if (path.Count - history.Count == 1) return this;
        path = history.Take(history.Count - path.Count + 1).Concat(path).ToList();
        return this;
    }
    
    public QuadPath(string path){
        this.path = new List<Position>();
        foreach (string p in path.Split('/')){
            this.path.Add(new Position(p));
        }
    }

    public QuadPath(QuadPath path){
        this.path = path.path;
    }

    public QuadPath(List<Position> path){
        this.path = path;
    }

    public QuadPath Child(Position pos){
        List<Position> child = new List<Position>(path);
        child.Add(pos);
        return new QuadPath(child);
    }

    public QuadPath Grow(Position root){
        List<Position> newPath = new List<Position>(path);
        newPath.Insert(0, root);
        return new QuadPath(newPath);
    }
    
    public QuadPath Grow(RelativePosition withFreeAdjacent){
        if (withFreeAdjacent == RelativePosition.Top || withFreeAdjacent == RelativePosition.Right){
            return Grow(new Position(2));
        } else {
            return Grow(new Position(1));
        }
    }

    public bool HasAdjacent(RelativePosition pos){
        if (path.Count == 0) return false;
        Normalized();
        foreach (Position currentPos in path.Reverse<Position>()){
            if (currentPos.HasAdjacent(pos)) return true;
        }
        return false;
    }

    public QuadPath FindAdjacentPath(RelativePosition adjacent){
        if (path.Count == 0) return null;
        Normalized();
        List<Position> sharedPath = new List<Position>();
        bool adjacentFound = false;
        foreach (Position pos in path.Reverse<Position>()){
            if (adjacentFound){
                sharedPath.Add(pos);
            } else {
                sharedPath.Add(pos.Flip(adjacent));
            }
            if (pos.HasAdjacent(adjacent)){
                adjacentFound = true;
            }
        }
        return new QuadPath(sharedPath.Reverse<Position>().ToList());
    }

    public override string ToString(){
        return String.Join("/", path.Select(pos => pos.ToString()).ToArray());
    }
}

delegate void SaveAction(BinaryWriter writer);

public class WeightfullTerrain {
    public TerrainType terrainType;
    public float weight = 0;

    public WeightfullTerrain(TerrainType type){
        terrainType = type;
    }


    public WeightfullTerrain(TerrainType type, float weight){
        terrainType = type;
        this.weight = weight;
    }
    
    public static WeightfullTerrain Tree {
        get { return new WeightfullTerrain(TerrainType.Tree); }
    }

    public static WeightfullTerrain Grass {
        get { return new WeightfullTerrain(TerrainType.Grass); }
    }

    public static WeightfullTerrain Stone {
        get { return new WeightfullTerrain(TerrainType.Stone); }
    }

    public static WeightfullTerrain Water {
        get { return new WeightfullTerrain(TerrainType.Water); }
    }

    public static WeightfullTerrain Empty {
        get { return new WeightfullTerrain(TerrainType.None, (float)QNode.BLOCK_SIZE); }
    }

    public bool affectWeights {
        get { return terrainType == TerrainType.Grass; }
    }
}


public class GeneratedVertexInfo {
    public int x;
    public int y;
    public QNode node;

    public GeneratedVertexInfo(int x, int y, QNode node){
        this.node = node;
        this.x = x;
        this.y = y;
    }
}

public class QInfoNode {
    public double[] weights = {0, 0, 0, 0};
    QuadPath path;

    public QInfoNode(QuadPath path){
        this.path = path;
    }

    public double totalWeight {
        get {
            return weights[0] + weights[1] + weights[2] + weights[3];
        }
    }

    private QInfoNode _parent;
    public QInfoNode parent {
        get {
            if (path.isRoot) return null;
            if (_parent != null){
                return _parent;
            }
            _parent = new QInfoNode(path.parent);
            _parent.Load();
            return _parent;
        }
    }

    public GeneratedVertexInfo GenerateVertex(){
        if (totalWeight < 0.01){
            return null;
        }
        double chance = (double)UnityEngine.Random.Range(0f, 1f);
        double luck = 0;
        int i = -1;
        while (luck < chance) luck += weights[++i]/totalWeight;

        QuadPath childpath = path.Child(new Position((byte)i));
        if (QTree.instance.HasNode(childpath.ToString())){
            return QTree.instance.LoadOrCreate(childpath).GenerateVertex();
        } else {
            QInfoNode child = new QInfoNode(childpath);
            child.Load();
            return child.GenerateVertex();
        }
    }


    public void Load(){
        string filePath = Path.Combine(Path.Combine(QTree.instance.rootLocation, path.ToString()), "info");
        if (!File.Exists(filePath)) return;
        FileStream stream = new FileStream(filePath, FileMode.Open);
        BinaryReader reader = new BinaryReader(stream);
        for (int i=0; i<4; i++){
            weights[i] = reader.ReadDouble();
        }
        reader.Close();
    }
    

    public void Save(){
        string nodePath = Path.Combine(QTree.instance.rootLocation, path.ToString());
        // try to use compression for datanodes
        // System.IO.Compression doesn't seems avaible inside Unity
        Directory.CreateDirectory(Path.GetDirectoryName(nodePath));
        FileStream stream = new FileStream(Path.Combine(nodePath, "info"), FileMode.Create);
        BinaryWriter writer = new BinaryWriter(stream);
        for (int i=0; i<4; i++){
            writer.Write(weights[i]);
        }
        writer.Close();

        if (parent == null) return;
        parent.weights[path.position.key] = totalWeight;
        parent.Save();

    }
}

public class QNode {

    public const int BLOCK_SIZE = 15;

    public QuadPath path;
    private QTree tree;
    private float grassWeight = 0;
    public WeightfullTerrain[,] terrain;


    public QNode(QTree tree, QuadPath path){
        this.tree = tree;
        this.path = path;
    }
    
    public QNode(QTree tree, string path){
        this.tree = tree;
        this.path = new QuadPath(path);
    }


    private bool _terrainFileChecked = false;
    private bool _terrainFileExists = false;
    public bool isPersist {
        get {
            if (_terrainFileChecked) return _terrainFileExists;
            _terrainFileChecked = true;
            _terrainFileExists = File.Exists(Path.Combine(tree.rootLocation, path.Normalized().ToString() + ".terrain"));
            return _terrainFileExists;
        }
    }

    private bool _isDirty = false;
    public void SetDirty(bool isDirty=true){
        if (_isDirty && isDirty) return;
        _isDirty = isDirty;
        if (!isDirty) return;

        tree.AddDirtyNode(this);
    }

    public GeneratedVertexInfo GenerateVertex(){
        float chance = UnityEngine.Random.Range(0f, 1f); 
        float luck = 0;
        for (int x = 0; x < BLOCK_SIZE; x++){
            for (int y = 0; y < BLOCK_SIZE; y++){
                luck += terrain[x,y].weight / grassWeight;
                if (luck >= chance){
                    terrain[x,y].terrainType = TerrainType.Grass;
                    ReduceWeightForNearestVertites(x, y, false);
                    return new GeneratedVertexInfo(x, y, this);
                }
            }
        }
        return null;
    }

    public void GenerateTerrain(){
        terrain = new WeightfullTerrain[BLOCK_SIZE,BLOCK_SIZE];
        grassWeight = BLOCK_SIZE * (BLOCK_SIZE * BLOCK_SIZE - 50);
        
        // make terrain elements
        for (int i = 0; i < 50; i++){
            int x = 0;
            int y = 0;
            do {
                x = UnityEngine.Random.Range(0, BLOCK_SIZE);
                y = UnityEngine.Random.Range(0, BLOCK_SIZE);
            } while (terrain[x,y] != null);

            float chance = UnityEngine.Random.Range((float)0, (float)1);
            if (chance < 0.1) terrain[x,y] = WeightfullTerrain.Tree;
            else if (chance < 0.4) terrain[x,y] = WeightfullTerrain.Grass;
            else if (chance < 0.5) terrain[x,y] = WeightfullTerrain.Water;
            else terrain[x,y] = WeightfullTerrain.Stone;
        }
        

        // fill terrain with default vertices
        for (int x=0; x<BLOCK_SIZE; x++){
            for (int y=0; y<BLOCK_SIZE; y++){
                if (terrain[x,y] == null) terrain[x,y] = WeightfullTerrain.Empty;
            }
        }

        // calculate weight for vertices
        for (int x=0; x<BLOCK_SIZE; x++){
            for (int y=0; y<BLOCK_SIZE; y++){
                if (terrain[x,y] != null && terrain[x,y].affectWeights) {
                    ReduceWeightForNearestVertites(x, y, true);
                }
            }
        }
    }

    public void ReduceWeightForNearestVertites(int x, int y, bool reflective=false){
        SetDirty();
        for (int dx=-2; dx<=2; dx++){
            for (int dy=-2; dy<=2; dy++){
                ReduceVertexWeight(x, y, dx, dy, reflective);
            }
        }
    }

    private void ReduceVertexWeight(int sourceX, int sourceY, int dx, int dy, bool reflective=false){

        QNode node = this;
        float reduceFactor = 0.15f * (3 - Mathf.Max(Mathf.Abs(dx), Mathf.Max(dy)));
        int x = (sourceX + dx + BLOCK_SIZE) % BLOCK_SIZE;
        int y = (sourceY + dy + BLOCK_SIZE) % BLOCK_SIZE;
        if (sourceX + dx < 0){
            node = tree.GetOrCreate(node.path.FindAdjacentPath(RelativePosition.Left));
        } else if (sourceX + dx >= BLOCK_SIZE){
            node = tree.GetOrCreate(node.path.FindAdjacentPath(RelativePosition.Right));
        }
        if (sourceY + dy < 0){
            node = tree.GetOrCreate(node.path.FindAdjacentPath(RelativePosition.Bottom));
        } else if (sourceY + dy >= BLOCK_SIZE){
            node = tree.GetOrCreate(node.path.FindAdjacentPath(RelativePosition.Top));
        }

        if (terrain[sourceX,sourceY].affectWeights){
            if (dx == 0 && dy == 0) reduceFactor = 1.0f;
            if (node == this && x == sourceX + dx && y == sourceY + dy){
                node.DoReduce(x, y, reduceFactor);
            } else if (node.isPersist){
                node.SetDirty();
                node.Load();
                node.DoReduce(x, y, reduceFactor);
            }
        }

        if (node != this && reflective && node.isPersist && node.terrain[x,y].affectWeights){
            DoReduce(sourceX, sourceY, reduceFactor);
        }
    }

    private void DoReduce(int x, int y, float reduceFactor){
        if (terrain[x,y].weight == 0) return;
        float reductionToApply = terrain[x,y].weight * reduceFactor;
        terrain[x,y].weight -= reductionToApply;
        grassWeight -= reductionToApply;
    }

    public void Load(){
        if (terrain != null) return;
        string filePath = Path.Combine(tree.rootLocation, path.Normalized().ToString());
        if (!isPersist) return;
        // try to use compression here
        // System.IO.Compression doesn't seems avaible inside Unity
        FileStream stream = new FileStream(filePath+".terrain", FileMode.Open);
        BinaryReader reader = new BinaryReader(stream);
        if (terrain == null) terrain = new WeightfullTerrain[BLOCK_SIZE,BLOCK_SIZE];
        grassWeight = reader.ReadSingle();
        for (int x = 0; x < BLOCK_SIZE; x++){
            for (int y = 0; y < BLOCK_SIZE; y++){
                TerrainType terrainType = (TerrainType)reader.ReadByte();
                float weight = reader.ReadSingle();
                terrain[x,y] = new WeightfullTerrain(terrainType, weight);
            }
        }
        reader.Close();
    }

    public void Save(){
        _terrainFileChecked = true;
        _terrainFileExists = true;
        string nodePath = Path.Combine(tree.rootLocation, path.Normalized().ToString()) + ".terrain";
        // try to use compression for datanodes
        // System.IO.Compression doesn't seems avaible inside Unity
        Directory.CreateDirectory(Path.GetDirectoryName(nodePath));
        FileStream stream = new FileStream(nodePath, FileMode.Create);
        BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(grassWeight);
        for ( int x=0; x<BLOCK_SIZE; x++){
            for ( int y=0; y<BLOCK_SIZE; y++){
                writer.Write((byte)terrain[x,y].terrainType);
                writer.Write(terrain[x,y].weight);
            }
        }
        writer.Close();
        QInfoNode info = new QInfoNode(path.parent);
        info.Load();
        info.weights[path.position.key] = grassWeight;
        info.Save();
    }

    
    public void UpdateWeights(){
        /*
        QNode node = this;
        do {
            QNode parent = tree.LoadOrCreate(node.path.parent);
            parent.weights[node.path.position.key] = node.totalWeights;
            parent.SaveCurrent();
            node = parent;
        } while (!node.path.isRoot);
        */
    }

    public IEnumerable<KeyValuePair<Vector3,TerrainType>> VisibleTerrain(){
        for (int x=0; x<BLOCK_SIZE; x++){
            for (int y=0; y<BLOCK_SIZE; y++){
                if (terrain[x,y].terrainType != TerrainType.None){
                    yield return new KeyValuePair<Vector3, TerrainType>(new Vector3(x, y, 0), terrain[x,y].terrainType);
                }
            }
        }
    }

}

public class QTree {

    public string rootLocation;

    public QuadPath history;

    private List<QNode> dirtyNodes;
    private LRUCache<string, QNode> cache;
    private int callDepth = 0;

    public QTree(){
        string historyPref = PlayerPrefs.GetString("TreeHistory", "");
        history = historyPref == "" ? QuadPath.empty : new QuadPath(historyPref);
        dirtyNodes = new List<QNode>();
        cache = new LRUCache<string, QNode>(16);
    }

    private static QTree _instance;
    public static QTree instance {
        get {
            if (_instance != null) return _instance;
            _instance = new QTree();
            return _instance;
        }
    }

    public void AddDirtyNode(QNode node){
        if (dirtyNodes.Contains(node)) return;
        dirtyNodes.Add(node);
    }

    public QInfoNode GetRoot(){
        QInfoNode root = new QInfoNode(QuadPath.empty);
        root.Load();
        return root;
    }
    
    public void Reparent(Position newParent){
        history = history.Grow(newParent);
        PlayerPrefs.SetString("TreeHistory", history.ToString());
        string[] dirs = Directory.GetDirectories(rootLocation);
        string[] files = Directory.GetFiles(rootLocation);

        QInfoNode newRoot = new QInfoNode(QuadPath.empty);
        newRoot.weights[newParent.key] = GetRoot().totalWeight;
        string tmpDir = Path.Combine(rootLocation, "tmp");
        Directory.CreateDirectory(tmpDir);
        foreach (string file in files){
            File.Move(file, file.Replace(rootLocation, tmpDir));
        }
        foreach (string dir in dirs){
            Directory.Move(dir, dir.Replace(rootLocation, tmpDir));
        }
        Directory.Move(tmpDir, Path.Combine(rootLocation, newParent.ToString()));
        newRoot.Save();
    }


    public GeneratedVertexInfo GenerateVertex(){
        GeneratedVertexInfo info = GetRoot().GenerateVertex(); 
        SaveDirty();
        return info;
    }


    public QNode GetTerrain(QuadPath path){
        callDepth++;
        QNode node = LoadOrCreate(path);
        if (node.terrain == null){
            node.GenerateTerrain();
        }
        callDepth--;
        if (callDepth == 0){
            SaveDirty();
        }
        return node;
    }

    public QNode GetAdjacentTerrain(QNode terrain, RelativePosition adjacent){
        if (terrain.path.HasAdjacent(adjacent)) return GetTerrain(terrain.path.FindAdjacentPath(adjacent));
        terrain.path = terrain.path.Grow(adjacent);
        Reparent(terrain.path.root);
        return GetTerrain(terrain.path.FindAdjacentPath(adjacent));
    }

    public bool HasNode(string path){
        return File.Exists(Path.Combine(rootLocation, path) + ".terrain");
    }

    public QNode Create(QuadPath path){
        return new QNode(this, path);
    }

    public QNode GetOrCreate(QuadPath path){
        if (cache.ContainsKey(path.imutable)){
            return cache[path.imutable];
        }
        return cache[path.imutable] = Create(path);
    }
    public QNode LoadOrCreate(string imutablePath){
        return LoadOrCreate(new QuadPath(imutablePath));
    }

    public QNode LoadOrCreate(QuadPath path){
        QNode node = GetOrCreate(path);
        node.Load();
        return node;

    }

    public void SaveDirty(){
        foreach (QNode node in dirtyNodes){
            node.Save();
            node.SetDirty(false);
        }
        dirtyNodes.Clear();
    }

    public void Clear(){
        history = QuadPath.empty;
        PlayerPrefs.SetString("TreeHistory", "");
        cache.Clear();
        dirtyNodes.Clear();
        Directory.Delete(rootLocation, true);
        Directory.CreateDirectory(rootLocation);
    }
}
